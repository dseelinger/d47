using D47.App.Media;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The playing video, as its own quad over the picture area the panel draws, so its pictures go up at the video's own
/// rate from the runtime's presenter while the panel is redrawn only when its content changes.
/// </summary>
public sealed class VrVideoSurface(VrPanelSurface panel, Func<VideoFrame?> frame) : IVrVideoSource
{
    /// <summary>Read on the serve.</summary>
    public bool Visible => Shows(panel.Visible, panel.VideoArea is not null, panel.AvatarCovered, frame() is not null);

    /// <summary>Read on the serve.</summary>
    public SurfacePlacement Placement =>
        panel.VideoArea is { } area
            ? VrPlacementMath.Over(panel.Placement.Sane(), panel.DrawnSize, area)
            : panel.Placement;

    /// <summary>Read on the presenter thread.</summary>
    public VrPicture? Picture => frame() is { } shown ? new VrPicture(shown, shown.Pixels, shown.Width, shown.Height) : null;

    /// <summary>Whether the quad goes up: the panel is, it drew a player's picture, nothing covers it, and there is a picture.</summary>
    internal static bool Shows(bool panelVisible, bool drawnOnPanel, bool covered, bool hasPicture) =>
        panelVisible && drawnOnPanel && !covered && hasPicture;
}
