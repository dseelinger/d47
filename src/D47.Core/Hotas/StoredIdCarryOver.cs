using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.Core.Hotas;

/// <summary>One stored controller id and the <see cref="HidIdentity"/> it moves to.</summary>
public sealed record IdMove(string OldId, string NewId)
{
    /// <summary>The move as the settings see it: the old id re-pointed, every button number kept.</summary>
    internal FoundDevice AsFound() => new(OldId, NewId, string.Empty, string.Empty, Fills: false);
}

/// <summary>
/// Moves stored <c>RawGameController.NonRoamableId</c>s to HID identities by matching this exe's two readings of the
/// same controllers. Reads and writes nothing itself apart from <see cref="Run"/>.
/// </summary>
public static class StoredIdCarryOver
{
    public const string LegacyPrefix = "{wgi/nrid/";

    /// <summary>Whether a stored id is one of the old form.</summary>
    public static bool IsLegacy(string? id) => id?.StartsWith(LegacyPrefix, StringComparison.Ordinal) == true;

    /// <summary>Every legacy id held by a switch or by the push-to-talk and cancel buttons.</summary>
    public static IReadOnlyList<string> LegacyIds(IReadOnlyList<SwitchMapping> stored, D47Settings settings) =>
    [
        .. stored.Select(mapping => mapping.DeviceId)
            .Append(HotasButton.Parse(settings.Listening.PushToTalkButton)?.DeviceId)
            .Append(HotasButton.Parse(settings.Speech.CancelButton)?.DeviceId)
            .Where(IsLegacy)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>
    /// The moves that are certain. A stored id seen by <paramref name="raw"/> moves to the HID reading of the same
    /// description when there is one; with several, only when their held buttons all differ and exactly one equals
    /// the held buttons of the raw reading.
    /// </summary>
    public static IReadOnlyList<IdMove> Plan(
        IReadOnlyList<string> legacyIds, IReadOnlyList<HotasReading> raw, IReadOnlyList<HotasReading> hid)
    {
        var moves = new List<IdMove>();

        foreach (var id in legacyIds)
        {
            var reading = raw.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

            if (reading is null)
            {
                continue;
            }

            var candidates = hid
                .Where(h => h.VendorId == reading.VendorId
                            && h.ProductId == reading.ProductId
                            && string.Equals(h.Describe(), reading.Describe(), StringComparison.Ordinal))
                .ToList();

            var chosen = candidates.Count switch
            {
                0 => null,
                1 => candidates[0],
                _ => Distinguished(candidates, reading),
            };

            if (chosen is not null)
            {
                moves.Add(new IdMove(id, chosen.Id));
            }
        }

        // Two stored ids never share a target.
        return [.. moves.GroupBy(move => move.NewId, StringComparer.Ordinal).Where(group => group.Count() == 1).SelectMany(group => group)];
    }

    private static HotasReading? Distinguished(List<HotasReading> candidates, HotasReading reading)
    {
        var held = candidates.Select(candidate => candidate.Held()).ToList();

        for (var i = 0; i < held.Count; i++)
        {
            for (var j = i + 1; j < held.Count; j++)
            {
                if (held[i].SequenceEqual(held[j]))
                {
                    return null;
                }
            }
        }

        var wanted = reading.Held();
        var matches = candidates.Where((_, index) => held[index].SequenceEqual(wanted)).ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>The stored set with every switch on a moved id re-pointed; device text is kept.</summary>
    public static IReadOnlyList<SwitchMapping> Apply(IReadOnlyList<SwitchMapping> stored, IReadOnlyList<IdMove> moves) =>
    [
        .. stored.Select(mapping =>
            moves.FirstOrDefault(move => string.Equals(move.OldId, mapping.DeviceId, StringComparison.Ordinal)) is { } hit
                ? mapping with { DeviceId = hit.NewId }
                : mapping),
    ];

    /// <summary>
    /// Takes one reading from each reader and writes the certain moves to the switch file and the two button
    /// settings. Returns whether the readers had settled, so the caller can try again if not.
    /// </summary>
    public static bool Run(
        IHotasReader raw, IHotasReader hid, SwitchStore store, SettingsService settings, ILogger logger)
    {
        store.Poll();

        if (LegacyIds(store.Switches, settings.Current).Count == 0)
        {
            return true;
        }

        if (!raw.IsSettled || !hid.IsSettled)
        {
            return false;
        }

        var hidReadings = hid.Poll();
        var rawReadings = raw.Poll();

        if (hidReadings.Count == 0 || rawReadings.Count == 0)
        {
            return false;
        }

        var moves = Plan(LegacyIds(store.Switches, settings.Current), rawReadings, hidReadings);

        if (moves.Count == 0)
        {
            return true;
        }

        var moved = Apply(store.Switches, moves);

        if (!moved.SequenceEqual(store.Switches))
        {
            store.Save(moved);
            store.Poll();
        }

        foreach (var move in moves)
        {
            settings.Replace(
                "controller identities", current => move.AsFound().RepointButtons(current, out _));
            logger.LogInformation("Moved stored controller id {Old} to {New}", move.OldId, move.NewId);
        }

        return true;
    }
}
