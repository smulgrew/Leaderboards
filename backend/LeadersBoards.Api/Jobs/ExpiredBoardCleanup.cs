using LeadersBoards.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeadersBoards.Api.Jobs;

/// <summary>
/// Deletes boards whose ExpiresAt has passed (public one-off boards, 30 days after the event).
/// Runs inside the API process once an hour. The delete is one SQL statement; Postgres cascades
/// it to events, scores, hearts, comments, views and memberships via the foreign keys.
///
/// Note: on free hosting that sleeps when idle, the timer only runs while the API is awake.
/// That's fine here (deletion being a few hours late doesn't matter), and expired boards are
/// already hidden from search. If it ever matters, move the same DELETE to Supabase pg_cron.
/// </summary>
public class ExpiredBoardCleanup(IServiceScopeFactory scopes, ILogger<ExpiredBoardCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                // A DbContext is "scoped" (one per request), so a background job makes its own scope.
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var deleted = await DeleteExpired(db, DateTimeOffset.UtcNow, stoppingToken);
                if (deleted > 0) log.LogInformation("Deleted {Count} expired boards", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Expired board cleanup failed; will retry next hour");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static Task<int> DeleteExpired(AppDbContext db, DateTimeOffset now, CancellationToken ct = default) =>
        db.Leaderboards.Where(b => b.ExpiresAt != null && b.ExpiresAt <= now).ExecuteDeleteAsync(ct);
}
