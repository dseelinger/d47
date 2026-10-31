using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>Cell readers for the generated TSV tables.</summary>
internal static class TsvCells
{
    public static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;

    public static int? Integer(string[] cells, int index) =>
        Text(cells, index) is { } text && int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
