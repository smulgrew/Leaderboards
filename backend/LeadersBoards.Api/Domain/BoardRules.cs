namespace LeadersBoards.Api.Domain;

/// <summary>Kind / visibility / plan rules, in one place (like ScoreRules for scores).</summary>
public static class BoardRules
{
    /// <summary>Member limit for a brand-new (Free) board.</summary>
    public static int? InitialMemberLimit(Visibility v, PlanOptions o) =>
        v == Visibility.Public ? o.FreePublicMemberLimit : o.FreePrivateMemberLimit;

    /// <summary>One-off comps must be paid for before they go live. Drafting and editing are free.</summary>
    public static bool NeedsPaymentToPublish(Leaderboard b) =>
        b.Kind == LeaderboardKind.OneOff && b.Plan != BoardPlan.OneOffEvent;

    public static bool IsFull(Leaderboard b, int currentMembers) =>
        b.MemberLimit is int limit && currentMembers >= limit;

    /// <summary>Non-admins can't post after a one-off has ended.</summary>
    public static bool ScoresClosed(Leaderboard b, DateTimeOffset now) =>
        b.EndsAt is DateTimeOffset end && now > end;

    public static (int AmountCents, string Currency) Price(PaymentKind kind, PlanOptions o) => kind switch
    {
        PaymentKind.OneOffEvent => (o.OneOffEventPriceCents, o.Currency),
        _ => (o.ProMonthlyPriceCents, o.Currency),
    };

    /// <summary>
    /// What a successful payment unlocks. Called by the dev "mark paid" endpoint today and by
    /// the Stripe webhook later, so the upgrade logic exists exactly once.
    /// </summary>
    public static void ApplyPaid(Payment p, Leaderboard b, PlanOptions o, DateTimeOffset now)
    {
        p.Status = PaymentStatus.Paid;
        p.PaidAt = now;
        switch (p.Kind)
        {
            case PaymentKind.OneOffEvent:
                b.Plan = BoardPlan.OneOffEvent;
                b.MemberLimit = o.OneOffEventMemberLimit;
                break;
            case PaymentKind.ProMonthly:
                b.Plan = BoardPlan.Pro;
                b.MemberLimit = null;
                b.PlanExpiresAt = (b.PlanExpiresAt > now ? b.PlanExpiresAt.Value : now).AddMonths(1);
                break;
        }
    }
}
