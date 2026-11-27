namespace LeadersBoards.Api.Domain;

/// <summary>Bound from appsettings.json "Plans". Change prices and limits without a deploy of code.</summary>
public class PlanOptions
{
    /// <summary>Free private boards: max members (accepted + pending invites) before upgrading.</summary>
    public int FreePrivateMemberLimit { get; set; } = 15;
    /// <summary>Free public boards. Null = unlimited.</summary>
    public int? FreePublicMemberLimit { get; set; }
    public int OneOffEventPriceCents { get; set; } = 2000;
    public int? OneOffEventMemberLimit { get; set; } = 500;
    public int ProMonthlyPriceCents { get; set; } = 900;
    public string Currency { get; set; } = "EUR";
}
