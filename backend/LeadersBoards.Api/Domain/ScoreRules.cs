namespace LeadersBoards.Api.Domain;

/// <summary>
/// All the "how do different score types behave" logic lives here, so the rest of the
/// app never has to care whether a score is a time, a weight or an AMRAP.
/// </summary>
public static class ScoreRules
{
    public static SortDirection DefaultSortFor(ScoreType type) =>
        type == ScoreType.Time ? SortDirection.Ascending : SortDirection.Descending;

    /// <summary>Returns an error message, or null if the score is valid for this event.</summary>
    public static string? Validate(LeaderboardEvent ev, decimal primary, int? secondary, bool isCapped)
    {
        if (primary < 0 || secondary < 0) return "Scores can't be negative.";
        if (isCapped && ev.ScoreType != ScoreType.Time) return "Only Time events can be capped.";
        if (isCapped && secondary is null) return "A capped score needs the reps completed (secondaryValue).";
        if (ev.ScoreType == ScoreType.RoundsReps && primary != decimal.Truncate(primary))
            return "Rounds must be a whole number; put extra reps in secondaryValue.";
        if (ev.ScoreType == ScoreType.Time && !isCapped && ev.TimeCapSeconds is int cap && primary > cap)
            return "Time is over the cap; submit it as capped with reps completed.";
        return null;
    }

    /// <summary>
    /// Sorts scores best-first for an event and assigns competition ranks (1, 2, 2, 4).
    /// Time events: finishers (by time) always beat capped athletes (by reps, more is better).
    /// </summary>
    public static List<(Score Score, int Rank)> Rank(LeaderboardEvent ev, IEnumerable<Score> scores)
    {
        // A sort key tuple: (group, primary, secondary). Lower group sorts first.
        // We flip signs for "higher is better" so we can always sort ascending.
        (int, decimal, decimal) Key(Score s)
        {
            if (s.IsCapped) return (1, -(s.SecondaryValue ?? 0), 0);
            var sign = ev.SortDirection == SortDirection.Ascending ? 1 : -1;
            return (0, sign * s.PrimaryValue, sign * (s.SecondaryValue ?? 0));
        }

        var sorted = scores.OrderBy(Key).ThenBy(s => s.SubmittedAt).ToList();
        var result = new List<(Score, int)>(sorted.Count);
        for (var i = 0; i < sorted.Count; i++)
        {
            var rank = i > 0 && Key(sorted[i]) == Key(sorted[i - 1]) ? result[i - 1].Item2 : i + 1;
            result.Add((sorted[i], rank));
        }
        return result;
    }

    /// <summary>Human-friendly display string, e.g. "12:34.2", "5 + 12", "102.5 kg", "CAP + 87 reps".</summary>
    public static string Format(ScoreType type, Score s)
    {
        if (s.IsCapped) return $"CAP + {s.SecondaryValue} reps";
        return type switch
        {
            ScoreType.Time => FormatTime(s.PrimaryValue),
            ScoreType.RoundsReps => $"{s.PrimaryValue:0} + {s.SecondaryValue ?? 0}",
            ScoreType.Weight => $"{s.PrimaryValue:0.##} kg",
            ScoreType.Distance => $"{s.PrimaryValue:0.##} m",
            ScoreType.Calories => $"{s.PrimaryValue:0} cal",
            _ => $"{s.PrimaryValue:0}"
        };
    }

    private static string FormatTime(decimal seconds)
    {
        var ts = TimeSpan.FromSeconds((double)seconds);
        var frac = ts.Milliseconds > 0 ? $".{ts.Milliseconds / 100}" : "";
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}{frac}"
            : $"{ts.Minutes}:{ts.Seconds:00}{frac}";
    }
}
