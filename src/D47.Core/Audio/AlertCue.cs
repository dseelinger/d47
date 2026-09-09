namespace D47.Core.Audio;

/// <summary>
/// A short non-speech marker played ahead of a warning, saying which warning it is before the sentence
/// has finished arriving (Phase 15).
/// </summary>
public enum AlertCue
{
    /// <summary>
    /// A pirate is lining up an interdiction. 88% of these were followed by an attack, at a median of
    /// six seconds — the strongest signal in the corpus.
    /// </summary>
    Interdiction,

    /// <summary>A pirate demanding cargo.</summary>
    Piracy,

    /// <summary>A bounty hunter, who is here for the Commander rather than for the hold.</summary>
    BountyHunter,

    /// <summary>
    /// Somebody is hurting the ship right now — shot, shields gone, hull opened, or pulled out of
    /// supercruise into it (#136).
    /// </summary>
    UnderFire,

    /// <summary>The ship is cooking itself (#136).</summary>
    Overheating,

    /// <summary>Flying in a rival Power's space.</summary>
    RivalTerritory,

    /// <summary>A timer or an alarm has gone off (Phase 24, "A timer says its own name").</summary>
    TimerElapsed,
}

/// <summary>What each cue is called when it is written down rather than heard (#201).</summary>
public static class AlertCues
{
    /// <summary>The bracketed line for one cue.</summary>
    public static string Caption(AlertCue cue) => cue switch
    {
        AlertCue.Interdiction => "[interdiction alert]",
        AlertCue.Piracy => "[pirate alert]",
        AlertCue.BountyHunter => "[bounty hunter alert]",
        AlertCue.UnderFire => "[attack alarm]",
        AlertCue.Overheating => "[heat alarm]",
        AlertCue.RivalTerritory => "[territory alert]",
        AlertCue.TimerElapsed => "[timer chime]",
        _ => "[alert]",
    };
}
