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

    /// <summary>Head-locked, flat, output only. Not reachable from Overlay Positioning.</summary>
    Captions,
}

/// <summary>
/// Head-locked or world-locked, per surface (list.md Phase 9, "VR Panel locking").
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

    /// <summary>Tilted back towards the Commander, in degrees, so a dropped panel still faces them.</summary>
    public float PitchDegrees { get; init; } = 12f;

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
    public VrPose Where(VrPose head)
    {
        if (Lock == SurfaceLock.WorldLocked && Placed is { } placed)
        {
            return placed;
        }

        return VrPlacementMath.HeadLocked(
            head,
            DistanceMetres,
            DropMetres,
            PitchDegrees * MathF.PI / 180f);
    }
}
