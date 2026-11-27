using LeadersBoards.Api.Contracts;
using LeadersBoards.Api.Data;
using LeadersBoards.Api.Domain;
using Microsoft.EntityFrameworkCore;
using LeadersBoards.Api.Jobs;

namespace LeadersBoards.Api.Endpoints;

public static class DevAuthEndpoints
{
    /// <summary>DEV ONLY. Sign in (or sign up) by email with no password. Mapped only in Development.</summary>
    public static void MapDevAuth(this WebApplication app)
    {
        app.MapPost("/dev/login", async (DevLoginRequest req, AppDbContext db) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                user = new User { Email = email, DisplayName = req.DisplayName.Trim() };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            return new UserDto(user.Id, user.Email, user.DisplayName);
        });

        // DEV ONLY. Pretend the payment provider said "paid". The real Stripe webhook will
        // verify the signature, look up the Payment by ProviderRef, then call the same ApplyPaid.
        app.MapPost("/dev/payments/{id:guid}/mark-paid", async (Guid id, AppDbContext db) =>
        {
            var p = await db.Payments.Include(x => x.Leaderboard).FirstOrDefaultAsync(x => x.Id == id);
            if (p?.Leaderboard is null) return Results.NotFound();
            if (p.Status == PaymentStatus.Paid) return Results.NoContent(); // webhooks can arrive twice
            BoardRules.ApplyPaid(p, p.Leaderboard, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // DEV ONLY. Run the expired-board cleanup now instead of waiting for the hourly timer.
        // ?asOf= lets you pretend it's a future date, to test the 30-day deletion.
        app.MapPost("/dev/cleanup-expired", async (DateTimeOffset? asOf, AppDbContext db) =>
            new { deleted = await ExpiredBoardCleanup.DeleteExpired(db, asOf ?? DateTimeOffset.UtcNow) });
    }
}
