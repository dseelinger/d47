using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>One line of the expedition kit, and the list it would go on.</summary>
public sealed record KitLine(ChecklistScope Scope, string Text);

/// <summary>What to carry when a carrier route leaves the bubble, from the ships at the carrier.</summary>
public static class ExpeditionKit
{
    /// <summary>How far from both Sol and Colonia a waypoint is outside the bubble.</summary>
    public const double BubbleRadius = 1_000;

    public static readonly StarPosition Colonia = new(-9530.5, -910.28125, 19808.125);

    /// <summary>Whether any waypoint is more than <see cref="BubbleRadius"/> from both Sol and Colonia.</summary>
    public static bool LeavesTheBubble(CarrierRoute route) =>
        route.Waypoints.Any(waypoint => waypoint.Position is { } at
                                        && at.DistanceTo(StarPosition.Origin) > BubbleRadius
                                        && at.DistanceTo(Colonia) > BubbleRadius);

    /// <summary>The kit for the ships at this carrier, in the order it is offered.</summary>
    public static IReadOnlyList<KitLine> For(CommanderGameState state)
    {
        var aboard = Aboard(state);
        var lines = new List<KitLine>();

        foreach (var ship in aboard.Where(ship => ship.Loadout?.Fitted(FuelScoop) == false))
        {
            lines.Add(new KitLine(
                ChecklistScope.Ship(ship.ShipId),
                $"Store a fuel scoop for {ship.Said} — nothing out there sells one."));
        }

        if (!aboard.Any(ship => ship.Loadout is { } loadout && MiningFit.For(loadout) is { IsFit: true }))
        {
            lines.Add(new KitLine(
                ChecklistScope.Universal,
                "Carry a tritium mining kit — laser, collector controllers, refinery — the only fuel out there is what you mine."));
        }

        lines.Add(new KitLine(
            ChecklistScope.Universal,
            "AFMU and repair limpet controllers — nothing repairs modules out there but you."));

        if (!aboard.Any(ship => ship.Loadout?.Fitted(SrvBay) == true))
        {
            lines.Add(new KitLine(
                ChecklistScope.Universal,
                "An SRV bay and spare SRVs — the one nobody counts until it is zero."));
        }

        foreach (var ship in aboard.Where(ship => ship.Loadout?.Fitted(SurfaceScanner) == false))
        {
            lines.Add(new KitLine(
                ChecklistScope.Ship(ship.ShipId),
                $"A Detailed Surface Scanner for {ship.Said} — an unmapped world pays far less."));
        }

        lines.Add(new KitLine(
            ChecklistScope.Universal,
            "Limpets on the carrier market — no station out there sells them."));

        return lines;
    }

    private const string FuelScoop = "int_fuelscoop";

    private const string SrvBay = "int_buggybay";

    private const string SurfaceScanner = "int_detailedsurfacescanner";

    private sealed record ShipAboard(int ShipId, string Said, ShipLoadout? Loadout);

    /// <summary>The ships stored at the Commander's carrier, and the one being flown when docked there.</summary>
    private static List<ShipAboard> Aboard(CommanderGameState state)
    {
        var aboard = new List<ShipAboard>();

        if (state.Carrier.CarrierId is { } carrier)
        {
            aboard.AddRange(state.Fleet.Ships
                .Where(ship => ship.MarketId == carrier && !ship.InTransit)
                .Select(ship => new ShipAboard(ship.ShipId, ship.Describe(), state.Loadouts.For(ship.ShipId)?.Loadout)));
        }

        if (state.Carrier.DockedAtOwnCarrier
            && state.FlownShip is { ShipId: { } flown } loadout
            && aboard.All(ship => ship.ShipId != flown))
        {
            var said = (loadout.Name, loadout.TypeSaid) switch
            {
                ({ } name, { } type) => $"{name} ({type})",
                (null, { } type) => type,
                ({ } name, null) => name,
                _ => "your ship",
            };

            aboard.Add(new ShipAboard(flown, said, loadout));
        }

        return aboard;
    }
}
