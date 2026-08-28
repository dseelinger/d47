using System.Globalization;
using System.Xml.Linq;

namespace D47.Core.Interface;

/// <summary>
/// How Elite is putting itself on the screen, which is the one thing that decides whether a
/// topmost window can be seen over it at all (Phase 48).
/// </summary>
public enum EliteDisplayMode
{
    /// <summary>
    /// d47 could not tell. The file is missing, unreadable, hand-edited, written by a mod, or
    /// holds a number nobody has seen. <b>This draws the overlay rather than refusing to</b> —
    /// the failure this whole reader exists to prevent is a strip that is silently not there.
    /// </summary>
    Unknown,

    /// <summary>A window with a frame. A topmost overlay composites over it.</summary>
    Windowed,

    /// <summary>A borderless window filling the screen. A topmost overlay composites over it.</summary>
    Borderless,

    /// <summary>
    /// Exclusive full screen. The game owns the swap chain, and a topmost overlay is <b>simply
    /// not there</b> — no error, no log line, nothing to diagnose.
    /// </summary>
    Exclusive,
}

/// <summary>
/// Reads which display mode Elite is set to, so the overlay can say out loud when the Commander
/// will not be able to see it (Phase 48).
/// <para>
/// <b>Why this is worth a reader at all.</b> A topmost window composites over a borderless or
/// windowed game and is simply not there over an exclusive-fullscreen one, with no error and no
/// log line. That is the failure shape this project has already paid for twice — the VR overlay
/// that ran with sound and no picture, and the microphone whose silent default was
/// indistinguishable from not hearing — so a small feature earns a check that turns an
/// undiagnosable nothing into a sentence.
/// </para>
/// <para>
/// <b>Read-only and fail-soft</b>, under the rule this inherits outright from
/// <see cref="ElitePalette"/>: this is the Commander's game configuration and d47 is a guest in
/// it. Missing, hand-edited or written by a mod resolves to <see cref="EliteDisplayMode.Unknown"/>,
/// and cannot-tell draws the overlay.
/// </para>
/// </summary>
public static class EliteDisplay
{
    /// <summary>
    /// Where Elite keeps it — beside the graphics override <see cref="ElitePalette"/> already
    /// opens, in the same Options/Graphics folder.
    /// </summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments",
        "Elite Dangerous",
        "Options",
        "Graphics",
        "DisplaySettings.xml");

    /// <summary>
    /// The mode the file names, or <see cref="EliteDisplayMode.Unknown"/> for anything this
    /// cannot read with confidence.
    /// <para>
    /// <b>2 is borderless, read off the Commander's own machine on 2026-08-22</b>, with the
    /// game set to borderless at the time. 0 for windowed and 1 for exclusive are the community's
    /// reading rather than Frontier's — nobody has flipped the setting and looked — which is why
    /// <see cref="Describe"/> says what it saw as well as what it thinks it means.
    /// </para>
    /// </summary>
    public static EliteDisplayMode Read(string path) => Number(path) switch
    {
        0 => EliteDisplayMode.Windowed,
        1 => EliteDisplayMode.Exclusive,
        2 => EliteDisplayMode.Borderless,
        _ => EliteDisplayMode.Unknown,
    };

    /// <summary>
    /// The raw <c>FullScreen</c> element, or null when there is not one to read. Its own method
    /// so a value nobody has documented can be reported by number rather than swallowed into
    /// "cannot tell".
    /// </summary>
    public static int? Number(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(path);

            if (document.Root is not { Name.LocalName: "DisplayConfig" } root)
            {
                return null;
            }

            var element = root.Element("FullScreen");

            return element is not null
                   && int.TryParse(
                       element.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode)
                ? mode
                : null;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// What the settings row says. <b>The sentence is worth more than the feature it guards</b>:
    /// a Commander in exclusive full screen who turns the overlay on sees nothing at all, and
    /// this is the only place that can tell them why.
    /// </summary>
    public static string Describe(string path) => Describe(Read(path), Number(path));

    /// <inheritdoc cref="Describe(string)"/>
    public static string Describe(EliteDisplayMode mode, int? number) => mode switch
    {
        EliteDisplayMode.Borderless =>
            "Elite is set to borderless, so the overlay will draw over it.",
        EliteDisplayMode.Windowed =>
            "Elite is set to windowed, so the overlay will draw over it.",
        EliteDisplayMode.Exclusive =>
            "Elite is set to exclusive full screen. Nothing can draw over that — the overlay "
            + "will be invisible while the game has the screen. Set Elite's display mode to "
            + "borderless in its graphics options.",
        _ when number is { } seen =>
            $"Elite's display mode reads {seen.ToString(CultureInfo.InvariantCulture)}, which D47 "
            + "does not recognise. The overlay is drawn anyway.",
        _ =>
            "D47 could not read Elite's display mode, so it will draw the overlay anyway. If you "
            + "cannot see it over the game, set Elite's display mode to borderless.",
    };
}
