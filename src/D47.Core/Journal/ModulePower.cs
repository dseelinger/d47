using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// What Elite says each module in the ship being flown actually draws (Phase 38).
/// <para>
/// <b>The measured half of the power gauge.</b> The game writes <c>ModulesInfo.json</c> beside the
/// journal with a <c>Power</c> and a <c>Priority</c> per slot, computed with the engineering
/// already in them — the same standing as the roll figures the Loadout tab already treats as
/// measured: authoritative when present, absent often, never invented.
/// </para>
/// <para>
/// <b>The journal's <c>ModuleInfo</c> event is a marker and carries none of this.</b> All 694 of
/// them in the 918-journal corpus are a timestamp and an event name — the figures live only in
/// the file, only for the ship being flown, and only as of the last time Elite rewrote it. The
/// plan of record read those 694 as measurements the corpus could be replayed against; it cannot,
/// and the check is a live one.
/// </para>
/// </summary>
public sealed record ModulePower
{
    public static readonly ModulePower None = new();

    /// <summary>Megawatts drawn, by the slot's own journal name. Empty until the file is read.</summary>
    public IReadOnlyDictionary<string, double> Draw { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The priority group each module is on, by slot.
    /// <para>
    /// Read and kept rather than drawn: Phase 38 ships two gauges and no ladder, and the figures a
    /// ladder would need are exactly these. <b>Not every module carries one</b> — the cockpit, the
    /// cosmetics and the colonisation suite have no priority at all — so a missing entry is a real
    /// state and not a parse failure.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, int> Priority { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The module in each slot, so a stale file can be told from a current one.</summary>
    public IReadOnlyDictionary<string, string> Items { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>When Elite last wrote it. Null means the file has not been seen.</summary>
    public DateTimeOffset? ReadAt { get; init; }

    /// <summary>
    /// Whether this file describes the ship whose loadout is in hand.
    /// <para>
    /// <b>The file names no ship</b>, so the check is that every slot it lists holds the module the
    /// <c>Loadout</c> says is there. A Commander who swaps ships and does not touch outfitting
    /// leaves the previous ship's figures on disk, and using those would put one hull's draw under
    /// another hull's plant.
    /// </para>
    /// </summary>
    public bool Describes(ShipLoadout? loadout)
    {
        if (ReadAt is null || Items.Count == 0 || loadout is null || loadout.Modules.Count == 0)
        {
            return false;
        }

        var agreed = 0;

        foreach (var module in loadout.Modules)
        {
            if (!Items.TryGetValue(module.Slot, out var item))
            {
                continue;
            }

            if (!string.Equals(item, module.Item, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            agreed++;
        }

        return agreed > 0;
    }
}

/// <summary>
/// Pull-based reads of <c>ModulesInfo.json</c>, on the same terms as every other reader here: no
/// thread, no clock, re-read only when the file's write time moves.
/// </summary>
public sealed class ModulePowerReader(string directory, ILogger logger)
{
    public const string FileName = "ModulesInfo.json";

    private DateTime _stamp;

    public ModulePower Current { get; private set; } = ModulePower.None;

    public bool Poll()
    {
        var path = Path.Combine(directory, FileName);

        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                return false;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat ModulesInfo.json");
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var document = JsonDocument.Parse(stream);

            var draw = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in document.RootElement.Items("Modules"))
            {
                if (element.String("Slot") is not { Length: > 0 } slot)
                {
                    continue;
                }

                if (element.Double("Power") is { } power)
                {
                    draw[slot] = power;
                }

                if (element.Int("Priority") is { } group)
                {
                    priority[slot] = group;
                }

                if (element.String("Item") is { Length: > 0 } item)
                {
                    items[slot] = item;
                }
            }

            Current = new ModulePower
            {
                Draw = draw,
                Priority = priority,
                Items = items,
                ReadAt = new DateTimeOffset(written, TimeSpan.Zero),
            };

            _stamp = written;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogDebug(ex, "Could not read ModulesInfo.json; will retry");
            return false;
        }
    }
}
