using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// At a crew lounge, says when the ship's fighter bay has no pilot posted to this hull, or when the pilot
/// posted here was left behind by a swap. A hangar known to hold the Nomad and no fighter is not checked.
/// </summary>
public sealed class FighterBayCrewCallout(ILogger? logger = null) : ICallout
{
    private const string Bay = "int_fighterbay";

    private const string Lounge = "crewlounge";

    private readonly HashSet<int> _told = [];

    private bool _atLounge;
    private bool _atCarrier;
    private bool _swapped;

    public string Id => "fighter-crew";

    public const string Key = "fighter-crew.empty";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "Docked":
                    _told.Clear();
                    _swapped = false;
                    _atLounge = journalEvent.Items("StationServices")
                        .Any(service => string.Equals(service.GetString(), Lounge, StringComparison.OrdinalIgnoreCase));
                    _atCarrier = string.Equals(
                        journalEvent.String("StationType"), "FleetCarrier", StringComparison.OrdinalIgnoreCase);

                    if (!context.IsPriming && _atLounge && Check(context) is { } docked)
                    {
                        yield return docked;
                    }

                    break;

                case "Undocked" or "LoadGame" or "Shutdown":
                    _atLounge = false;
                    _swapped = false;
                    _told.Clear();
                    break;

                case "ShipyardSwap" or "ShipyardNew":
                    _swapped = true;
                    break;

                case "Loadout" when _swapped:
                    _swapped = false;

                    if (!context.IsPriming && _atLounge && Check(context) is { } swapped)
                    {
                        yield return swapped;
                    }

                    break;
            }
        }
    }

    private Announcement? Check(CalloutContext context)
    {
        if (context.State is not { } state
            || context.Status.Has2(StatusFlags2.InMulticrew)
            || state.Ship.ShipId is not { } shipId
            || state.Fitted(Bay, "a fighter bay crew reminder", logger) is not true
            || state.Hangars.For(shipId) is { NomadOnly: true })
        {
            return null;
        }

        var posted = state.Crew.Members.Where(member => member.PostedShipId == shipId).ToList();

        string? text;

        if (posted.Count == 0)
        {
            text = Missing(state.Crew.Members);
        }
        else if (posted.Any(member => !member.HullLeft))
        {
            text = null;
        }
        else
        {
            text = $"Make {posted[0].Name} active in the crew lounge.";
        }

        if (text is null || !_told.Add(shipId))
        {
            return null;
        }

        return new Announcement(Key, text) { Cooldown = TimeSpan.FromMinutes(1) };
    }

    private string Missing(IReadOnlyList<CrewMember> hired)
    {
        var hire = _atCarrier ? string.Empty : ", or hire one here";

        return hired.Count switch
        {
            0 => _atCarrier
                ? "No pilot for this ship's fighter bay."
                : "No pilot for this ship's fighter bay. The crew lounge here can hire one.",
            1 => $"No pilot in this ship's fighter bay. Make {hired[0].Name} active in the crew lounge{hire}.",
            _ => $"No pilot in this ship's fighter bay. Make one of your {hired.Count} pilots active in the crew lounge{hire}.",
        };
    }
}
