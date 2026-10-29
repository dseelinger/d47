using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One commodity in the hold, and how much of it.</summary>
/// <param name="Symbol">The internal name, folded through <see cref="JournalJson.Symbol"/>.</param>
public sealed record CargoItem(string Symbol, int Count)
{
    /// <summary>Elite's own spelling, where it wrote one.</summary>
    public string? DisplayName { get; init; }
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
public sealed class CargoManifestReader(string directory, IFileSystem files, ILogger logger)
{
    public const string ManifestFile = "Cargo.json";

    private DateTime _stamp;

    public CargoHold Current { get; private set; } = CargoHold.Empty;

    /// <summary>Re-reads the manifest if it has changed.</summary>
    public bool Poll()
    {
        var path = Path.Combine(directory, ManifestFile);

        var hold = StampedJson.Read(files, path, ref _stamp, logger, (root, written) =>
        {
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
                });
            }

            return new CargoHold
            {
                Vessel = root.String("Vessel"),
                Items = items,
                Count = root.Int("Count") ?? items.Sum(item => item.Count),
                ReadAt = new DateTimeOffset(written, TimeSpan.Zero),
            };
        });

        if (hold is null)
        {
            return false;
        }

        Current = hold;
        return true;
    }
}
