namespace D47.Core.Audio;

/// <summary>
/// A short non-speech marker played ahead of a warning, saying <em>which</em> warning it is before
/// the sentence has finished arriving (list.md Phase 15).
/// <para>
/// The phase asks for this in as many words: each id group gets "its own spoken line and its own
/// alert cue rather than one generic warning, because the game has already told us which situation
/// it is". A cue is the fastest thing d47 can put in a Commander's ear — the median warning here is
/// six to eight seconds ahead of the shooting, and the first second of that is spent on the word
/// "interdiction".
/// </para>
/// <para>
/// Named like <see cref="LoopState"/> and for the same reason: the cue file is named for the enum
/// member and <see cref="CueLibrary"/> asserts the shipped set matches this enum exactly. A hand
/// written member-to-filename table is a place for a typo to live, and a typo there goes wrong as
/// silence — the one failure mode a warning cannot afford, since a warning that did not fire and a
/// warning whose cue did not load sound identical.
/// </para>
/// </summary>
public enum AlertCue
{
    /// <summary>
    /// A pirate is lining up an interdiction. 88% of these were followed by an attack, at a median
    /// of six seconds — the strongest signal in the corpus.
    /// </summary>
    Interdiction,

    /// <summary>
    /// A pirate demanding cargo. Far commoner than the interdiction line and correspondingly
    /// weaker at 67%, which is why it does not share a cue with it: the Commander who learns the
    /// difference by ear can weigh them differently.
    /// </summary>
    Piracy,

    /// <summary>
    /// A bounty hunter, who is here for the Commander rather than for the hold. Its own cue
    /// because the answer is different — cargo will not buy this one off.
    /// </summary>
    BountyHunter,

    /// <summary>
    /// Flying in a rival Power's space. Not a thing that just happened but a condition entered,
    /// and the only cue here that is not about somebody shooting.
    /// </summary>
    RivalTerritory,
}
