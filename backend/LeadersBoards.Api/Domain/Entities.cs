namespace LeadersBoards.Api.Domain;

// Plain EF Core entity classes. Table/column names are snake_case in Postgres
// (configured in AppDbContext), PascalCase here.

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Leaderboard
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public LeaderboardStatus Status { get; set; } = LeaderboardStatus.Draft;
    /// <summary>Where the draft came from: "manual" or "ai_image" (later "ai_video", "scrape").</summary>
    public string Source { get; set; } = "manual";

    public LeaderboardKind Kind { get; set; } = LeaderboardKind.Ongoing;
    public Visibility Visibility { get; set; } = Visibility.Private;
    /// <summary>OneOff: when the comp happens. Scores close at EndsAt (admins can still correct).</summary>
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    public BoardPlan Plan { get; set; } = BoardPlan.Free;
    /// <summary>Max members (accepted + pending invites). Null = unlimited. Copied from the plan
    /// when it changes, so you can grandfather old boards or give someone a custom deal.</summary>
    public int? MemberLimit { get; set; }
    /// <summary>When the board (and all its scores) gets deleted automatically. Set on publish for
    /// public one-off boards; null = kept until the owner deletes it. See ExpiredBoardCleanup.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }

    public List<LeaderboardEvent> Events { get; set; } = [];
    public List<Membership> Memberships { get; set; } = [];
}

/// <summary>One workout / station on a leaderboard, e.g. "1km SkiErg" or "Fran".
/// Called LeaderboardEvent to avoid confusion with C# events; the table is "events".</summary>
public class LeaderboardEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LeaderboardId { get; set; }
    public Leaderboard Leaderboard { get; set; } = null!;
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ScoreType ScoreType { get; set; }
    public SortDirection SortDirection { get; set; }
    /// <summary>Optional cap for Time events. Athletes who don't finish log reps instead (IsCapped).</summary>
    public int? TimeCapSeconds { get; set; }
    public int Position { get; set; }
    /// <summary>Ongoing boards: the day this WOD belongs to (e.g. Monday's class). Null for one-offs.</summary>
    public DateOnly? ScheduledOn { get; set; }

    public List<Score> Scores { get; set; } = [];
}

/// <summary>A user's membership of a leaderboard. Doubles as the invite: Invited -> Accepted | Declined.</summary>
public class Membership
{
    public Guid LeaderboardId { get; set; }
    public Leaderboard Leaderboard { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public MemberRole Role { get; set; } = MemberRole.Member;
    public MembershipStatus Status { get; set; } = MembershipStatus.Invited;
    public Guid? InvitedById { get; set; }
    public DateTimeOffset InvitedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }

    public bool IsAdmin => Role is MemberRole.Admin or MemberRole.Owner;
}

/// <summary>One score per user per event. Re-submitting updates it.</summary>
public class Score
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EventId { get; set; }
    public LeaderboardEvent Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public decimal PrimaryValue { get; set; }
    public int? SecondaryValue { get; set; }
    /// <summary>Time events only: didn't finish under the cap. SecondaryValue = reps completed.</summary>
    public bool IsCapped { get; set; }
    public bool IsRx { get; set; } = true;
    public string? Notes { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Heart> Hearts { get; set; } = [];
}

/// <summary>"Seen by": first/last time a user viewed an event's (unlocked) results.</summary>
public class EventView
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Heart
{
    public Guid ScoreId { get; set; }
    public Score Score { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Comment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A payment for a board. Provider-agnostic: today only a dev endpoint marks it Paid;
/// later a Stripe webhook does the same thing (see PaymentRules.Apply).
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Nullable so the payment record survives when its board is deleted (accounting).</summary>
    public Guid? LeaderboardId { get; set; }
    public Leaderboard? Leaderboard { get; set; }
    public Guid UserId { get; set; }
    public PaymentKind Kind { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "EUR";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    /// <summary>"stripe" etc., and the provider's id (Checkout Session / PaymentIntent) for reconciling.</summary>
    public string? Provider { get; set; }
    public string? ProviderRef { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
}
