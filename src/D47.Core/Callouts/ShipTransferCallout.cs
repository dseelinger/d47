using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>Says when a ship sent to the Commander's station has arrived.</summary>
public sealed class ShipTransferCallout : ICallout
{
    public string Id => "ship-transfer";

    public const string Key = "ship.transfer";

    private readonly List<Pending> _pending = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ShipyardTransfer" || journalEvent.Long("TransferTime") is not { } seconds || seconds <= 0)
            {
                continue;
            }

            var station = context.State?.Location.StationName;
            var shipId = journalEvent.Int("ShipID");
            var hull = journalEvent.Named("ShipType") ?? "ship";

            _pending.Add(new Pending(journalEvent.Timestamp.AddSeconds(seconds), hull, shipId, station));
        }

        if (_pending.Count == 0)
        {
            yield break;
        }

        var due = _pending.Where(entry => entry.Due <= context.Now).OrderBy(entry => entry.Due).ToList();

        _pending.RemoveAll(entry => entry.Due <= context.Now);

        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var entry in due)
        {
            var name = context.State?.Fleet.Ships.FirstOrDefault(ship => ship.ShipId == entry.ShipId && entry.ShipId is not null)?.Name;
            var ship = name is { Length: > 0 } ? name : EliteSpecifications.HullSaid(entry.Hull);
            var where = entry.Station is { Length: > 0 } station ? $" at {station}" : string.Empty;

            yield return new Announcement(Key, $"Your {ship} has arrived{where}.") { Cooldown = TimeSpan.Zero };
        }
    }

    private sealed record Pending(DateTimeOffset Due, string Hull, int? ShipId, string? Station);
}
