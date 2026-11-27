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

/// <summary>What the board has paid for. Limits per plan come from appsettings "Plans".</summary>
public enum BoardPlan { Free, OneOffEvent, Pro }

public enum PaymentKind { OneOffEvent, ProMonthly }

public enum PaymentStatus { Pending, Paid, Failed, Refunded }
