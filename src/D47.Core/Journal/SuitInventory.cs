using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One kind of thing carried on foot, and how many.</summary>
public sealed record SuitItem(string Name, string Kind, int Count)
{
    public string? DisplayName { get; init; }

    public string Speak() => DisplayName ?? Name;
}

/// <summary>
/// What the Commander is carrying on foot: the backpack they have with them and the ship locker they
/// left behind (Phase 7, "Know what is in your backpack and ship locker").
/// </summary>
public sealed record SuitInventory
{
    public static readonly SuitInventory Empty = new();

    /// <summary>Carried right now.</summary>
    public IReadOnlyList<SuitItem> Backpack { get; init; } = [];

    /// <summary>Stowed in the ship.</summary>
    public IReadOnlyList<SuitItem> ShipLocker { get; init; } = [];

    public DateTimeOffset? BackpackReadAt { get; init; }

    public DateTimeOffset? ShipLockerReadAt { get; init; }

    public bool IsKnown => BackpackReadAt is not null || ShipLockerReadAt is not null;

    public int BackpackCount => Backpack.Sum(item => item.Count);

    public int ShipLockerCount => ShipLocker.Sum(item => item.Count);

    /// <summary>The four lists Elite writes into each file.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["Items", "Components", "Consumables", "Data"];

    /// <summary>
    /// How many of one micro-resource the Commander has, backpack and locker together (Phase 20).
    /// </summary>
    public int CountOf(string symbol)
    {
        var wanted = JournalJson.Symbol(symbol);

        return wanted is null
            ? 0
            : Backpack.Concat(ShipLocker)
                .Where(item => JournalJson.Symbol(item.Name) == wanted)
                .Sum(item => item.Count);
    }

    /// <summary>
    /// Everything held in one of Elite's four categories, totalled — which is what the locker's cap is
    /// measured against.
    /// </summary>
    public int ShipLockerTotal(string kind) => ShipLockerOf(kind).Sum(item => item.Count);

    public IReadOnlyList<SuitItem> BackpackOf(string kind) =>
        [.. Backpack.Where(item => string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))];

    public IReadOnlyList<SuitItem> ShipLockerOf(string kind) =>
        [.. ShipLocker.Where(item => string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))];
}

/// <summary>Pull-based reads of the two on-foot inventory files.</summary>
public sealed class SuitInventoryReader(string directory, ILogger logger)
{
    public const string BackpackFile = "Backpack.json";

    public const string ShipLockerFile = "ShipLocker.json";

    private DateTime _backpackStamp;
    private DateTime _shipLockerStamp;

    public SuitInventory Current { get; private set; } = SuitInventory.Empty;

    /// <summary>Re-reads whichever of the two files has changed.</summary>
    public bool Poll()
    {
        var changed = false;

        if (ReadIfChanged(BackpackFile, ref _backpackStamp) is { } backpack)
        {
            Current = Current with
            {
                Backpack = backpack.Items,
                BackpackReadAt = backpack.Written,
            };
            changed = true;
        }

        if (ReadIfChanged(ShipLockerFile, ref _shipLockerStamp) is { } locker)
        {
            Current = Current with
            {
                ShipLocker = locker.Items,
                ShipLockerReadAt = locker.Written,
            };
            changed = true;
        }

        return changed;
    }

    private (IReadOnlyList<SuitItem> Items, DateTimeOffset Written)? ReadIfChanged(
        string fileName,
        ref DateTime stamp)
    {
        var path = Path.Combine(directory, fileName);

        DateTime written;

        try
        {
            var info = new FileInfo(path);

            // Not an error.
            if (!info.Exists)
            {
                return null;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat {File}", fileName);
            return null;
        }

        if (written == stamp)
        {
            return null;
        }

        try
        {
            // Share everything: Elite is actively rewriting this file, and a plain OpenRead loses the race
            // often enough to matter at 10 Hz.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var document = JsonDocument.Parse(stream);

            var items = new List<SuitItem>();

            foreach (var kind in SuitInventory.Kinds)
            {
                foreach (var element in document.RootElement.Items(kind))
                {
                    if (element.String("Name") is not { } name)
                    {
                        continue;
                    }

                    items.Add(new SuitItem(name, kind, element.Int("Count") ?? 0)
                    {
                        DisplayName = element.String("Name_Localised"),
                    });
                }
            }

            // Recorded only after a successful parse, so a file caught mid-write is retried on the next tick
            // instead of being skipped until Elite happens to touch it again.
            stamp = written;

            return (items, new DateTimeOffset(written, TimeSpan.Zero));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Caught mid-write.
            logger.LogDebug(ex, "Could not read {File}; will retry", fileName);
            return null;
        }
    }
}
