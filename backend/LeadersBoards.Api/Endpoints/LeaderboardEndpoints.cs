using LeadersBoards.Api.Auth;
using LeadersBoards.Api.Contracts;
using LeadersBoards.Api.Data;
using LeadersBoards.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadersBoards.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboards(this WebApplication app)
    {
        var g = app.MapGroup("/").RequireDevUser();

        // Boards I'm an accepted member of.
        g.MapGet("/me/leaderboards", async (HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            return await db.Memberships
                .Where(m => m.UserId == me && m.Status == MembershipStatus.Accepted
                            && (m.Leaderboard.Status == LeaderboardStatus.Published
                                || m.Role != MemberRole.Member))
                .OrderByDescending(m => m.Leaderboard.CreatedAt)
                .Select(m => new LeaderboardSummaryDto(m.LeaderboardId, m.Leaderboard.Name, m.Leaderboard.Kind,
                    m.Leaderboard.Visibility, m.Leaderboard.Status, m.Role, m.Leaderboard.Events.Count,
                    m.Leaderboard.StartsAt))
                .ToListAsync();
        });

        // Discover public boards: published, not finished yet. Optional ?q= name search.
        g.MapGet("/leaderboards/public", async (string? q, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var now = DateTimeOffset.UtcNow;
            var query = db.Leaderboards.Where(b => b.Visibility == Visibility.Public
                && b.Status == LeaderboardStatus.Published && (b.EndsAt == null || b.EndsAt > now));
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(b => EF.Functions.ILike(b.Name, $"%{q.Trim()}%"));

            var rows = await query.OrderBy(b => b.StartsAt ?? b.CreatedAt).Take(50)
                .Select(b => new
                {
                    b.Id, b.Name, b.Description, b.Kind, b.StartsAt, b.EndsAt, b.MemberLimit,
                    Count = b.Memberships.Count(m => m.Status != MembershipStatus.Declined),
                    AmMember = b.Memberships.Any(m => m.UserId == me && m.Status == MembershipStatus.Accepted),
                }).ToListAsync();
            return rows.Select(r => new PublicLeaderboardDto(r.Id, r.Name, r.Description, r.Kind, r.StartsAt, r.EndsAt,
                r.Count, r.MemberLimit is int l && r.Count >= l, r.AmMember));
        });

        // Create a draft board (optionally with its events in one go, e.g. from AI import).
        g.MapPost("/leaderboards", async (CreateLeaderboardRequest req, HttpContext http, AppDbContext db,
            IOptions<PlanOptions> plans) =>
        {
            if (req.Kind == LeaderboardKind.OneOff && req.StartsAt is null)
                return Results.BadRequest("A one-off event needs a start date.");
            if (req.EndsAt is not null && req.StartsAt is not null && req.EndsAt < req.StartsAt)
                return Results.BadRequest("End is before start.");

            var me = http.UserId();
            var board = new Leaderboard
            {
                Name = req.Name.Trim(), Description = req.Description, OwnerId = me,
                Kind = req.Kind, Visibility = req.Visibility,
                StartsAt = req.StartsAt,
                // One-offs default to closing scores 2 days after they start; ongoing boards never close.
                EndsAt = req.EndsAt ?? (req.Kind == LeaderboardKind.OneOff ? req.StartsAt!.Value.AddDays(2) : null),
                MemberLimit = BoardRules.InitialMemberLimit(req.Visibility, plans.Value),
            };
            board.Memberships.Add(new Membership
            {
                UserId = me, Role = MemberRole.Owner, Status = MembershipStatus.Accepted,
                RespondedAt = DateTimeOffset.UtcNow
            });
            var pos = 0;
            foreach (var e in req.Events ?? []) board.Events.Add(NewEvent(e, pos++));
            db.Leaderboards.Add(board);
            await db.SaveChangesAsync();
            return Results.Created($"/leaderboards/{board.Id}", new { board.Id });
        });

        g.MapGet("/leaderboards/{id:guid}", async (Guid id, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var m = await db.AcceptedMembership(id, me);
            if (m is null || (m.Leaderboard.Status == LeaderboardStatus.Draft && !m.IsAdmin)) return Results.NotFound();
            var b = m.Leaderboard;

            // Ongoing boards: newest day first (today's WOD on top). One-offs: in running order.
            var eventsQuery = db.Events.Where(e => e.LeaderboardId == id);
            eventsQuery = b.Kind == LeaderboardKind.Ongoing
                ? eventsQuery.OrderByDescending(e => e.ScheduledOn).ThenBy(e => e.Position)
                : eventsQuery.OrderBy(e => e.Position);
            var events = await eventsQuery
                .Select(e => new EventDto(e.Id, e.Name, e.Description, e.ScoreType, e.SortDirection,
                    e.TimeCapSeconds, e.Position, e.ScheduledOn, e.Scores.Any(s => s.UserId == me)))
                .ToListAsync();

            return Results.Ok(new LeaderboardDto(b.Id, b.Name, b.Description, b.Kind, b.Visibility, b.Status,
                b.StartsAt, b.EndsAt, b.Plan, b.MemberLimit, await db.MemberCount(id),
                BoardRules.NeedsPaymentToPublish(b), m.Role, events));
        });

        // Public boards: tap Join, no invite needed.
        g.MapPost("/leaderboards/{id:guid}/join", async (Guid id, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var b = await db.Leaderboards.FindAsync(id);
            if (b is null || b.Visibility != Visibility.Public || b.Status != LeaderboardStatus.Published)
                return Results.NotFound();

            var existing = await db.Memberships.FindAsync(id, me);
            if (existing is { Status: MembershipStatus.Accepted }) return Results.NoContent();
            // A pending invite already holds a seat; anyone else needs a free one.
            if (existing is not { Status: MembershipStatus.Invited } && BoardRules.IsFull(b, await db.MemberCount(id)))
                return Results.Conflict("This board is full.");
            if (existing is null)
            {
                existing = new Membership { LeaderboardId = id, UserId = me };
                db.Memberships.Add(existing);
            }
            existing.Status = MembershipStatus.Accepted;
            existing.RespondedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Add an event to a board (admins only). For ongoing boards, pass scheduledOn (e.g. today's WOD).
        g.MapPost("/leaderboards/{id:guid}/events", async (Guid id, CreateEventRequest req, HttpContext http, AppDbContext db) =>
        {
            var m = await db.AcceptedMembership(id, http.UserId());
            if (m is null || !m.IsAdmin) return Results.StatusCode(403);
            var pos = await db.Events.CountAsync(e => e.LeaderboardId == id);
            var ev = NewEvent(req, pos);
            ev.LeaderboardId = id;
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            return Results.Created($"/events/{ev.Id}", new { ev.Id });
        });

        g.MapDelete("/events/{eventId:guid}", async (Guid eventId, HttpContext http, AppDbContext db) =>
        {
            var (ev, m) = await db.EventForMember(eventId, http.UserId());
            if (ev is null || m is null) return Results.NotFound();
            if (!m.IsAdmin) return Results.StatusCode(403);
            if (await db.Scores.AnyAsync(s => s.EventId == eventId))
                return Results.Conflict("This event already has scores.");
            db.Events.Remove(ev);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Draft -> Published. One-off comps must be paid for first (402 Payment Required).
        g.MapPost("/leaderboards/{id:guid}/publish", async (Guid id, HttpContext http, AppDbContext db) =>
        {
            var m = await db.AcceptedMembership(id, http.UserId());
            if (m is null || !m.IsAdmin) return Results.StatusCode(403);
            // Ongoing boards may start empty (WODs get added day by day); one-offs need their events up front.
            if (m.Leaderboard.Kind == LeaderboardKind.OneOff && !await db.Events.AnyAsync(e => e.LeaderboardId == id))
                return Results.BadRequest("Add at least one event before publishing.");
            if (BoardRules.NeedsPaymentToPublish(m.Leaderboard))
                return Results.Problem("Pay for this one-off event before publishing (POST /leaderboards/{id}/checkout).",
                    statusCode: 402);
            m.Leaderboard.Status = LeaderboardStatus.Published;
            m.Leaderboard.PublishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Start a payment (owner/admin). Today it only records a Pending payment; with Stripe this
        // creates a Checkout Session and returns its URL for the app to open in the browser.
        g.MapPost("/leaderboards/{id:guid}/checkout", async (Guid id, CheckoutRequest req, HttpContext http,
            AppDbContext db, IOptions<PlanOptions> plans) =>
        {
            var me = http.UserId();
            var m = await db.AcceptedMembership(id, me);
            if (m is null || !m.IsAdmin) return Results.StatusCode(403);
            if (req.Kind == PaymentKind.OneOffEvent && m.Leaderboard.Kind != LeaderboardKind.OneOff)
                return Results.BadRequest("Only one-off events use the one-off fee.");
            if (req.Kind == PaymentKind.OneOffEvent && m.Leaderboard.Plan == BoardPlan.OneOffEvent)
                return Results.Conflict("Already paid.");

            var (amount, currency) = BoardRules.Price(req.Kind, plans.Value);
            var p = new Payment { LeaderboardId = id, UserId = me, Kind = req.Kind, AmountCents = amount, Currency = currency };
            db.Payments.Add(p);
            await db.SaveChangesAsync();
            return Results.Ok(new CheckoutDto(p.Id, amount, currency, CheckoutUrl: null));
        });
    }

    private static LeaderboardEvent NewEvent(CreateEventRequest e, int position) => new()
    {
        Name = e.Name.Trim(),
        Description = e.Description,
        ScoreType = e.ScoreType,
        SortDirection = e.SortDirection ?? ScoreRules.DefaultSortFor(e.ScoreType),
        TimeCapSeconds = e.TimeCapSeconds,
        ScheduledOn = e.ScheduledOn,
        Position = position
    };
}
