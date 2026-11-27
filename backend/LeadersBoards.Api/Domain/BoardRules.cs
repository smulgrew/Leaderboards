namespace LeadersBoards.Api.Domain;

/// <summary>Kind / visibility / plan rules, in one place (like ScoreRules for scores).
///
/// Pricing, as decided by Charlie (Nov 2026):
///  • Public one-off  → pay once to publish; deleted 30 days after the event.
///  • Public ongoing  → pay once to publish; runs until deleted.
///  • Private         → free up to 30 people; pay once (PrivateUnlimited) to go beyond.
/// </summary>
public static class BoardRules
{
    /// <summary>Member limit for a brand-new board. Public boards are paid, so they're uncapped.</summary>
    public static int? InitialMemberLimit(Visibility v, PlanOptions o) =>
        v == Visibility.Public ? null : o.FreePrivateMemberLimit;

    /// <summary>Which payment a public board needs before it can go live (null = none, publish is free).</summary>
    public static PaymentKind? RequiredPaymentToPublish(Leaderboard b) =>
        b.Visibility != Visibility.Public ? null
        : b.Kind == LeaderboardKind.OneOff ? (b.Plan == BoardPlan.PublicOneOff ? null : PaymentKind.PublicOneOff)
        : (b.Plan == BoardPlan.PublicOngoing ? null : PaymentKind.PublicOngoing);

    public static bool NeedsPaymentToPublish(Leaderboard b) => RequiredPaymentToPublish(b) is not null;

    /// <summary>Returns an error if this payment kind doesn't fit this board, else null.</summary>
    public static string? CheckPaymentFits(PaymentKind kind, Leaderboard b) => kind switch
    {
        PaymentKind.PublicOneOff when b.Visibility != Visibility.Public || b.Kind != LeaderboardKind.OneOff
            => "That plan is for public one-off events.",
        PaymentKind.PublicOngoing when b.Visibility != Visibility.Public || b.Kind != LeaderboardKind.Ongoing
            => "That plan is for public ongoing boards.",
        PaymentKind.PrivateUnlimited when b.Visibility != Visibility.Private
            => "That plan is for private boards.",
        _ when b.Plan.ToString() == kind.ToString() => "Already paid.",
        _ => null,
    };

    public static bool IsFull(Leaderboard b, int currentMembers) =>
        b.MemberLimit is int limit && currentMembers >= limit;

    /// <summary>Non-admins can't post after a one-off has ended.</summary>
    public static bool ScoresClosed(Leaderboard b, DateTimeOffset now) =>
        b.EndsAt is DateTimeOffset end && now > end;

    public static (int AmountCents, string Currency) Price(PaymentKind kind, PlanOptions o) => kind switch
    {
        PaymentKind.PublicOneOff => (o.PublicOneOffPriceCents, o.Currency),
        PaymentKind.PublicOngoing => (o.PublicOngoingPriceCents, o.Currency),
        _ => (o.PrivateUnlimitedPriceCents, o.Currency),
    };

    /// <summary>
    /// What a successful payment unlocks. Called by the dev "mark paid" endpoint today and by
    /// the Stripe webhook later, so the upgrade logic exists exactly once.
    /// </summary>
    public static void ApplyPaid(Payment p, Leaderboard b, DateTimeOffset now)
    {
        p.Status = PaymentStatus.Paid;
        p.PaidAt = now;
        b.Plan = p.Kind switch
        {
            PaymentKind.PublicOneOff => BoardPlan.PublicOneOff,
            PaymentKind.PublicOngoing => BoardPlan.PublicOngoing,
            _ => BoardPlan.PrivateUnlimited,
        };
        b.MemberLimit = null; // every paid plan is uncapped
    }

    /// <summary>Public one-offs get a deletion date when published: RetentionDays after the event
    /// starts (or after publishing, if the event date is already in the past).</summary>
    public static DateTimeOffset? ExpiryOnPublish(Leaderboard b, PlanOptions o, DateTimeOffset now) =>
        b.Visibility == Visibility.Public && b.Kind == LeaderboardKind.OneOff
            ? (b.StartsAt > now ? b.StartsAt.Value : now).AddDays(o.OneOffRetentionDays)
            : null;
}
