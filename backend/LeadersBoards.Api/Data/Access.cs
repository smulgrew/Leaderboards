using LeadersBoards.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeadersBoards.Api.Data;

/// <summary>Membership lookups shared by the endpoints. All permission checks go through here.</summary>
public static class Access
{
    /// <summary>The caller's accepted membership of a board, or null if they aren't a member.</summary>
    public static Task<Membership?> AcceptedMembership(this AppDbContext db, Guid leaderboardId, Guid userId) =>
        db.Memberships.Include(m => m.Leaderboard).FirstOrDefaultAsync(m =>
            m.LeaderboardId == leaderboardId && m.UserId == userId && m.Status == MembershipStatus.Accepted);

    /// <summary>People who count toward the member limit: accepted members plus pending invites.</summary>
    public static Task<int> MemberCount(this AppDbContext db, Guid leaderboardId) =>
        db.Memberships.CountAsync(m => m.LeaderboardId == leaderboardId && m.Status != MembershipStatus.Declined);

    /// <summary>Loads an event plus the caller's membership of its board, in one go.</summary>
    public static async Task<(LeaderboardEvent? Event, Membership? Membership)> EventForMember(
        this AppDbContext db, Guid eventId, Guid userId)
    {
        var ev = await db.Events.Include(e => e.Leaderboard).FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return (null, null);
        var m = await db.AcceptedMembership(ev.LeaderboardId, userId);
        // Members can only see published boards; admins can also see drafts they're editing.
        if (m is not null && ev.Leaderboard.Status == LeaderboardStatus.Draft && !m.IsAdmin) m = null;
        return (ev, m);
    }

    /// <summary>
    /// THE core rule: you can see an event's results (and comments) only if you're an
    /// admin/owner of the board, or you've already submitted your own score for it.
    /// </summary>
    public static async Task<bool> CanSeeResults(this AppDbContext db, LeaderboardEvent ev, Membership m) =>
        m.IsAdmin || await db.Scores.AnyAsync(s => s.EventId == ev.Id && s.UserId == m.UserId);
}
