namespace LeadersBoards.Api.Domain;

// Enums are stored as text in Postgres (see AppDbContext) so the data stays readable
// in psql / Supabase table views, and adding a new value never needs a DB enum migration.

/// <summary>What kind of number a score is. Drives default sort direction and display format.</summary>
public enum ScoreType
{
    Time,        // PrimaryValue = seconds (e.g. 754.2 = 12:34.2). Lower is better.
    Reps,        // PrimaryValue = rep count. Higher is better.
    Weight,      // PrimaryValue = kg. Higher is better.
    RoundsReps,  // PrimaryValue = full rounds, SecondaryValue = extra reps (AMRAP "5+12"). Higher is better.
    Distance,    // PrimaryValue = metres. Higher is better.
    Calories     // PrimaryValue = calories. Higher is better.
}

public enum SortDirection { Ascending, Descending }

public enum LeaderboardStatus { Draft, Published, Archived }

public enum MemberRole { Member, Admin, Owner }

public enum MembershipStatus { Invited, Accepted, Declined }

/// <summary>OneOff = a dated comp (half marathon, 1-day throwdown). Ongoing = never ends (daily class WODs).</summary>
public enum LeaderboardKind { OneOff, Ongoing }

/// <summary>Public = anyone can find it and tap Join. Private = admins invite people.</summary>
public enum Visibility { Private, Public }

/// <summary>What the board has paid for. Prices and limits per plan come from appsettings "Plans".
///  Free             – private boards, up to FreePrivateMemberLimit (30) people.
///  PublicOneOff     – paid; public one-off comp, deleted RetentionDays (30) after the event.
///  PublicOngoing    – paid; public board that runs until its owner deletes it.
///  PrivateUnlimited – paid; a private board that has outgrown the free limit.</summary>
public enum BoardPlan { Free, PublicOneOff, PublicOngoing, PrivateUnlimited }

/// <summary>What a payment buys. Each one moves the board to the BoardPlan of the same name.</summary>
public enum PaymentKind { PublicOneOff, PublicOngoing, PrivateUnlimited }

public enum PaymentStatus { Pending, Paid, Failed, Refunded }
