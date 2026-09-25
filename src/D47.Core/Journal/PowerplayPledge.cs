namespace D47.Core.Journal;

/// <summary>
/// Which Power the Commander flies for, if any, and how far up that Power's ladder they are (Phase 15,
/// "Warn that you are exposed in a rival Power's territory").
/// </summary>
public sealed record PowerplayPledge(string? Power, int Rank = 0)
{
    /// <summary>No Powerplay event read yet.</summary>
    public static readonly PowerplayPledge None = new((string?)null);

    /// <summary>Read from the journal as pledged to no Power.</summary>
    public static readonly PowerplayPledge Unpledged = new((string?)null) { IsKnown = true };

    public bool IsPledged => Power is { Length: > 0 };

    /// <summary>Whether a Powerplay event has said either way.</summary>
    public bool IsKnown { get; init; }

    /// <summary>The Commander's merit total with this Power; null until the journal reports it.</summary>
    public long? Merits { get; init; }

    /// <summary>Whether this system's controlling Power is somebody else's.</summary>
    public bool IsRival(string? controllingPower) =>
        IsPledged
        && controllingPower is { Length: > 0 }
        && !string.Equals(controllingPower, Power, StringComparison.OrdinalIgnoreCase);

    /// <summary>Folds one event.</summary>
    public PowerplayPledge Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "PowerplayLeave" => Unpledged,

        // Only the Powerplay snapshot carries a rank and merits. Joining and defecting do not, and both
        // start the ladder again under the new Power, so both go back to nothing until the next snapshot.
        "Powerplay" or "PowerplayJoin" or "PowerplayDefect" =>
            Named(journalEvent) is { } power
                ? new PowerplayPledge(power, journalEvent.Int("Rank") ?? 0)
                {
                    IsKnown = true,
                    Merits = journalEvent.Kind == "Powerplay" ? journalEvent.Long("Merits") : null,
                }
                : this,

        "PowerplayMerits" when IsPledged
            && string.Equals(journalEvent.String("Power"), Power, StringComparison.OrdinalIgnoreCase) =>
            journalEvent.Long("TotalMerits") is { } total ? this with { Merits = total } : this,

        "PowerplayRank" when IsPledged =>
            journalEvent.Int("Rank") is { } promoted ? this with { Rank = promoted } : this,

        _ => this,
    };

    /// <summary>Whose side the event puts the Commander on.</summary>
    private static string? Named(JournalEvent journalEvent) =>
        journalEvent.String("ToPower") is { Length: > 0 } defected
            ? defected
            : journalEvent.String("Power") is { Length: > 0 } pledged
                ? pledged
                : null;
}
