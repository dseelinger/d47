using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>What the activity draw is gated on: which places lie within a session's reach of the Commander, found by the galaxy search.</summary>
public static class DrawGates
{
    /// <summary>
    /// Whether the galaxy search lists a fixed station or a landable body, in a system with no permit, around the waypoint from
    /// <paramref name="start"/> toward <paramref name="target"/> at <paramref name="reach"/>, as a travelling chapter's places are searched.
    /// True when it cannot be told.
    /// </summary>
    public static async Task<bool> PlacesOnTheWayAsync(IGalaxyService? galaxy, StarPosition start, StarPosition target, double reach, ILogger logger, CancellationToken cancellationToken)
    {
        if (galaxy is not { } search)
        {
            return true;
        }

        var centre = ChapterPull.Waypoint(start, target, reach);
        var radius = ChapterPull.PlacesWithin * reach;

        try
        {
            var stations = await search.FindStationsAsync(StationQuery.Near(centre, radius, ChapterPull.PlacesPerKind), cancellationToken).ConfigureAwait(false);

            if (stations.Stations.Any(station => station is { Position: not null, IsFixed: true } && !PermitSystemTable.Locked(station.SystemName)))
            {
                return true;
            }

            var bodies = await search.FindBodiesAsync(BodyQuery.LandableNear(centre, radius, ChapterPull.PlacesPerKind), cancellationToken).ConfigureAwait(false);

            return bodies.Bodies.Any(body => body is { Position: not null, IsLandable: true } && !PermitSystemTable.Locked(body.SystemName));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The galaxy search could not list places on the way to a span's target, so the span is not turned back");
            return true;
        }
    }

