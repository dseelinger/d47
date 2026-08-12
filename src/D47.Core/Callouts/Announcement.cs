using D47.Core.Audio;

namespace D47.Core.Callouts;

/// <summary>How much a callout outranks whatever else is being said.</summary>
public enum CalloutUrgency
{
    /// <summary>
    /// Worth saying, not worth interrupting for. Route progress, arrivals, milestones. Queued
    /// behind whatever is currently being spoken.
    /// </summary>
    Routine,

    /// <summary>
    /// Said now, over the top of anything else. Danger and fuel. An alert that waits for the
    /// current sentence to finish is not an alert, which is why <see cref="AudioChannel.Alert"/>
    /// exists above <see cref="AudioChannel.Speech"/>.
    /// </summary>
    Urgent,
}

/// <summary>
/// Something d47 has decided to say without being asked (list.md Phase 8).
/// <para>
/// A callout emits one of these from the tick loop, which is synchronous and must not block —
/// so this is a description of what to say, not the act of saying it. The app drains these onto
/// the thread pool and into the voice pipeline. That split is what keeps a slow TTS synthesis
/// from stalling push-to-talk.
/// </para>
/// </summary>
/// <param name="Key">
/// Identity for cooldown purposes: two announcements sharing a key are the same warning said
/// twice. "fuel.low", "danger.shields", "route.progress" — coarse enough that a repeat is
/// suppressed, specific enough that a different warning is not.
/// </param>
/// <param name="Text">What to say. Complete sentences: this goes to a voice, not a status bar.</param>
public sealed record Announcement(string Key, string Text, CalloutUrgency Urgency = CalloutUrgency.Routine)
{
    /// <summary>
    /// How long this key stays suppressed after being said. Zero means the callout manages its
    /// own repetition — the material milestones do, since each threshold fires once by
    /// construction and a time-based cooldown would swallow a legitimate second one.
    /// </summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.Zero;

    public AudioChannel Channel =>
        Urgency == CalloutUrgency.Urgent ? AudioChannel.Alert : AudioChannel.Speech;
}
