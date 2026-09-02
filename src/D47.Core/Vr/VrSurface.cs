using System.Numerics;

namespace D47.Core.Vr;

/// <summary>
/// Which surface a placement belongs to. Two overlay handles, and the panel's two content
/// modes each carry their own placement (architecture.md D2, as amended in Phase 9).
/// <para>
/// Full and Mini are not the same quad at two scales. Apparent text size in a headset is the
/// texture's pixel count and the quad's width in metres <em>together</em>, so a mini panel is
/// a smaller image at a smaller width — drawing the full image and hanging it nearer gives
/// text a third of the size, which is the one thing a surface meant to be read at a glance
/// cannot be.
/// </para>
/// </summary>
public enum VrSurface
{
    PanelFull,
    PanelMini,

    /// <summary>
    /// Flat, output only, and not reachable from Overlay Positioning. Head-locked or world-locked
    /// since <a href="https://github.com/dseelinger/d47/issues/204">#204</a>, between two computed
    /// positions — the lock is the only thing about a caption's placement that is settable.
    /// </summary>
    Captions,
}

/// <summary>
/// Head-locked or world-locked, per surface (Phase 9, "VR Panel locking").
/// </summary>
public enum SurfaceLock
{
    /// <summary>Carried by the head. Always in view, never in the way of a re-anchor.</summary>
    HeadLocked,

    /// <summary>Put down in the world and left there. What re-anchor exists for.</summary>
    WorldLocked,
}

/// <summary>
/// Everything <em>Overlay Positioning &amp; Look</em> configures, for one surface. Each of the
/// five knobs the checklist names maps onto exactly one OpenVR call, which is what keeps the
/// settings surface honest about what it is changing:
/// <list type="bullet">
/// <item><description><see cref="Opacity"/> — <c>SetOverlayAlpha</c></description></item>
/// <item><description><see cref="Curvature"/> — <c>SetOverlayCurvature</c>, where 0 is flat.
/// That is the whole of <em>Panels can switch between curved and flat</em>: it is a value on
/// this record and not a second mode.</description></item>
/// <item><description><see cref="DistanceMetres"/> — how far along the anchor's forward</description></item>
/// <item><description><see cref="WidthMetres"/> — <c>SetOverlayWidthInMeters</c>; height
/// follows from the texture's aspect and cannot be set</description></item>
/// <item><description><see cref="ZoomPercent"/> — the content's own scale, which is
/// <em>Scale the big panel</em>, and distinct from mini mode because it changes how large the
/// panel is drawn rather than how much of it there is</description></item>
/// </list>
/// </summary>
public sealed record SurfacePlacement
{
    public SurfaceLock Lock { get; init; } = SurfaceLock.HeadLocked;

    /// <summary>
    /// How far in front. 1.4 m was tried in a previous implementation and read as enormous —
    /// close to fifty degrees of view, so the panel filled the middle and the cockpit was
    /// behind it rather than around it.
    /// </summary>
    public float DistanceMetres { get; init; } = 1.1f;

    /// <summary>How far below eye level, in metres. Negative is down.</summary>
    public float DropMetres { get; init; } = -0.25f;

    /// <summary>
    /// A trim on top of the tilt that already faces the Commander, in degrees. Zero means "face
    /// my eyes", which is what <see cref="VrPlacementMath.EyeFacingPitch"/> works out from
    /// <see cref="DistanceMetres"/> and <see cref="DropMetres"/>.
    /// <para>
    /// This used to be the whole angle, fixed at 12°, and a fixed angle can only suit one
    /// distance and drop — see <see cref="VrPlacementMath.EyeFacingPitch"/> for what that cost
    /// mini. A file written before the change carries the old 12 and is cleared once, on load,
    /// by the repair <see cref="Configuration.VrSettings.PitchRepaired"/> counts.
    /// </para>
    /// </summary>
    public float PitchDegrees { get; init; }

    /// <summary>
    /// Whether the surface tilts to face the Commander's eyes, or holds whatever
    /// <see cref="PitchDegrees"/> says outright.
    /// <para>
    /// True for the panel, which is furniture a Commander reads. False for captions, and only
    /// them: they sit 0.45 m below the eye at 1.6 m, so deriving would tilt them 15.7° and they
    /// are deliberately square to the view. Not a settings row — it is a property of what the
    /// surface is for, and nothing about a caption layer is placed by hand.
    /// </para>
    /// <para>
    /// <b>Read only on the head-locked path</b>, which is what <see cref="Where"/> uses it for. A
    /// world-locked caption carries a pose that was already tilted at the eye when it was worked
    /// out (<see cref="VrPlacementMath.Resting"/>), so this says nothing about it — see
    /// <c>VrCaptionSurface</c>, where the band 40° below the eye would be read edge-on without
    /// that tilt.
    /// </para>
    /// </summary>
    public bool FacesTheEyes { get; init; } = true;

