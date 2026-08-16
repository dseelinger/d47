using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One commodity in the hold, and how much of it.</summary>
/// <param name="Symbol">
/// The internal name, folded through <see cref="JournalJson.Symbol"/>. This is the join key and
/// never the thing spoken — see <see cref="CargoHold"/> for why the display name comes from
/// somewhere else.
/// </param>
public sealed record CargoItem(string Symbol, int Count)
{
    /// <summary>
    /// Elite's own spelling, where it wrote one. <b>Absent more often than not</b> — 33 of the 64
    /// commodities measured carry no <c>Name_Localised</c> on any hold row, because the game omits
    /// it when the display name is merely the symbol capitalised.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>How many of these are stolen, as Elite reports it. Stated, never interpreted.</summary>
    public int Stolen { get; init; }
}

/// <summary>
/// What is in the hold right now (list.md Phase 18, "Colonisation and construction tracking").
/// <para>
/// <b>This comes from <c>Cargo.json</c> and cannot come from the journal.</b> The <c>Cargo</c>
/// event carries its <c>Inventory</c> array on only <b>1,151 of 13,762</b> occurrences measured
/// across the 912-journal corpus — essentially the first one in each file — and the other 12,611
/// carry a bare tonnage <c>Count</c>. So a journal-only reader has a full manifest for the first
/// minute of a session and a stale one for the rest of it, which reads exactly like a correct
/// answer. Elite rewrites the file on every change, which is the same reason
/// <see cref="SuitInventory"/> reads its two files rather than the matching events.
/// </para>
/// <para>
/// <b>The display name is not this record's to supply.</b> The join key is the symbol, and where a
/// commodity is wanted by a construction site the site's own <c>Name_Localised</c> is the spelling
/// used — it is present on all 120,208 depot rows measured, against a third of hold rows. So the
/// hold answers "how many" and the depot answers "called what".
/// </para>
/// </summary>
public sealed record CargoHold
{
    public static readonly CargoHold Empty = new();

    /// <summary>
    /// <c>Ship</c> or <c>SRV</c>, as Elite words it. <b>Carried rather than assumed</b>: the file
    /// is rewritten for the SRV's own hold when the Commander drives one, so a report that did not
    /// say which vessel it was describing would quietly answer about eight tonnes of scoopings
    /// while the ship above holds four hundred.
    /// </summary>
    public string? Vessel { get; init; }

    public IReadOnlyList<CargoItem> Items { get; init; } = [];

    /// <summary>
    /// Elite's own total tonnage. Kept beside the rows rather than recomputed from them: it is
    /// the game's figure, and if the two ever disagree that is worth being able to see.
    /// </summary>
    public int Count { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    public bool IsKnown => ReadAt is not null;

    /// <summary>Whether the manifest describes the ship, as against the SRV under it.</summary>
    public bool IsShip => !string.Equals(Vessel, "SRV", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How many tonnes of one commodity are aboard. Zero for one that is not, which is the same
    /// answer as "none of it" and is what every caller here wants.
    /// </summary>
    public int Of(string? symbol) =>
        JournalJson.Symbol(symbol) is { } wanted
            ? Items.Where(item => item.Symbol == wanted).Sum(item => item.Count)
            : 0;
}

/// <summary>
/// Pull-based reads of <c>Cargo.json</c>. Owns no thread and reads no clock, for the same reason
/// <see cref="JournalReader"/> does not (architecture.md §4) — the tick loop calls
/// <see cref="Poll"/>, and a test calls it directly.
/// <para>
/// Deliberately the same shape as <see cref="SuitInventoryReader"/>, down to the share flags and
/// the stamp-after-parse rule, because it is the same problem: a file the game rewrites in place
/// while d47 reads it at 10 Hz.
/// </para>
/// </summary>
public sealed class CargoManifestReader(string directory, ILogger logger)
{
    public const string ManifestFile = "Cargo.json";

    private DateTime _stamp;

    public CargoHold Current { get; private set; } = CargoHold.Empty;

    /// <summary>
    /// Re-reads the manifest if it has changed. Returns true when it did, so the caller can skip
    /// work rather than having to compare holds.
    /// </summary>
    public bool Poll()
    {
        var path = Path.Combine(directory, ManifestFile);

        DateTime written;

        try
        {
            var info = new FileInfo(path);

            // Not an error. A Commander who has not launched since installing has no file yet.
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
            // Share everything: Elite is actively rewriting this file, and a plain OpenRead loses
            // the race often enough to matter at 10 Hz.
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

            // Recorded only after a successful parse, so a file caught mid-write is retried on the
            // next tick instead of being skipped until Elite happens to touch it again.
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
            // Caught mid-write. Expected at 10 Hz against a file the game rewrites, and not worth
            // more than a debug line — the next poll gets it.
            logger.LogDebug(ex, "Could not read {File}; will retry", ManifestFile);
            return false;
        }
    }
}
