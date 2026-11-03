using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>One rare's published source quote and far sell price; a null field is unknown.</summary>
public sealed record RareIndexRow(
    string Symbol,
    int? FarSellPrice,
    int FarReports,
    int? BuyPrice,
    int? Stock,
    DateTimeOffset? ReportedAt);

/// <summary>The rare index, <c>rares.tsv</c>, parsed from its text.</summary>
public sealed class RareIndex
{
    public const string Header = "symbol\tfar_sell_price\tfar_reports\tbuy_price\tstock\treported_at";

    private readonly Dictionary<string, RareIndexRow> _rows;

    private RareIndex(DateTimeOffset? built, DateTimeOffset? farPricesAt, Dictionary<string, RareIndexRow> rows)
    {
        Built = built;
        FarPricesAt = farPricesAt;
        _rows = rows;
    }

    public DateTimeOffset? Built { get; }

    public DateTimeOffset? FarPricesAt { get; }

    public IReadOnlyCollection<RareIndexRow> Rows => _rows.Values;

    public RareIndexRow? Find(string symbol) => _rows.GetValueOrDefault(symbol);

    /// <summary>Parses the file's text; null when the header line is missing.</summary>
    public static RareIndex? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        DateTimeOffset? built = null;
        DateTimeOffset? farPricesAt = null;
        Dictionary<string, RareIndexRow>? rows = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (rows is null)
            {
                if (line.StartsWith("# built ", StringComparison.Ordinal))
                {
                    built = Time(line["# built ".Length..]);
                }
                else if (line.StartsWith("# far prices ", StringComparison.Ordinal))
                {
                    farPricesAt = Time(line["# far prices ".Length..]);
                }
                else if (line == Header)
                {
                    rows = new Dictionary<string, RareIndexRow>(StringComparer.Ordinal);
                }
                else if (line.Length > 0 && !line.StartsWith('#'))
                {
                    return null;
                }

                continue;
            }

            var fields = line.Split('\t');

            if (fields.Length != 6 || fields[0].Length == 0)
            {
                continue;
            }

            rows[fields[0]] = new RareIndexRow(
                fields[0],
                Number(fields[1]),
                Number(fields[2]) ?? 0,
                Number(fields[3]),
                Number(fields[4]),
                Time(fields[5]));
        }

        return rows is null ? null : new RareIndex(built, farPricesAt, rows);
    }

    private static int? Number(string field) =>
        int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static DateTimeOffset? Time(string field) =>
        DateTimeOffset.TryParse(field, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value.ToUniversalTime()
            : null;
}