    public float WidthMetres { get; init; } = 1.0f;

    /// <summary>0 is flat, 1 is fully wrapped. SteamVR's own range.</summary>
    public float Curvature { get; init; }

    public float Opacity { get; init; } = 0.95f;

    /// <summary>The content scale, on the same ladder the desktop window zooms with.</summary>
    public int ZoomPercent { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>
    /// Where it was put down, for a world-locked surface, and where the head was when it was
    /// put there. Both, because re-anchor needs the second one: without it there is no delta
    /// to apply and "put it back" can only mean "stack it in front of me".
    /// </summary>
    public VrPose? Placed { get; init; }

    public VrPose? PlacedAgainst { get; init; }

    /// <summary>
    /// Clamped to what SteamVR and a human will actually accept. A hand-edited settings file
    /// can hold anything, and a width of zero is an overlay that is there and invisible —
    /// which presents as the feature not working rather than as a bad number.
    /// </summary>
    public SurfacePlacement Sane() => this with
    {
        DistanceMetres = Math.Clamp(DistanceMetres, 0.3f, 5f),
        DropMetres = Math.Clamp(DropMetres, -2f, 2f),
        PitchDegrees = Math.Clamp(PitchDegrees, -60f, 60f),
        WidthMetres = Math.Clamp(WidthMetres, 0.15f, 4f),
        Curvature = Math.Clamp(Curvature, 0f, 1f),
        Opacity = Math.Clamp(Opacity, 0.1f, 1f),
        ZoomPercent = Interface.ZoomLadder.Snap(ZoomPercent),
    };

    /// <summary>
    /// Where this surface goes, given where the head is now. A world-locked surface that has
    /// never been put down falls back to its head-locked position, so the first thing the
    /// Commander sees is a panel in front of them rather than one at the origin behind them.
    /// </summary>
    /// <summary>
    /// Whether this surface rides the head rather than sitting in the room. A world-locked
    /// surface that has never been put down still rides it, which is what puts the first panel
    /// a Commander ever sees in front of them.
    /// </summary>
    public bool RidesTheHead => Lock != SurfaceLock.WorldLocked || Placed is null;

    /// <summary>
    /// Where this surface sits relative to the head itself, for a runtime that can hang an
    /// overlay off the headset rather than being told a room position every frame.
    /// <para>
    /// <b>Derived from <see cref="Where"/> rather than computed beside it</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/189">#189</a>). The offset is by
    /// definition the pose that, composed with the head, lands the quad where <see cref="Where"/>
    /// says it goes — so taking it as <c>where · head⁻¹</c> makes the drawn quad and the pose
    /// every other caller reasons about the same thing by construction. Two parallel derivations
    /// is how a ray comes to be cast at a surface a degree or two from where it is drawn.
    /// </para>
    /// <para>
    /// It still leaves the tracking universe out of it altogether, which is the point of having
    /// it: the runtime carries the quad and nothing crosses the seated-versus-standing boundary.
    /// </para>
    /// </summary>
    public VrPose AgainstTheHead(VrPose head)
    {
        if (!Matrix4x4.Invert(head.ToMatrix(), out var inverse))
        {
            // A head pose that cannot be inverted is not a pose. Falling back to the origin gives
            // the offset this returned before the head was consulted at all, which is a surface
            // in the right place and not levelled — a downgrade, not a disappearance.
            return Where(VrPose.Origin);
        }

        return VrPose.FromMatrix(Where(head).ToMatrix() * inverse);
    }

    /// <summary>The offset against a head at the origin, which is one that is already level.</summary>
    public VrPose AgainstTheHead() => AgainstTheHead(VrPose.Origin);

    /// <summary>
    /// Where this surface goes, given where the head is now.
    /// <para>
    /// <b>A head-locked surface hangs off the head's <em>upright</em> frame</b>, not off the
    /// headset itself (#189). It follows the Commander's yaw and pitch and ignores their roll, so
    /// it stays level with the horizon rather than level with their head — which is what a
    /// caption reported as sitting rotated clockwise from the cockpit's own lines was missing.
    /// See <see cref="VrPlacementMath.Upright"/>.
    /// </para>
    /// </summary>
    public VrPose Where(VrPose head)
    {
        if (Lock == SurfaceLock.WorldLocked && Placed is { } placed)
        {
            return placed;
        }

        return VrPlacementMath.HeadLocked(
            VrPlacementMath.Upright(head),
            DistanceMetres,
            DropMetres,
            PitchDegrees * MathF.PI / 180f,
            FacesTheEyes);
    }
}
