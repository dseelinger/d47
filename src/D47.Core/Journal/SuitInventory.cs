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
/// What the Commander is carrying on foot: the backpack they have with them and the ship
/// locker they left behind (Phase 7, "Know what is in your backpack and ship locker").
/// <para>
/// These come from <c>Backpack.json</c> and <c>ShipLocker.json</c> rather than from the
/// journal, which is what the checklist specifies and is also the only thing that works: the
/// matching journal events carry the full contents only sometimes, and Elite rewrites the two
/// files on every change. Reading the file is the reliable answer.
/// </para>
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

    /// <summary>
    /// The four lists Elite writes into each file. Kept as the game's own labels: a Commander
    /// asking about "components" means the tab with that name on it.
    /// </summary>
    public static IReadOnlyList<string> Kinds { get; } = ["Items", "Components", "Consumables", "Data"];

    /// <summary>
    /// How many of one micro-resource the Commander has, <b>backpack and locker together</b>
    /// (Phase 20).
    /// <para>
    /// Both, because an on-foot recipe spends from either and a Commander standing at Pioneer
    /// Supplies with the last five in their backpack is not short. Keyed on the journal's symbol,
    /// folded, so a name written one way in one file and another way in another still joins —
    /// the same reason <see cref="JournalJson.Symbol(string?)"/> exists.
    /// </para>
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
    /// Everything held in one of Elite's four categories, totalled — which is what the locker's
    /// cap is measured against. See <see cref="Knowledge.OnFootRules.LockerCapacityPerCategory"/>:
    /// the cap is per category rather than per item, measured over 28,148 locker snapshots.
    /// </summary>
    public int ShipLockerTotal(string kind) => ShipLockerOf(kind).Sum(item => item.Count);

    public IReadOnlyList<SuitItem> BackpackOf(string kind) =>
        [.. Backpack.Where(item => string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))];

    public IReadOnlyList<SuitItem> ShipLockerOf(string kind) =>
        [.. ShipLocker.Where(item => string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))];
}

/// <summary>
/// Pull-based reads of the two on-foot inventory files. Owns no thread and reads no clock, for
/// the same reason <see cref="JournalReader"/> does not (architecture.md §4) — the tick loop
/// calls <see cref="Poll"/>, and a test calls it directly.
/// <para>
/// Each file is re-read only when its last-write time moves. Elite rewrites both on every
/// change, and re-parsing two unchanged files ten times a second would be the loop's whole
/// cost for no result.
/// </para>
/// </summary>
public sealed class SuitInventoryReader(string directory, ILogger logger)
{
    public const string BackpackFile = "Backpack.json";

    public const string ShipLockerFile = "ShipLocker.json";

    private DateTime _backpackStamp;
    private DateTime _shipLockerStamp;

    public SuitInventory Current { get; private set; } = SuitInventory.Empty;

    /// <summary>
    /// Re-reads whichever of the two files has changed. Returns true when anything did, so the
    /// caller can skip work rather than having to compare inventories.
    /// </summary>
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

            // Not an error. A Commander who has never played on foot has neither file, and a
            // Horizons-only install never will.
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
            // Share everything: Elite is actively rewriting this file, and a plain OpenRead
            // loses the race often enough to matter at 10 Hz.
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

            // Recorded only after a successful parse, so a file caught mid-write is retried on
            // the next tick instead of being skipped until Elite happens to touch it again.
            stamp = written;

            return (items, new DateTimeOffset(written, TimeSpan.Zero));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Caught mid-write. Expected at 10 Hz against a file the game rewrites, and not
            // worth more than a debug line — the next poll gets it.
            logger.LogDebug(ex, "Could not read {File}; will retry", fileName);
            return null;
        }
    }
}
