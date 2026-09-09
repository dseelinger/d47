using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One commodity in the hold, and how much of it.</summary>
/// <param name="Symbol">The internal name, folded through <see cref="JournalJson.Symbol"/>.</param>
public sealed record CargoItem(string Symbol, int Count)
{
    /// <summary>Elite's own spelling, where it wrote one.</summary>
    public string? DisplayName { get; init; }

    /// <summary>How many of these are stolen, as Elite reports it.</summary>
    public int Stolen { get; init; }
}

/// <summary>What is in the hold right now (Phase 18, "Colonisation and construction tracking").</summary>
public sealed record CargoHold
{
    public static readonly CargoHold Empty = new();

    /// <summary><c>Ship</c> or <c>SRV</c>, as Elite words it.</summary>
    public string? Vessel { get; init; }

    public IReadOnlyList<CargoItem> Items { get; init; } = [];

    /// <summary>Elite's own total tonnage.</summary>
    public int Count { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    public bool IsKnown => ReadAt is not null;

    /// <summary>Whether the manifest describes the ship, as against the SRV under it.</summary>
    public bool IsShip => !string.Equals(Vessel, "SRV", StringComparison.OrdinalIgnoreCase);

    /// <summary>How many tonnes of one commodity are aboard.</summary>
    public int Of(string? symbol) =>
        JournalJson.Symbol(symbol) is { } wanted
            ? Items.Where(item => item.Symbol == wanted).Sum(item => item.Count)
            : 0;
}

/// <summary>Pull-based reads of <c>Cargo.json</c>.</summary>
public sealed class CargoManifestReader(string directory, ILogger logger)
{
    public const string ManifestFile = "Cargo.json";

    private DateTime _stamp;

    public CargoHold Current { get; private set; } = CargoHold.Empty;

    /// <summary>Re-reads the manifest if it has changed.</summary>
    public bool Poll()
    {
        var path = Path.Combine(directory, ManifestFile);

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
            logger.LogDebug(ex, "Could not stat {File}", ManifestFile);
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        try
        {
            // Share everything: Elite is actively rewriting this file, and a plain OpenRead loses the race
            // often enough to matter at 10 Hz.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var document = JsonDocument.Parse(stream);

            var root = document.RootElement;
            var items = new List<CargoItem>();

            foreach (var element in root.Items("Inventory"))
            {
                if (JournalJson.Symbol(element.String("Name")) is not { } symbol)
                {
                    continue;
                }

                items.Add(new CargoItem(symbol, element.Int("Count") ?? 0)
                {
                    DisplayName = element.String("Name_Localised"),
                    Stolen = element.Int("Stolen") ?? 0,
                });
            }

            // Recorded only after a successful parse, so a file caught mid-write is retried on the next tick
            // instead of being skipped until Elite happens to touch it again.
            _stamp = written;

            Current = new CargoHold
            {
                Vessel = root.String("Vessel"),
                Items = items,
                Count = root.Int("Count") ?? items.Sum(item => item.Count),
                ReadAt = new DateTimeOffset(written, TimeSpan.Zero),
            };

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Caught mid-write.
            logger.LogDebug(ex, "Could not read {File}; will retry", ManifestFile);
            return false;
        }
    }
}
