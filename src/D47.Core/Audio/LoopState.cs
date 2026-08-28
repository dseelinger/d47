namespace D47.Core.Audio;

/// <summary>
/// Where the conversation loop is. Every state has exactly one shipped cue, and the cue file
/// is named for the state — `assets/cues/thinking.wav` is the cue for
/// <see cref="Thinking"/> and nothing else can be (Phase 5, #20).
/// <para>
/// The naming is the whole point. A hand-written state → filename table is a place for a
/// typo to live, and a typo there goes wrong as silence: the cue simply never plays and
/// nobody notices for weeks. Deriving the name means <see cref="CueLibrary"/> can assert the
/// shipped set matches this enum exactly, so the failure arrives at startup instead.
/// </para>
/// </summary>
public enum LoopState
{
    /// <summary>Nothing in flight. The state the loop returns to after every turn.</summary>
    Idle,

    /// <summary>The microphone is open. Arrives with push-to-talk in Phase 6.</summary>
    Listening,

    /// <summary>Captured audio is being turned into text. Phase 6.</summary>
    Transcribing,

    /// <summary>A turn is running — the model, a tool, or the keyword router.</summary>
    Thinking,

    /// <summary>An answer is being spoken.</summary>
    Speaking,

    /// <summary>The turn produced an answer.</summary>
    Answered,

    /// <summary>
    /// The turn produced an explicit "unsure". A state of the loop rather than an error,
    /// which is why it gets its own cue rather than borrowing <see cref="Failed"/>'s
    /// (Phase 3, "Ship's AI Unsure").
    /// </summary>
    Unsure,

    /// <summary>The turn could not be completed.</summary>
    Failed,
}
