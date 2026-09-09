namespace D47.Core.Journal;

/// <summary>
/// Which Power the Commander flies for, if any (Phase 15, "Warn that you are exposed in a rival Power's
/// territory").
/// </summary>
public sealed record PowerplayPledge(string? Power)
{
    public static readonly PowerplayPledge None = new((string?)null);

    public bool IsPledged => Power is { Length: > 0 };

    /// <summary>Whether this system's controlling Power is somebody else's.</summary>
    public bool IsRival(string? controllingPower) =>
        IsPledged
        && controllingPower is { Length: > 0 }
        && !string.Equals(controllingPower, Power, StringComparison.OrdinalIgnoreCase);

    /// <summary>Folds one event.</summary>
    public PowerplayPledge Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "PowerplayLeave" => None,

        "Powerplay" or "PowerplayJoin" or "PowerplayDefect" =>
            Named(journalEvent) is { } power ? new PowerplayPledge(power) : this,

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
