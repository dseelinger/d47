namespace D47.Core.Vr;

/// <summary>
/// What one surface's last upload left behind, carried between frames.
/// </summary>
/// <param name="Pending">
/// A frame the compositor refused, still sitting in the buffer it was drawn into and waiting to
/// go again. The buffer is <em>not</em> rotated on a refusal, so retrying costs one more
/// <c>SetOverlayRaw</c> and no rasterise at all.
/// </param>
/// <param name="Reported">
/// Whether the current run of refusals has already been said once. A stalled compositor refuses
/// every frame, and the log is the only place any of this can be seen.
/// </param>
public readonly record struct FrameHeld(bool Pending, bool Reported);

/// <summary>What to do with one surface this frame.</summary>
public sealed record FramePlan
{
    /// <summary>Rasterise. Only ever for a surface that says something changed.</summary>
    public required bool Draw { get; init; }

    /// <summary>Hand the buffer to the runtime — a new frame, or a refused one going again.</summary>
    public required bool Submit { get; init; }
}

/// <summary>What the runtime's answer means: what to hold, what to say, and whether to move on.</summary>
public sealed record FrameOutcome
{
    public required FrameHeld Held { get; init; }

    /// <summary>Say it was refused. Once per run, not once per frame.</summary>
    public required bool Report { get; init; }

    /// <summary>Say frames are going through again, after a run that was reported.</summary>
    public required bool Recovered { get; init; }

    /// <summary>
    /// Move the ring on. Only ever for a frame the runtime actually took — see
    /// <c>VrPixels.InFlight</c> for why the ring exists, and <see cref="FrameHeld.Pending"/> for
    /// why a refused frame must stay exactly where it is.
    /// </summary>
    public required bool Rotate { get; init; }
}

/// <summary>
/// What happens to a frame the compositor turns down (remediation.md 16, item 1).
/// <para>
/// <b>It used to be nothing.</b> <c>Serve</c> submitted only when the surface said it was dirty,
/// <c>Draw</c> cleared that flag before the submit, and the submit's answer was discarded. So an
/// <c>EVROverlayError</c> from <c>SetOverlayRaw</c> lost that frame permanently: the panel in the
/// headset held whatever it last showed until something unrelated happened to mark the surface
/// dirty again, which for a panel nobody is touching can be minutes. The Commander sees a panel
/// that is quietly one state behind and no log line saying so.
/// </para>
/// <para>
/// <b>Redrawing is the wrong retry.</b> What the compositor refused is the upload, not the
/// picture — the pixels are already in the buffer and are still the ones wanted. So a refusal
/// holds the frame rather than dirtying the surface: the ring does not rotate, nothing is
/// rasterised again, and the next pass re-submits the same bytes. Re-rasterising an Avalonia
/// tree ten times a second to fix a call that costs microseconds would be paying the whole price
/// of the frame to retry the cheapest part of it.
/// </para>
/// <para>
/// <b>And only while the runtime is drawing us.</b> A refusal while the SteamVR dashboard is up,
/// or the headset is in standby or off the Commander's head, is the correct behaviour and not a
/// fault: the compositor is refusing frames for a quad nobody is looking at. Retrying through
/// that would re-send a panel-sized buffer at every tick for as long as the dashboard is open —
/// roughly nine megabytes a submit, copied inside OpenVR, for a picture that goes nowhere. The
/// frame is held instead, unsubmitted and undrawn, and goes the moment the quad is visible again.
/// </para>
/// <para>
/// Lifted out of <c>SteamVrRuntime.Serve</c> for the reason <see cref="RuntimeReadback"/> was:
/// this is a decision about three booleans, and leaving it threaded between an overlay submit and
/// a compositor call puts it in the one part of d47 no test can reach (architecture.md §8).
/// </para>
/// </summary>
public static class FrameDelivery
{
    /// <param name="held">What the last pass left behind for this surface.</param>
    /// <param name="isDirty">Whether the surface says anything has changed since its last draw.</param>
    /// <param name="isVisible">
    /// Whether SteamVR is drawing this quad at all. Asked of the runtime rather than tracked from
    /// its event stream: a single <c>OverlayShown</c> that never arrived would otherwise leave a
    /// held frame stuck for the session.
    /// </param>
    public static FramePlan Plan(FrameHeld held, bool isDirty, bool isVisible)
    {
        // A new frame supersedes a held one and goes whatever the visibility says. Visibility is
        // a throttle on retrying, never a gate on drawing: a surface that changed while the
        // dashboard was up must be current the moment the dashboard closes, and the only way to
        // be sure of that is to have drawn it.
        if (isDirty)
        {
            return new FramePlan { Draw = true, Submit = true };
        }

        return new FramePlan { Draw = false, Submit = held.Pending && isVisible };
    }

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

                // Only worth saying if the refusal was said. Otherwise every single frame of an
                // ordinary session would announce that it went through.
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
