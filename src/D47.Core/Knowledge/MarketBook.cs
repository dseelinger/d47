using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>The markets the Commander has stood in themselves (Phase 36).</summary>
public sealed class MarketBook(string path, ILogger logger)
{
    /// <summary>How many markets are kept.</summary>
    public const int Capacity = 25;

    private readonly List<MarketSnapshot> _markets = [];

    private readonly Lock _gate = new();

    public string Path { get; } = path;

    /// <summary>Everything remembered, newest first.</summary>
    public IReadOnlyList<MarketSnapshot> Markets
    {
        get
        {
            lock (_gate)
            {
                return [.. _markets];
            }
        }
    }

    public MarketSnapshot? At(string? system, string? station)
    {
        if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(station))
        {
            return null;
        }

        lock (_gate)
        {
            return _markets.FirstOrDefault(market => market.IsSamePlaceAs(station, system));
        }
    }

    /// <summary>Files a market, replacing whatever was known about that station.</summary>
    public bool Remember(MarketSnapshot market)
    {
        lock (_gate)
        {
            var existing = _markets.FindIndex(known => known.IsSamePlaceAs(market.Station, market.System));

            if (existing >= 0)
            {
                if (_markets[existing].UpdatedAt >= market.UpdatedAt)
                {
                    return false;
                }

                _markets.RemoveAt(existing);
            }

            _markets.Insert(0, market);

            if (_markets.Count > Capacity)
            {
                _markets.RemoveRange(Capacity, _markets.Count - Capacity);
            }
        }

        Save();
        return true;
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(Path);
            using var document = JsonDocument.Parse(stream);

            var read = new List<MarketSnapshot>();

            foreach (var element in document.RootElement.Items("markets"))
            {
                if (Read(element) is { } market)
                {
                    read.Add(market);
                }
            }

            lock (_gate)
            {
                _markets.Clear();
                _markets.AddRange(read.Take(Capacity));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // A remembered price is a convenience, never a correctness requirement: the plan still works from
            // the network alone.
            logger.LogDebug(ex, "Could not read the market book at {Path}", Path);
        }
    }

    private void Save()
    {
        try
        {
            MarketSnapshot[] snapshot;

            lock (_gate)
            {
                snapshot = [.. _markets];
            }

            var buffer = new MemoryStream();

            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteStartArray("markets");

                foreach (var market in snapshot)
                {
                    Write(writer, market);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            AtomicFile.WriteAllText(Path, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not write the market book at {Path}", Path);
        }
    }

    private static void Write(Utf8JsonWriter writer, MarketSnapshot market)
    {
        writer.WriteStartObject();
        writer.WriteString("station", market.Station);
        writer.WriteString("system", market.System);
        writer.WriteNumber("x", market.X);
        writer.WriteNumber("y", market.Y);
        writer.WriteNumber("z", market.Z);

        if (market.Type is { } type)
        {
            writer.WriteString("type", type);
        }

        writer.WriteBoolean("largePad", market.HasLargePad);

        if (market.UpdatedAt is { } seen)
        {
            writer.WriteString("seen", seen);
        }

        writer.WriteStartArray("quotes");

        // Ordered by name rather than by whatever the game listed, so two saves of the same market are the
        // same file and a Commander diffing it sees a price change rather than a shuffle.
        foreach (var quote in market.Quotes.Values.OrderBy(quote => quote.Commodity, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("commodity", quote.Commodity);
            writer.WriteNumber("buy", quote.BuyPrice);
            writer.WriteNumber("sell", quote.SellPrice);
            writer.WriteNumber("supply", quote.Supply);
            writer.WriteNumber("demand", quote.Demand);

            if (quote.IsRare)
            {
                writer.WriteBoolean("rare", true);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static MarketSnapshot? Read(JsonElement element)
    {
        var station = element.String("station");
        var system = element.String("system");

        if (station is null || system is null)
        {
            return null;
        }

        var quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

        foreach (var quote in element.Items("quotes"))
        {
            if (quote.String("commodity") is not { } commodity)
            {
                continue;
            }

            quotes[commodity] = new MarketQuote(commodity)
            {
                BuyPrice = quote.Int("buy") ?? 0,
                SellPrice = quote.Int("sell") ?? 0,
                Supply = quote.Int("supply") ?? 0,
                Demand = quote.Int("demand") ?? 0,
                IsRare = quote.TryGetProperty("rare", out var rare) && rare.ValueKind == JsonValueKind.True,
            };
        }

        return new MarketSnapshot
        {
            Station = station,
            System = system,
            X = element.Double("x") ?? 0,
            Y = element.Double("y") ?? 0,
            Z = element.Double("z") ?? 0,
            Type = element.String("type"),
            HasLargePad = element.TryGetProperty("largePad", out var pad) && pad.ValueKind == JsonValueKind.True,
            UpdatedAt = element.TryGetProperty("seen", out var seen)
                        && seen.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(
                            seen.GetString(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal
                            | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsed)
                ? parsed
                : null,
            Source = PriceSource.Seen,
            Quotes = quotes,
        };
    }
}

/// <summary>Pull-based reads of <c>Market.json</c>, filed into a <see cref="MarketBook"/>.</summary>
public sealed class MarketReader(string directory, MarketBook book, ILogger logger)
{
    public const string MarketFile = "Market.json";

    private DateTime _stamp;

    /// <summary>Re-reads the market if the file has changed.</summary>
    public bool Poll(StarPosition? position)
    {
        var path = System.IO.Path.Combine(directory, MarketFile);

        DateTime written;

        try
        {
            var info = new FileInfo(path);

            // Not an error.
            if (!info.Exists)
            {
                return false;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat {File}", MarketFile);
            return false;
        }

        if (written == _stamp || position is null)
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var document = JsonDocument.Parse(stream);

            var root = document.RootElement;
            var station = root.String("StationName");
            var system = root.String("StarSystem");

            if (station is null || system is null)
            {
                return false;
            }

            var quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in root.Items("Items"))
            {
                // The localised spelling is the join key with everything else: the index and the Commander
                // both say "Low Temperature Diamonds", and only this file says
                // "$lowtemperaturediamond_name;".
                var name = item.String("Name_Localised")
                           ?? JournalJson.Spoken(JournalJson.Symbol(item.String("Name")));

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                quotes[name] = new MarketQuote(name)
                {
                    BuyPrice = item.Int("BuyPrice") ?? 0,
                    SellPrice = item.Int("SellPrice") ?? 0,

                    // Elite calls supply "Stock" here and the index calls it "supply".
                    Supply = item.Int("Stock") ?? 0,
                    Demand = item.Int("Demand") ?? 0,
                    IsRare = item.TryGetProperty("Rare", out var rare) && rare.ValueKind == JsonValueKind.True,
                };
            }

            // Recorded only after a successful parse, so a file caught mid-write is retried on the next tick
            // rather than skipped until the game happens to touch it again.
            _stamp = written;

            if (quotes.Count == 0)
            {
                return false;
            }

            return book.Remember(new MarketSnapshot
            {
                Station = station,
                System = system,
                X = position.Value.X,
                Y = position.Value.Y,
                Z = position.Value.Z,
                Type = root.String("StationType"),

                // The file says nothing about pads, and the Commander is standing on one.
                HasLargePad = true,
                UpdatedAt = new DateTimeOffset(written, TimeSpan.Zero),
                Source = PriceSource.Seen,
                Quotes = quotes,
            });
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogDebug(ex, "Could not read {File}; will retry", MarketFile);
            return false;
        }
    }
}
