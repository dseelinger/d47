namespace D47.Core.Interface;

/// <summary>
/// How many pixels the big headset panel is rendered at (list.md Phase 25, "The panel resizes
/// and zooms").
/// <para>
/// <b>Three levers, and they are different things worth keeping distinct in the Commander's
/// head.</b> Pixels decide how much the image can hold. Metres — <c>VrSurfaceSettings.Width</c>
/// — decide how big it looks in the room. And zoom decides how much logical layout those pixels
/// carry: <see cref="ZoomLadder"/> is a <c>LayoutTransform</c>, so it re-measures and rewraps
/// rather than merely scaling, which makes it a density control rather than only a legibility
/// one. A Commander who wants a whole ship's slots visible zooms out; one who wants to read
/// across the cockpit zooms in.
/// </para>
/// <para>
/// <b>Every rung is 16:10.</b> The quad takes a width in metres and derives its height from the
/// texture's aspect, so a resize that changed the aspect would silently change the shape of the
/// thing in the room — a Commander asking for more pixels would get a differently-proportioned
/// panel they did not ask for. Holding the aspect makes the two levers independent, which is
/// the whole claim above.
/// </para>
/// <para>
/// <b>The ceiling is not code.</b> Past what the quad subtends on the headset's own panel,
/// extra pixels are render cost with nothing to show for it, and cost is linear in them — which
/// <c>VrPanelSurface</c> already says. The top rung is generous rather than a limit anybody has
/// measured; what stops a Commander wasting a frame budget is being able to see what they
/// picked, not a number in here.
/// </para>
/// </summary>
public static class PanelResolution
{
    /// <summary>
    /// What the panel has always been, and what it stays for a Commander who never touches this.
    /// </summary>
    public static readonly (int Width, int Height) Default = (1024, 640);

    /// <summary>
    /// The rungs, smallest first. Chosen rather than continuous, and for the reason the zoom
    /// ladder is: the control is used with a ray at a metre and no keyboard, where picking from
    /// a short list is the gesture that works and typing a number is not.
    /// </summary>
    public static readonly IReadOnlyList<(int Width, int Height)> Steps =
    [
        (800, 500),
        (1024, 640),
        (1280, 800),
        (1600, 1000),
        (1920, 1200),
        (2560, 1600),
    ];

    /// <summary>"1280x800" — how a settings file holds one and how a row labels it.</summary>
    public static string Describe((int Width, int Height) size) => $"{size.Width}x{size.Height}";

    /// <summary>Every rung, as the strings a choice row offers.</summary>
    public static IReadOnlyList<string> Choices => [.. Steps.Select(Describe)];

    /// <summary>
    /// The nearest rung to whatever a file happens to hold, by pixel count.
    /// <para>
    /// Snapping rather than rejecting, exactly as <see cref="ZoomLadder.Snap"/> does and for the
    /// same reason: settings are hand-editable, and a rejected size presents as the setting
    /// silently not taking. Zero and negatives land on the smallest rung rather than on
    /// something a texture allocation would refuse.
    /// </para>
    /// </summary>
    public static (int Width, int Height) Snap(int width, int height)
    {
        var wanted = (long)Math.Max(0, width) * Math.Max(0, height);
        var nearest = Steps[0];

        foreach (var step in Steps)
        {
            var area = (long)step.Width * step.Height;

            if (Math.Abs(area - wanted) < Math.Abs((long)nearest.Width * nearest.Height - wanted))
            {
                nearest = step;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The rung a "1280x800" names, or the default when it names nothing — which is what a
    /// hand-edited file, an older settings revision and an empty string all arrive as.
    /// </summary>
    public static (int Width, int Height) Parse(string? value)
    {
        if (value is null)
        {
            return Default;
        }

        var at = value.IndexOf('x', StringComparison.OrdinalIgnoreCase);

        if (at <= 0
            || !int.TryParse(value[..at], out var width)
            || !int.TryParse(value[(at + 1)..], out var height))
        {
            return Default;
        }

        return Snap(width, height);
    }

    /// <summary>
    /// What a mini panel presents at, which is not on this ladder and is not meant to be.
    /// <para>
    /// 512 wide rather than 640 because that is the lever on apparent text size, and the height
    /// does <b>not</b> shrink with it: mini is "the tail and the provenance line", the chrome
    /// around it does not get smaller, and holding the aspect left the transcript pane with
    /// nothing. The reasoning is <c>VrPanelSurface</c>'s and it is recorded there in full; this
    /// is the number, kept beside the other one so a reader finds both together.
    /// </para>
    /// </summary>
    public static readonly (int Width, int Height) Mini = (512, 280);
}