    /// <summary>
    /// Whether a fixed station with a pad for the ship aboard, in a system needing no permit, lies within a story's reach.
    /// True when it cannot be told: no galaxy search, no known position, or the query failed.
    /// </summary>
    public static async Task<bool> StationsInReachAsync(IGalaxyService? galaxy, CommanderGameState? game, ILogger logger, CancellationToken cancellationToken)
    {
        if (galaxy is not { } search || game?.Location.StarSystem is not { Length: > 0 } system)
        {
            return true;
        }

        var ship = game.Ship;
        PadSize? pad = Enum.TryParse<PadSize>(EliteSpecifications.Ship(ship.Type)?.Pad, ignoreCase: true, out var size) ? size : null;

        try
        {
            var found = await search
                .FindStationsAsync(StationQuery.Near(system, AdventureGenerator.Radius(AdventureReach.Session, ship.MaxJumpRange, game.Carrier.Owned), 20), cancellationToken)
                .ConfigureAwait(false);

            return found.Stations.Any(station =>
                station.IsFixed && !PermitSystemTable.Locked(station.SystemName) && (pad is not { } needed || station.Admits(needed)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The galaxy search could not list the stations within reach of a story, so its activities are not gated on them");
            return true;
        }
    }

    /// <summary>
    /// Whether an open fixed station with Frontline Solutions, in a system at war, lies within a story's reach.
    /// False when it cannot be told: no galaxy search, no known position, or the query failed.
    /// </summary>
    public static async Task<bool> FrontlineInReachAsync(IGalaxyService? galaxy, CommanderGameState? game, ILogger logger, CancellationToken cancellationToken)
    {
        if (galaxy is not { } search || game?.Location.StarSystem is not { Length: > 0 } system)
        {
            return false;
        }

        var ship = game.Ship;
        PadSize? pad = Enum.TryParse<PadSize>(EliteSpecifications.Ship(ship.Type)?.Pad, ignoreCase: true, out var size) ? size : null;
        var resolver = new AdventureResolver(search, game.Standings);

        try
        {
            var radius = AdventureGenerator.Radius(AdventureReach.Session, ship.MaxJumpRange, game.Carrier.Owned);
            var found = await search.FindStationsAsync(AdventureResolver.FrontlineNear(system, radius, 20), cancellationToken).ConfigureAwait(false);

            foreach (var station in found.Stations.Where(station =>
                         station.IsFixed && !PermitSystemTable.Locked(station.SystemName) && (pad is not { } needed || station.Admits(needed))))
            {
                if (await resolver.FrontlineAtWarAsync(station, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The galaxy search could not list the Frontline Solutions stations within reach of a story, so on-foot kill bonds are not drawn");
            return false;
        }
    }

    /// <summary>
    /// Whether a system at war or in civil war lies within a story's reach.
    /// False when it cannot be told: no galaxy search, no known position, or the query failed.
    /// </summary>
    public static async Task<bool> WarInReachAsync(IGalaxyService? galaxy, CommanderGameState? game, ILogger logger, CancellationToken cancellationToken)
    {
        if (galaxy is not { } search || game?.Location.StarSystem is not { Length: > 0 } system)
        {
            return false;
        }

        var radius = AdventureGenerator.Radius(AdventureReach.Session, game.Ship.MaxJumpRange, game.Carrier.Owned);
        var requested = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = $"0-{radius.ToString("0", CultureInfo.InvariantCulture)}",
            ["faction_state"] = "War,Civil War",
        };

        if (!GalaxyQuery.TryParse(system, requested, 20, out var query, out _))
        {
            return false;
        }

        try
        {
            var found = await search.SearchAsync(query, cancellationToken).ConfigureAwait(false);

            return found.Systems.Any(candidate => candidate.AtWar);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The galaxy search could not list the systems at war within reach of a story, so space kill bonds are not drawn");
            return false;
        }
    }

    /// <summary>
    /// The chosen campaign front when its system address is known and the system lies within a story's reach, else null.
    /// Null when it cannot be told: no galaxy search, no known position, or the query failed.
    /// </summary>
    public static async Task<ChapterFront?> FrontInReachAsync(ChapterFront? front, IGalaxyService? galaxy, CommanderGameState? game, ILogger logger, CancellationToken cancellationToken)
    {
        if (front is not { SystemAddress: not null } || galaxy is not { } search || game?.Location.StarSystem is not { Length: > 0 } system)
        {
            return null;
        }

        var radius = AdventureGenerator.Radius(AdventureReach.Session, game.Ship.MaxJumpRange, game.Carrier.Owned);

        try
        {
            var distance = await search.DistanceAsync(system, front.System, cancellationToken).ConfigureAwait(false);

            return distance is { } away && away <= radius ? front : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The galaxy search could not measure the distance to the campaign front, so no front objective is drawn");
            return null;
        }
    }

    /// <summary>
    /// The state-gated mission families with a ranked station in a story's reach whose system has a faction in a required state.
    /// Empty when it cannot be told: no galaxy search, no known position, or the query failed.
    /// </summary>
    public static async Task<IReadOnlySet<string>> StateFamiliesInReachAsync(IGalaxyService? galaxy, CommanderGameState? game, ILogger logger, CancellationToken cancellationToken)
    {
        var inReach = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (galaxy is not { } search || game?.Location.StarSystem is not { Length: > 0 } system)
        {
            return inReach;
        }

        var radius = AdventureGenerator.Radius(AdventureReach.Session, game.Ship.MaxJumpRange, game.Carrier.Owned);

        foreach (var family in ChapterActivities.StateGatedFamilies)
        {
            var names = MissionCatalogue.All.Where(name => string.Equals(name.Family, family, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!MissionStationQuery.TryParse(system, names, radius, out var query, out _))
            {
                continue;
            }

            try
            {
                var scan = await search.FindMissionStationsAsync(query, cancellationToken).ConfigureAwait(false);

                if (MissionStationRanking.Rank(query, scan, MissionStationQuery.Size).Any(candidate => candidate.MatchingStates > 0))
                {
                    inReach.Add(family);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "The galaxy search could not list the stations within reach of a story for the mission family {Family}, so it is not drawn", family);
            }
        }

        return inReach;
    }
}
