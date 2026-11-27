using LeadersBoards.Api.Auth;
using LeadersBoards.Api.Contracts;
using LeadersBoards.Api.Data;
using LeadersBoards.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeadersBoards.Api.Endpoints;

/// <summary>Scores, results (with the hidden-until-you-post rule), seen-by, hearts and comments.</summary>
public static class EventEndpoints
{
    public static void MapEvents(this WebApplication app)
    {
        var g = app.MapGroup("/").RequireDevUser();

        // Submit or update my score for an event.
        g.MapPut("/events/{eventId:guid}/scores/me", async (Guid eventId, SubmitScoreRequest req, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var (ev, m) = await db.EventForMember(eventId, me);
            if (ev is null || m is null) return Results.NotFound();
            if (ev.Leaderboard.Status != LeaderboardStatus.Published)
                return Results.BadRequest("Scores open once the board is published.");
            if (!m.IsAdmin && BoardRules.ScoresClosed(ev.Leaderboard, DateTimeOffset.UtcNow))
                return Results.BadRequest("This event has finished; scores are closed.");
            if (ScoreRules.Validate(ev, req.PrimaryValue, req.SecondaryValue, req.IsCapped) is string error)
                return Results.BadRequest(error);

            var score = await db.Scores.FirstOrDefaultAsync(s => s.EventId == eventId && s.UserId == me);
            if (score is null)
            {
                score = new Score { EventId = eventId, UserId = me };
                db.Scores.Add(score);
            }
            score.PrimaryValue = req.PrimaryValue;
            score.SecondaryValue = req.SecondaryValue;
            score.IsCapped = req.IsCapped;
            score.IsRx = req.IsRx;
            score.Notes = req.Notes;
            score.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { score.Id });
        });

        // The leaderboard for one event. Locked unless you're an admin or have posted.
        g.MapGet("/events/{eventId:guid}/results", async (Guid eventId, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var (ev, m) = await db.EventForMember(eventId, me);
            if (ev is null || m is null) return Results.NotFound();

            var entryCount = await db.Scores.CountAsync(s => s.EventId == eventId);
            if (!await db.CanSeeResults(ev, m))
                return Results.Ok(new ResultsDto(ev.Id, ev.Name, ev.ScoreType, Locked: true, entryCount, []));

            var scores = await db.Scores.Where(s => s.EventId == eventId)
                .Include(s => s.User).Include(s => s.Hearts).ToListAsync();
            var entries = ScoreRules.Rank(ev, scores).Select(r => new ResultEntryDto(
                r.Rank, r.Score.Id, r.Score.UserId, r.Score.User.DisplayName,
                ScoreRules.Format(ev.ScoreType, r.Score), r.Score.PrimaryValue, r.Score.SecondaryValue,
                r.Score.IsCapped, r.Score.IsRx, r.Score.Hearts.Count,
                r.Score.Hearts.Any(h => h.UserId == me), r.Score.SubmittedAt)).ToList();

            await RecordView(db, eventId, me);
            return Results.Ok(new ResultsDto(ev.Id, ev.Name, ev.ScoreType, Locked: false, entryCount, entries));
        });

        // Who has seen this event's results. Same visibility rule as the results themselves.
        g.MapGet("/events/{eventId:guid}/seen-by", async (Guid eventId, HttpContext http, AppDbContext db) =>
        {
            var (ev, m) = await db.EventForMember(eventId, http.UserId());
            if (ev is null || m is null) return Results.NotFound();
            if (!await db.CanSeeResults(ev, m)) return Results.StatusCode(403);
            var views = await db.EventViews.Where(v => v.EventId == eventId).OrderBy(v => v.FirstSeenAt)
                .Select(v => new SeenByDto(v.UserId, v.User.DisplayName, v.FirstSeenAt)).ToListAsync();
            return Results.Ok(views);
        });

        // Hearts: PUT to like, DELETE to unlike. Both are safe to repeat.
        g.MapPut("/scores/{scoreId:guid}/heart", (Guid scoreId, HttpContext http, AppDbContext db) =>
            SetHeart(db, scoreId, http.UserId(), on: true));
        g.MapDelete("/scores/{scoreId:guid}/heart", (Guid scoreId, HttpContext http, AppDbContext db) =>
            SetHeart(db, scoreId, http.UserId(), on: false));

        // Comments are per event and also hidden until you've posted (they'd spoil the results).
        g.MapGet("/events/{eventId:guid}/comments", async (Guid eventId, HttpContext http, AppDbContext db) =>
        {
            var (ev, m) = await db.EventForMember(eventId, http.UserId());
            if (ev is null || m is null) return Results.NotFound();
            if (!await db.CanSeeResults(ev, m)) return Results.StatusCode(403);
            var comments = await db.Comments.Where(c => c.EventId == eventId && c.DeletedAt == null)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto(c.Id, c.UserId, c.User.DisplayName, c.Body, c.CreatedAt)).ToListAsync();
            return Results.Ok(comments);
        });

        g.MapPost("/events/{eventId:guid}/comments", async (Guid eventId, CreateCommentRequest req, HttpContext http, AppDbContext db) =>
        {
            var me = http.UserId();
            var (ev, m) = await db.EventForMember(eventId, me);
            if (ev is null || m is null) return Results.NotFound();
            if (!await db.CanSeeResults(ev, m)) return Results.StatusCode(403);
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest("Comment is empty.");
            var c = new Comment { EventId = eventId, UserId = me, Body = req.Body.Trim() };
            db.Comments.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/comments/{c.Id}", new { c.Id });
        });
    }

    private static async Task RecordView(AppDbContext db, Guid eventId, Guid userId)
    {
        var view = await db.EventViews.FindAsync(eventId, userId);
        if (view is null) db.EventViews.Add(new EventView { EventId = eventId, UserId = userId });
        else view.LastSeenAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private static async Task<IResult> SetHeart(AppDbContext db, Guid scoreId, Guid me, bool on)
    {
        var score = await db.Scores.FindAsync(scoreId);
        if (score is null) return Results.NotFound();
        var (ev, m) = await db.EventForMember(score.EventId, me);
        if (ev is null || m is null) return Results.NotFound();
        if (!await db.CanSeeResults(ev, m)) return Results.StatusCode(403);

        var heart = await db.Hearts.FindAsync(scoreId, me);
        if (on && heart is null) db.Hearts.Add(new Heart { ScoreId = scoreId, UserId = me });
        if (!on && heart is not null) db.Hearts.Remove(heart);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
