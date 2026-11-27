using LeadersBoards.Api.Auth;
using LeadersBoards.Api.Contracts;
using LeadersBoards.Api.Data;
using LeadersBoards.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeadersBoards.Api.Endpoints;

public static class InviteEndpoints
{
    public static void MapInvites(this WebApplication app)
    {
        var g = app.MapGroup("/").RequireDevUser();

        // Admin invites someone by email. MVP assumption: they must already have an account.
        g.MapPost("/leaderboards/{id:guid}/invites", async (Guid id, InviteRequest req, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var m = await db.AcceptedMembership(id, me);
            if (m is null || !m.IsAdmin) return Results.StatusCode(403);
            if (req.Role == MemberRole.Owner) return Results.BadRequest("A board has one owner.");

            var invitee = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.Trim().ToLowerInvariant());
            if (invitee is null) return Results.NotFound("No user with that email yet.");

            var existing = await db.Memberships.FindAsync(id, invitee.Id);
            if (existing is { Status: MembershipStatus.Accepted }) return Results.Conflict("Already a member.");
            // Pending invites hold a seat, so check the cap now rather than at accept time.
            if (existing is not { Status: MembershipStatus.Invited } && BoardRules.IsFull(m.Leaderboard, await db.MemberCount(id)))
                return Results.Problem($"This board is full ({m.Leaderboard.MemberLimit} members). Upgrade to add more.",
                    statusCode: 402);
            if (existing is null)
            {
                existing = new Membership { LeaderboardId = id, UserId = invitee.Id };
                db.Memberships.Add(existing);
            }
            // Re-inviting someone who declined resets their invite.
            existing.Status = MembershipStatus.Invited;
            existing.Role = req.Role;
            existing.InvitedById = me;
            existing.InvitedAt = DateTimeOffset.UtcNow;
            existing.RespondedAt = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/me/invites", async (HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            return await db.Memberships
                .Where(m => m.UserId == me && m.Status == MembershipStatus.Invited)
                .Select(m => new InviteDto(m.LeaderboardId, m.Leaderboard.Name,
                    db.Users.Where(u => u.Id == m.InvitedById).Select(u => u.DisplayName).FirstOrDefault() ?? "",
                    m.InvitedAt))
                .ToListAsync();
        });

        g.MapPost("/leaderboards/{id:guid}/invites/accept", (Guid id, HttpContext http, AppDbContext db) =>
            Respond(id, http.UserId(), db, MembershipStatus.Accepted));
        g.MapPost("/leaderboards/{id:guid}/invites/decline", (Guid id, HttpContext http, AppDbContext db) =>
            Respond(id, http.UserId(), db, MembershipStatus.Declined));
    }

    private static async Task<IResult> Respond(Guid id, Guid me, AppDbContext db, MembershipStatus answer)
    {
        var m = await db.Memberships.FindAsync(id, me);
        if (m is not { Status: MembershipStatus.Invited }) return Results.NotFound();
        m.Status = answer;
        m.RespondedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
