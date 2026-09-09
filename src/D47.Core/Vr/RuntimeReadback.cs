namespace D47.Core.Vr;

/// <summary>The last thing the log said about one surface, and when it said it.</summary>
/// <param name="Text">The runtime's own account, verbatim.</param>
public readonly record struct SurfaceReport(string Text, DateTimeOffset When);

/// <summary>Whether to write the read-back, and what to hold once the decision is acted on.</summary>
public sealed record ReadbackPlan
{
    public required bool Write { get; init; }

    /// <summary>The state to keep.</summary>
    public required SurfaceReport Held { get; init; }
}

/// <summary>How often SteamVR's own account of an overlay reaches the log (Phase 9).</summary>
public static class RuntimeReadback
{
    /// <summary>The floor between two writes for one surface, changed or not.</summary>
    public static readonly TimeSpan AtMost = TimeSpan.FromSeconds(1);

    /// <summary>How often the runtime is asked to describe itself when nothing about it has changed.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    /// <summary>
    /// <param name="held"> What was last written for this surface, or null before anything has been.
    /// </summary>
    /// <param name="held">
    /// What was last written for this surface, or null before anything has been.
    /// </param>
    /// <param name="described">The runtime's account right now.</param>
    public static ReadbackPlan Plan(SurfaceReport? held, string described, DateTimeOffset now)
    {
        if (held is not { } last)
        {
            return Writing(described, now);
        }

        // Ordinal, because this is one machine's own string compared against itself a tenth of a second
        // later.
        var changed = !string.Equals(last.Text, described, StringComparison.Ordinal);

        // The floor is the whole design in one line: a change is news and waits a second, and sameness is a
        // heartbeat and waits five minutes.
        return now - last.When >= (changed ? AtMost : Every)
            ? Writing(described, now)
            : new ReadbackPlan { Write = false, Held = last };
    }

    /// <summary>
    /// The held state advances only when something is written, which is what makes a change survive
    /// being suppressed.
    /// </summary>
    private static ReadbackPlan Writing(string described, DateTimeOffset now) =>
        new() { Write = true, Held = new SurfaceReport(described, now) };
}
