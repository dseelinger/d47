namespace D47.Core.Vr;

/// <summary>What one surface's last upload left behind, carried between frames.</summary>
/// <param name="Pending">
/// A frame the compositor refused, still sitting in the buffer it was drawn into and waiting to go
/// again.
/// </param>
/// <param name="Reported">Whether the current run of refusals has already been said once.</param>
public readonly record struct FrameHeld(bool Pending, bool Reported);

/// <summary>What to do with one surface this frame.</summary>
public sealed record FramePlan
{
    /// <summary>Rasterise.</summary>
    public required bool Draw { get; init; }

    /// <summary>Hand the buffer to the runtime — a new frame, or a refused one going again.</summary>
    public required bool Submit { get; init; }
}

/// <summary>What the runtime's answer means: what to hold, what to say, and whether to move on.</summary>
public sealed record FrameOutcome
{
    public required FrameHeld Held { get; init; }

    /// <summary>Say it was refused.</summary>
    public required bool Report { get; init; }

    /// <summary>Say frames are going through again, after a run that was reported.</summary>
    public required bool Recovered { get; init; }

    /// <summary>Move the ring on.</summary>
    public required bool Rotate { get; init; }
}

/// <summary>What happens to a frame the compositor turns down (remediation.md 16, item 1).</summary>
public static class FrameDelivery
{
    /// <summary>
    /// <param name="held">What the last pass left behind for this surface.</param> <param
    /// name="isDirty">Whether the surface says anything has changed since its last draw.</param> <param
    /// name="isVisible"> Whether SteamVR is drawing this quad at all.
    /// </summary>
    /// <param name="held">What the last pass left behind for this surface.</param>
    /// <param name="isDirty">
    /// Whether the surface says anything has changed since its last draw.
    /// </param>
    /// <param name="isVisible">Whether SteamVR is drawing this quad at all.</param>
    public static FramePlan Plan(FrameHeld held, bool isDirty, bool isVisible)
    {
        // A new frame supersedes a held one and goes whatever the visibility says.
        if (isDirty)
        {
            return new FramePlan { Draw = true, Submit = true };
        }

        return new FramePlan { Draw = false, Submit = held.Pending && isVisible };
    }

    /// <summary>
    /// <param name="held">What was held going in — which is what decides whether this is news.</param>
    /// <param name="accepted">Whether the runtime took the frame.</param>
    /// </summary>
    /// <param name="held">What was held going in — which is what decides whether this is news.</param>
    /// <param name="accepted">Whether the runtime took the frame.</param>
    public static FrameOutcome Took(FrameHeld held, bool accepted)
    {
        if (accepted)
        {
            return new FrameOutcome
            {
                Held = default,
                Report = false,

                // Only worth saying if the refusal was said.
                Recovered = held.Reported,
                Rotate = true,
            };
        }

        return new FrameOutcome
        {
            Held = new FrameHeld(Pending: true, Reported: true),
            Report = !held.Reported,
            Recovered = false,
            Rotate = false,
        };
    }
}
