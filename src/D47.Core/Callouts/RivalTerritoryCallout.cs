using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Arriving at or undocking from an orbital station or carrier in a rival Power's system.
/// </summary>
public sealed class RivalTerritoryCallout : ICallout
{
    public string Id => "rival-territory";

    /// <summary>How long an attack keeps this quiet after the last sign of it.</summary>
    public TimeSpan DangerHold { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Long enough that circling a station does not repeat it, and keyed per system so moving into a
    /// different rival Power's space says so immediately.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);

    /// <summary>The local day the full explanation last played, and how to remember a new one.</summary>
    public Func<string?>? LastExplainedDay { get; set; }

    public Action<string>? RememberExplainedDay { get; set; }

    private string? _sessionExplainedDay;

    private static readonly HashSet<string> StationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coriolis", "Orbis", "Ocellus", "Outpost", "AsteroidBase", "Dodec", "MegaShip", "FleetCarrier",
    };

    private const string StrongholdCarrier = "Stronghold Carrier";

    private readonly Dictionary<long, HashSet<string>> _carrierSignals = [];
    private bool _carrierDropPending;
    private string? _dockedName;
    private string? _dockedType;
    private DateTimeOffset? _quietUntil;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (Threat.IsUnder(context))
        {
            _quietUntil = context.Now + DangerHold;
        }

        var state = context.State;
        var location = state?.Location;

        var arrived = false;

        foreach (var journalEvent in context.Events)
        {
            arrived |= Fold(journalEvent, location?.SystemAddress);
        }

        // Folded during priming and announced never: the backlog is where the Commander already is.
        if (!arrived || context.IsPriming)
        {
            yield break;
        }

        if (location is null || !state!.Pledge.IsRival(location.ControllingPower))
        {
            yield break;
        }

        if (_quietUntil is { } quiet && context.Now < quiet)
        {
            yield break;
        }

        var power = location.ControllingPower!;

        // The Commander's local day, off the injected clock — no Core component reads one.
        var today = context.Now.LocalDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var full = !string.Equals(LastExplainedDay is { } last ? last() : _sessionExplainedDay, today, StringComparison.Ordinal);

        if (full)
        {
            if (RememberExplainedDay is { } remember)
            {
                remember(today);
            }
            else
            {
                _sessionExplainedDay = today;
            }
        }

        var text = full
            ? $"{SaidName(power)} controls this system, and you fly for {SaidName(state!.Pledge.Power!)}. You are exposed here."
            : "Hostile territory. Be on guard.";

        yield return new Announcement($"territory.rival.{location.StarSystem}", text)
        {
            Cue = AlertCue.RivalTerritory,
            Cooldown = Cooldown,
        };
    }

    /// <summary>Remembers what the event says about carriers and docked stations; true when it is an arrival or departure that speaks.</summary>
    private bool Fold(JournalEvent journalEvent, long? currentSystem)
    {
        switch (journalEvent.Kind)
        {
            case "FSDJump" or "CarrierJump":
                _carrierDropPending = false;

                if (journalEvent.Long("SystemAddress") is { } jumped)
                {
                    foreach (var key in _carrierSignals.Keys.Where(k => k != jumped).ToList())
                    {
                        _carrierSignals.Remove(key);
                    }
                }

                return false;

            case "FSSSignalDiscovered"
                when journalEvent.Long("SystemAddress") is { } address
                     && journalEvent.String("SignalName") is { Length: > 0 } name
                     && IsCarrierSignal(journalEvent.String("SignalType"), name):
                if (!_carrierSignals.TryGetValue(address, out var names))
                {
                    _carrierSignals[address] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                names.Add(name);
                return false;

            case "SupercruiseDestinationDrop":
                _carrierDropPending = currentSystem is { } system
                    && _carrierSignals.TryGetValue(system, out var known)
                    && (journalEvent.String("Type") is { } type && known.Contains(type)
                        || journalEvent.String("Type_Localised") is { } localised && known.Contains(localised));
                return false;

            case "SupercruiseExit":
                var carrier = _carrierDropPending;
                _carrierDropPending = false;
                return carrier || string.Equals(journalEvent.String("BodyType"), "Station", StringComparison.OrdinalIgnoreCase);

            case "Docked" or "Location" when journalEvent.Bool("Docked") || journalEvent.Kind == "Docked":
                _dockedName = journalEvent.String("StationName");
                _dockedType = journalEvent.String("StationType");
                return false;

            case "Undocked":
                var departedFrom = journalEvent.String("StationName") ?? _dockedName;
                var departed = string.Equals(departedFrom, StrongholdCarrier, StringComparison.OrdinalIgnoreCase)
                    || _dockedType is { } dockedType && StationTypes.Contains(dockedType);
                _dockedName = null;
                _dockedType = null;
                return departed;

            default:
                return false;
        }
    }

    private static bool IsCarrierSignal(string? type, string name) =>
        type is "FleetCarrier" or "SquadronCarrier"
        || (type is "StationMegaShip" && string.Equals(name, StrongholdCarrier, StringComparison.OrdinalIgnoreCase));

    /// <summary>"A.</summary>
    private static string SaidName(string power) =>
        power.Length > 3 && char.IsAsciiLetterUpper(power[0]) && power[1] == '.' && power[2] == ' '
            ? power[3..]
            : power;
}
