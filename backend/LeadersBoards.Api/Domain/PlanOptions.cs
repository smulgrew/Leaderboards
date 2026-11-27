namespace LeadersBoards.Api.Domain;

/// <summary>Bound from appsettings.json "Plans". Prices are placeholders; change them without touching code.</summary>
public class PlanOptions
{
    /// <summary>Private boards are free up to this many people (accepted members + pending invites).</summary>
    public int FreePrivateMemberLimit { get; set; } = 30;

    public int PublicOneOffPriceCents { get; set; } = 2000;
    public int PublicOngoingPriceCents { get; set; } = 5000;
    public int PrivateUnlimitedPriceCents { get; set; } = 1000;

    /// <summary>Public one-off boards are deleted this many days after the event starts.</summary>
    public int OneOffRetentionDays { get; set; } = 30;

    public string Currency { get; set; } = "EUR";
}
