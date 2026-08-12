using System.Globalization;
using System.Xml.Linq;

namespace D47.Core.Interface;

/// <summary>
/// The 3x3 matrix Elite Dangerous applies to its HUD colours. Each output channel is the dot
/// product of one row with the source colour, which is why a Commander's HUD can be teal or
/// white without the game shipping a teal or white palette.
/// </summary>
public sealed record GuiColourMatrix(
    double RedFromRed, double RedFromGreen, double RedFromBlue,
    double GreenFromRed, double GreenFromGreen, double GreenFromBlue,
    double BlueFromRed, double BlueFromGreen, double BlueFromBlue)
{
    /// <summary>The matrix that changes nothing — the game's own default.</summary>
    public static readonly GuiColourMatrix Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public (byte R, byte G, byte B) Transform(byte r, byte g, byte b)
    {
        var (rd, gd, bd) = (r / 255.0, g / 255.0, b / 255.0);

        return (
            Clamp(rd * RedFromRed + gd * RedFromGreen + bd * RedFromBlue),
            Clamp(rd * GreenFromRed + gd * GreenFromGreen + bd * GreenFromBlue),
            Clamp(rd * BlueFromRed + gd * BlueFromGreen + bd * BlueFromBlue));
    }

    public bool IsIdentity => Equals(Identity);

    private static byte Clamp(double value) => (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);
}

/// <summary>
/// Reads the Commander's own HUD colour scheme, so one of d47's themes can match whatever the
/// cockpit already looks like (list.md Phase 4, "Themes").
/// <para>
/// Read-only and fail-soft, for the same reason the binds parser is read-only: this is the
/// Commander's game configuration, d47 is a guest in it, and a file that is missing, hand-edited
/// or written by a HUD mod resolves to "no matrix" rather than to an error. The theme then
/// falls back to d47's own Elite palette, which is what it would have looked like anyway.
/// </para>
/// </summary>
public static class ElitePalette
{
    /// <summary>Where Elite keeps the override, unless the Commander moved it.</summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments",
        "Elite Dangerous",
        "Options",
        "Graphics",
        "GraphicsConfigurationOverride.xml");

    public static GuiColourMatrix? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(path);

            var section = document.Descendants("GUIColour").Elements("Default").FirstOrDefault();
            if (section is null)
            {
                return null;
            }

            var red = ParseRow(section.Element("MatrixRed"));
            var green = ParseRow(section.Element("MatrixGreen"));
            var blue = ParseRow(section.Element("MatrixBlue"));

            if (red is null || green is null || blue is null)
            {
                return null;
            }

            return new GuiColourMatrix(
                red[0], red[1], red[2],
                green[0], green[1], green[2],
                blue[0], blue[1], blue[2]);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A row is three comma-separated numbers, with whatever whitespace the editor left.</summary>
    private static double[]? ParseRow(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var parts = element.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return null;
        }

        var values = new double[3];
        for (var i = 0; i < 3; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                return null;
            }
        }

        return values;
    }
}
