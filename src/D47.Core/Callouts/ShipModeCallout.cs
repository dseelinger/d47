using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A remark from the core aboard when the Commander has held a ship mode long enough to be deliberate, once
/// per mode per game session and at most one every five minutes (#1445).
/// </summary>
public sealed class ShipModeCallout : ICallout
{
    /// <summary>Every ship-mode key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "shipmode.";

    private static readonly TimeSpan Spacing = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MiningWindow = TimeSpan.FromMinutes(10);

    private enum Quiet { Nothing, Mining, MiningOrCargo }

    private sealed record Mode(
        string Key,
        string Name,
        string Line,
        StatusFlags Needs,
        StatusFlags Without,
        TimeSpan Hold,
        Quiet After);

    private static readonly Mode[] Modes =
    [
        new(KeyPrefix + "silent", "silent running", "Running silent for a while now.",
            StatusFlags.SilentRunning, StatusFlags.None, TimeSpan.FromSeconds(10), Quiet.Nothing),
        new(KeyPrefix + "faoff", "flight assist off", "Flight assist has been off for a while now.",
            StatusFlags.FlightAssistOff, StatusFlags.None, TimeSpan.FromSeconds(30), Quiet.Nothing),
        new(KeyPrefix + "hardpoints", "hardpoints deployed with nothing hostile around",
            "Hardpoints have been out for a while with nothing hostile around.",
            StatusFlags.HardpointsDeployed, StatusFlags.InDanger, TimeSpan.FromSeconds(60), Quiet.Mining),
        new(KeyPrefix + "scoop", "the cargo scoop left open", "The cargo scoop has been left open for a while now.",
            StatusFlags.CargoScoopDeployed, StatusFlags.None, TimeSpan.FromSeconds(60), Quiet.MiningOrCargo),
        new(KeyPrefix + "gear", "the landing gear down away from a station",
            "The landing gear has been down for a while now.",
            StatusFlags.LandingGearDown, StatusFlags.FsdMassLocked, TimeSpan.FromSeconds(60), Quiet.Nothing),
        new(KeyPrefix + "masslocked", "the ship mass locked while in danger",
            "We have been mass locked in danger for a while now.",
            StatusFlags.FsdMassLocked | StatusFlags.InDanger, StatusFlags.None, TimeSpan.FromSeconds(10), Quiet.Nothing),
    ];

    private readonly Dictionary<string, DateTimeOffset> _since = new(StringComparer.Ordinal);
    private readonly HashSet<string> _spoken = new(StringComparer.Ordinal);
    private DateTimeOffset? _lastMining;
    private DateTimeOffset? _lastCargo;
    private DateTimeOffset? _lastRemark;

    public string Id => "ship-modes";

    /// <summary>The mode a ship-mode key names, or null for any other key.</summary>
    internal static string? ModeOf(string key) =>
        Modes.FirstOrDefault(mode => mode.Key == key)?.Name;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame" when journalEvent.StartsSession:
                    _since.Clear();
                    _spoken.Clear();
                    _lastMining = null;
                    _lastCargo = null;
                    _lastRemark = null;
                    break;

                case "ProspectedAsteroid" or "MiningRefined" or "AsteroidCracked":
                    _lastMining = journalEvent.Timestamp;
                    break;

                case "CollectCargo":
                    _lastCargo = journalEvent.Timestamp;
                    break;
            }
        }

        var status = context.Status;
        var flying = !context.IsPriming
            && status.Has(StatusFlags.InMainShip)
            && !status.Has(StatusFlags.Docked)
            && !status.Has(StatusFlags.Landed)
            && !status.Has(StatusFlags.Supercruise);

        Announcement? said = null;

        foreach (var mode in Modes)
        {
            if (!flying || !status.Has(mode.Needs) || (status.Flags & mode.Without) != 0)
            {
                _since.Remove(mode.Key);
                continue;
            }

            if (!_since.TryGetValue(mode.Key, out var since))
            {
                _since[mode.Key] = context.Now;
                continue;
            }

            if (said is not null
                || context.Now - since < mode.Hold
                || _spoken.Contains(mode.Key)
                || (_lastRemark is { } last && context.Now - last < Spacing)
                || Recent(mode.After, context.Now))
            {
                continue;
            }

            _spoken.Add(mode.Key);
            _lastRemark = context.Now;
            said = new Announcement(mode.Key, mode.Line);
        }

        if (said is not null)
        {
            yield return said;
        }
    }

    private bool Recent(Quiet after, DateTimeOffset now) => after switch
    {
        Quiet.Mining => Within(_lastMining, now),
        Quiet.MiningOrCargo => Within(_lastMining, now) || Within(_lastCargo, now),
        _ => false,
    };

    private static bool Within(DateTimeOffset? at, DateTimeOffset now) => at is { } then && now - then < MiningWindow;
}
