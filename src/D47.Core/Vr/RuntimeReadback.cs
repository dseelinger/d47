namespace D47.Core.Vr;

/// <summary>
/// The last thing the log said about one surface, and when it said it.
/// </summary>
/// <param name="Text">
/// The runtime's own account, verbatim. Compared rather than parsed: what matters is whether it
/// is the same sentence as last time, and any structure imposed on it here would be a second
/// thing to keep in step with <c>VrOverlay.Describe</c>.
/// </param>
public readonly record struct SurfaceReport(string Text, DateTimeOffset When);

/// <summary>Whether to write the read-back, and what to hold once the decision is acted on.</summary>
public sealed record ReadbackPlan
{
    public required bool Write { get; init; }

    /// <summary>
    /// The state to keep. Unchanged from what was held when <see cref="Write"/> is false, which is
    /// the property the whole throttle rests on — see <see cref="RuntimeReadback"/>.
    /// </summary>
    public required SurfaceReport Held { get; init; }
}

/// <summary>
/// How often SteamVR's own account of an overlay reaches the log (list.md Phase 9).
/// <para>
/// The read-back exists because going blind is what made the Phase 12 fault cost a day: a quad
/// can be placed correctly, reported visible, and still be invisible, and three theories
/// reasoning from what d47 <em>sent</em> were all wrong. What settles it is what the runtime says
/// it is holding. So the read-back stays.
/// </para>
/// <para>
/// What cannot stay is writing it every frame. <c>Serve</c> runs at 10 Hz against three surfaces,
/// so an unthrottled read-back is thirty lines a second — it wrote seven hundred identical lines
/// an hour at the old five-second interval and buried everything else in the file. <b>The
/// interesting moment is the answer changing</b> — visible going false, a width the Commander did
/// not set, a transform that stopped tracking — and that is exactly what a wall of identical
/// lines hides.
/// </para>
/// <para>
/// So: on change, floored so that a value which merely jitters cannot write ten times a second;
/// otherwise on a slow heartbeat that proves the session is still being served. Lifted out of
/// <c>SteamVrRuntime.Serve</c>, where it was five lines threaded between an overlay submit and a
/// compositor call and could not be reached without a headset — while being pure arithmetic over
/// a string, two timestamps and two constants (list.md Phase 19, "Give the composition root a
/// test harness").
/// </para>
/// </summary>
public static class RuntimeReadback
{
    /// <summary>
    /// The floor between two writes for one surface, changed or not. <c>Serve</c> runs at 10 Hz,
    /// so a value that merely jittered would write ten lines a second — which is the fault being
    /// fixed here, only faster.
    /// </summary>
    public static readonly TimeSpan AtMost = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the runtime is asked to describe itself when nothing about it has changed. The
    /// heartbeat that says the session is still being served, and nothing more — five minutes,
    /// because its only reader is somebody scrolling back through a log asking whether d47 was
    /// still talking to SteamVR at the time.
    /// </summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    /// <param name="held">
    /// What was last written for this surface, or null before anything has been. Null is not the
    /// same as "written long ago": the first sighting is always worth one line, and that is the
    /// line saying the surface exists at all.
    /// </param>
    /// <param name="described">The runtime's account right now.</param>
    public static ReadbackPlan Plan(SurfaceReport? held, string described, DateTimeOffset now)
    {
        if (held is not { } last)
        {
            return Writing(described, now);
        }

        // Ordinal, because this is one machine's own string compared against itself a tenth of a
        // second later. A culture-aware comparison here would be a slower way to get the same
        // answer, and a wrong one the day a description carries a number.
        var changed = !string.Equals(last.Text, described, StringComparison.Ordinal);

        // The floor is the whole design in one line: a change is news and waits a second, and
        // sameness is a heartbeat and waits five minutes.
        return now - last.When >= (changed ? AtMost : Every)
            ? Writing(described, now)
            : new ReadbackPlan { Write = false, Held = last };
    }

    /// <summary>
    /// <b>The held state advances only when something is written</b>, which is what makes a
    /// change survive being suppressed. A description that changes half a second after the last
    /// line is held back — and because the old text is still what is held, the next call still
    /// sees a change and writes it as soon as the floor passes. Advancing the text without
    /// writing would swallow that change permanently, which is the one outcome a read-back must
    /// not produce.
    /// <para>
    /// The same property is what makes a blip that reverts within the floor cost nothing: by the
    /// time the floor passes the text matches what was last written, so there is no change left
    /// to report and the heartbeat takes over.
    /// </para>
    /// </summary>
    private static ReadbackPlan Writing(string described, DateTimeOffset now) =>
        new() { Write = true, Held = new SurfaceReport(described, now) };
}
