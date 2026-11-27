using LeadersBoards.Api.Domain;

namespace LeadersBoards.Api.Contracts;

// Request/response shapes. Kept separate from entities so the API contract
// doesn't change every time the database does.

public record DevLoginRequest(string Email, string DisplayName);
public record UserDto(Guid Id, string Email, string DisplayName);

public record CreateLeaderboardRequest(string Name, string? Description, List<CreateEventRequest>? Events,
    LeaderboardKind Kind = LeaderboardKind.Ongoing, Visibility Visibility = Visibility.Private,
    DateTimeOffset? StartsAt = null, DateTimeOffset? EndsAt = null);
public record CreateEventRequest(string Name, string? Description, ScoreType ScoreType,
    SortDirection? SortDirection, int? TimeCapSeconds, DateOnly? ScheduledOn = null);

public record LeaderboardSummaryDto(Guid Id, string Name, LeaderboardKind Kind, Visibility Visibility,
    LeaderboardStatus Status, MemberRole MyRole, int EventCount, DateTimeOffset? StartsAt);
public record LeaderboardDto(Guid Id, string Name, string? Description, LeaderboardKind Kind, Visibility Visibility,
    LeaderboardStatus Status, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? ExpiresAt,
    BoardPlan Plan, int? MemberLimit, int MemberCount, PaymentKind? RequiredPaymentToPublish,
    MemberRole MyRole, List<EventDto> Events);
public record EventDto(Guid Id, string Name, string? Description, ScoreType ScoreType,
    SortDirection SortDirection, int? TimeCapSeconds, int Position, DateOnly? ScheduledOn, bool ISubmitted);

/// <summary>A public board in search results. AmMember lets the app show "Open" instead of "Join".</summary>
public record PublicLeaderboardDto(Guid Id, string Name, string? Description, LeaderboardKind Kind,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, int MemberCount, bool IsFull, bool AmMember);

public record CheckoutRequest(PaymentKind Kind);
/// <summary>CheckoutUrl will be a Stripe Checkout link once a provider is wired up.</summary>
public record CheckoutDto(Guid PaymentId, int AmountCents, string Currency, string? CheckoutUrl);

public record InviteRequest(string Email, MemberRole Role = MemberRole.Member);
public record InviteDto(Guid LeaderboardId, string LeaderboardName, string InvitedBy, DateTimeOffset InvitedAt);

public record SubmitScoreRequest(decimal PrimaryValue, int? SecondaryValue, bool IsCapped = false,
    bool IsRx = true, string? Notes = null);

/// <summary>
/// The results response. When Locked is true, Entries is empty: the caller hasn't
/// submitted yet and isn't an admin, so they only learn how many people have posted.
/// </summary>
public record ResultsDto(Guid EventId, string EventName, ScoreType ScoreType, bool Locked,
    int EntryCount, List<ResultEntryDto> Entries);
public record ResultEntryDto(int Rank, Guid ScoreId, Guid UserId, string DisplayName, string Display,
    decimal PrimaryValue, int? SecondaryValue, bool IsCapped, bool IsRx, int Hearts, bool HeartedByMe,
    DateTimeOffset SubmittedAt);

public record SeenByDto(Guid UserId, string DisplayName, DateTimeOffset FirstSeenAt);

public record CreateCommentRequest(string Body);
public record CommentDto(Guid Id, Guid UserId, string DisplayName, string Body, DateTimeOffset CreatedAt);
