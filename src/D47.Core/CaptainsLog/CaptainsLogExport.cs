using System.Globalization;
using System.Text;
using D47.Core.Utilities;

namespace D47.Core.CaptainsLog;

/// <summary>What <see cref="CaptainsLogStore.Export"/> wrote: <paramref name="Path"/> is null when there was nothing to write or the write failed.</summary>
public sealed record CaptainsLogExported(int Count, string? Path);

/// <summary>Renders log entries as Markdown, oldest first.</summary>
public static class CaptainsLogExport
{
    public static string Render(IEnumerable<CaptainsLogEntry> entries)
    {
        var text = new StringBuilder("# Captain's log").AppendLine();

        foreach (var entry in entries.OrderBy(entry => entry.At))
        {
            var galactic = GalacticTime.Galactic(entry.At.ToUniversalTime());

            text.AppendLine()
                .Append("## ").AppendLine(galactic.ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture));

            if (Stamp(entry) is { Length: > 0 } stamp)
            {
                text.AppendLine().Append('*').Append(stamp).AppendLine("*");
            }

            text.AppendLine().AppendLine(entry.Text.Trim());
        }

        return text.ToString();
    }

    private static string Stamp(CaptainsLogEntry entry)
    {
        var ship = string.Join(
            ' ',
            new[] { entry.ShipType, entry.ShipName is { } name ? $"\"{name}\"" : null }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var position = entry.Latitude is { } latitude && entry.Longitude is { } longitude
            ? $"{latitude.ToString("0.0000", CultureInfo.InvariantCulture)}, {longitude.ToString("0.0000", CultureInfo.InvariantCulture)}"
            : null;

        var fields = new (string Label, string? Value)[]
        {
            ("System", entry.System),
            ("Body", entry.Body),
            ("Station", entry.Station),
            ("Ship", ship),
            ("Position", position),
        };

        return string.Join(
            "; ",
            fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)).Select(field => $"{field.Label}: {field.Value}"));
    }
}
