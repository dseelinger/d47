namespace D47.Core.Reminders;

/// <summary>The journal moment a reminder waits for.</summary>
public enum JournalTrigger
{
    /// <summary>Any <c>Docked</c>.</summary>
    NextDocking,

    /// <summary>A <c>Docked</c> at the station the argument names.</summary>
    DockingAt,

    /// <summary>An <c>FSDJump</c>, <c>CarrierJump</c> or <c>Location</c> in the system the argument names.</summary>
    ArrivalIn,

    /// <summary>A <c>Docked</c> at the Commander's own carrier, not a squadron carrier.</summary>
    OwnCarrier,

    /// <summary>The ship's hold becoming empty.</summary>
    HoldEmpty,

    /// <summary>The ship's hold becoming full.</summary>
    HoldFull,

    /// <summary>The material the argument names reaching its capacity.</summary>
    MaterialFull,

    /// <summary>The next <c>LoadGame</c>.</summary>
    NextSession,
}

public enum JournalReminderState
{
    Armed,
    Fired,
}

/// <summary>Something to say once, in the Commander's own words, when the journal reaches a moment.</summary>
/// <param name="Sentence">What the Commander said, spoken verbatim and never given to a model.</param>
/// <param name="Argument">The station, system or material the trigger names, or null.</param>
public sealed record JournalReminder(string Id, string Sentence, JournalTrigger Trigger, string? Argument = null)
{
    public JournalReminderState State { get; init; } = JournalReminderState.Armed;

    /// <summary>When it fired; null while armed, and for a reminder fired before this was kept.</summary>
    public DateTimeOffset? FiredAt { get; init; }

    /// <summary>When it was set; a <see cref="JournalTrigger.NextSession"/> reminder fires only at a later <c>LoadGame</c>.</summary>
    public DateTimeOffset Set { get; init; }

    /// <summary>Whether the trigger needs an argument to match against.</summary>
    public static bool NeedsArgument(JournalTrigger trigger) =>
        trigger is JournalTrigger.DockingAt or JournalTrigger.ArrivalIn or JournalTrigger.MaterialFull;
}
