namespace D47.Core.Journal;

/// <summary>
/// Which Power the Commander flies for, if any (Phase 15, "Warn that you are exposed in a
/// rival Power's territory").
/// <para>
/// <b>Tracked as history rather than read once</b>, which is the item's own wording and is the
/// difference between a warning that is right and one that is confidently wrong. A pledge changes:
/// the journal corpus behind this has a Commander leaving Pranav Antal in January 2026, and a
/// pledge captured at startup would have gone on calling their new Power's space hostile.
/// </para>
/// <para>
/// Only the Power is kept. Rank, merits and time pledged all arrive on the same events and none of
/// them changes any answer d47 gives, so they are not folded — an unread field is a field that
/// goes wrong quietly.
/// </para>
/// </summary>
public sealed record PowerplayPledge(string? Power)
{
    public static readonly PowerplayPledge None = new((string?)null);

    public bool IsPledged => Power is { Length: > 0 };

    /// <summary>
    /// Whether this system's controlling Power is somebody else's.
    /// <para>
    /// Both halves have to be known. An unpledged Commander is not exposed anywhere, and an
    /// unoccupied system has no <c>ControllingPower</c> at all — the field is absent rather than
    /// null, in 1,932 of the 4,891 jumps measured, so treating a missing value as "not mine" would
    /// warn about deep space.
    /// </para>
    /// </summary>
    public bool IsRival(string? controllingPower) =>
        IsPledged
        && controllingPower is { Length: > 0 }
        && !string.Equals(controllingPower, Power, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Folds one event. <c>Powerplay</c> is the session snapshot Elite writes on entry; the other
    /// three are the transitions, and without them the snapshot is only ever as fresh as the last
    /// time the Commander started the game.
    /// </summary>
    public PowerplayPledge Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "PowerplayLeave" => None,

        "Powerplay" or "PowerplayJoin" or "PowerplayDefect" =>
            Named(journalEvent) is { } power ? new PowerplayPledge(power) : this,

        _ => this,
    };

    /// <summary>
    /// Whose side the event puts the Commander on. Defecting names both ends and
    /// <c>ToPower</c> is the one that matters; the other three name it <c>Power</c>.
    /// <c>PowerplayDefect</c> does not appear in the corpus at all, so its field name is taken
    /// from Frontier's schema rather than from something counted — which is why <c>Power</c> is
    /// still accepted underneath it rather than assumed absent.
    /// </summary>
    private static string? Named(JournalEvent journalEvent) =>
        journalEvent.String("ToPower") is { Length: > 0 } defected
            ? defected
            : journalEvent.String("Power") is { Length: > 0 } pledged
                ? pledged
                : null;
}
