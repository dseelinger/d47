using System.Globalization;
using System.Xml.Linq;

namespace D47.Core.Interface;

/// <summary>
/// How Elite is putting itself on the screen, which is the one thing that decides whether a topmost
/// window can be seen over it at all (Phase 48).
/// </summary>
public enum EliteDisplayMode
{
    /// <summary>d47 could not tell.</summary>
    Unknown,

    /// <summary>A window with a frame.</summary>
    Windowed,

    /// <summary>A borderless window filling the screen.</summary>
    Borderless,

    /// <summary>Exclusive full screen.</summary>
    Exclusive,
}

/// <summary>
/// Reads which display mode Elite is set to, so the overlay can say out loud when the Commander will
/// not be able to see it (Phase 48).
/// </summary>
public static class EliteDisplay
{
    /// <summary>
    /// Where Elite keeps it — beside the graphics override <see cref="ElitePalette"/> already opens, in
    /// the same Options/Graphics folder.
    /// </summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments",
        "Elite Dangerous",
        "Options",
        "Graphics",
        "DisplaySettings.xml");

    /// <summary>
    /// The mode the file names, or <see cref="EliteDisplayMode.Unknown"/> for anything this cannot read
    /// with confidence.
    /// </summary>
    public static EliteDisplayMode Read(string path) => Number(path) switch
    {
        0 => EliteDisplayMode.Windowed,
        1 => EliteDisplayMode.Exclusive,
        2 => EliteDisplayMode.Borderless,
        _ => EliteDisplayMode.Unknown,
    };

    /// <summary>The raw <c>FullScreen</c> element, or null when there is not one to read.</summary>
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

    /// <summary>What the settings row says.</summary>
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
