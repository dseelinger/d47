using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Goals;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Watches;

namespace D47.Core.Web;

/// <summary>The stores a <see cref="WebSnapshot"/> reads d47's own records from; a missing one gives no section.</summary>
public sealed record WebSnapshotStores
{
    public ChecklistStore? Checklists { get; init; }

    public GoalBook? Goals { get; init; }

    public WatchStore? Watches { get; init; }

    public RoutePlanBook? RoutePlans { get; init; }
}

/// <summary>
/// Builds a <see cref="WebSnapshot"/> from one Commander's state and the stores' in-memory records. Reads
/// no clock and no file and makes no request, so it can run on the tick thread.
/// </summary>
public static class WebSnapshotBuilder
{
    private static readonly string[] Superpowers = ["Empire", "Federation", "Alliance", "Independent"];

    private static readonly string[] Navies = ["Empire", "Federation"];

    /// <param name="hud">Elite's HUD colour matrix, carried only when <paramref name="theme"/> is <see cref="ThemeCatalog.ElitePaletteId"/>.</param>
    /// <param name="zone">The zone a watch's last firing is worded in.</param>
    public static WebSnapshot Build(
        CommanderGameState state,
        WebSnapshotStores stores,
        string theme,
        GuiColourMatrix? hud,
        TimeZoneInfo zone,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(zone);

        var fid = state.Identity.FrontierId;

        return new WebSnapshot
        {
            FrontierId = fid,
            Name = state.Identity.Name,
            TakenAt = now,
            Theme = theme,
            HudMatrix = theme == ThemeCatalog.ElitePaletteId && hud is { } matrix ? Nine(matrix) : null,
            Progress = Progress(state),
            Ships = Ships(state),
            Carrier = Carrier(state.Carrier),
            Engineers = Engineers(state),
            Materials = Materials(state.Materials),
            Missions = Missions(state.Missions),
            Checklist = Checklist(state, stores),
            Watches = stores.Watches is { } watches ? Watches(watches.Watches, zone) : null,
            RoutePlans = stores.RoutePlans is { } plans ? RoutePlans(plans.PlansOf(fid)) : null,
        };
    }

    private static double[] Nine(GuiColourMatrix m) =>
    [
        m.RedFromRed, m.RedFromGreen, m.RedFromBlue,
        m.GreenFromRed, m.GreenFromGreen, m.GreenFromBlue,
        m.BlueFromRed, m.BlueFromGreen, m.BlueFromBlue,
    ];

    private static WebProgress? Progress(CommanderGameState state)
    {
        var location = state.Location is { StarSystem: { } system } where && Text(system) is { } named
            ? new WebLocation
            {
                StarSystem = named,
                StationName = where.Docked ? Text(where.StationName) : null,
                Mode = where.Mode == FlightMode.Unknown ? null : where.Mode.ToString(),
                Body = Text(where.Body),
                SystemFaction = Text(where.SystemFaction),
                ControllingPower = Text(where.ControllingPower),
                PowerplayState = Text(where.PowerplayState),
            }
            : null;

        var progress = new WebProgress
        {
            Balance = state.Session.Balance,
            Location = location,
            Ranks = OrNull(state.Ranks.Standings.Where(standing => standing.IsCareer).Select(Rank)),
            Navy = OrNull(Navies.Select(state.Ranks.For).OfType<RankStanding>().Select(Rank)),
            Reputation = OrNull(
                Superpowers
                    .Select(name => (name, reading: state.Reputation.Reading(name)))
                    .Where(pair => pair.reading is not null)
                    .Select(pair => new WebReputation(pair.name, pair.reading!.MyReputation))),
        };

        return progress == new WebProgress() ? null : progress;
    }

    private static WebRank Rank(RankStanding standing) => new(standing.Career, standing.Rank, standing.Percent);

    private static WebShips? Ships(CommanderGameState state)
    {
        var fleet = state.Fleet.IsKnown
            ? OrNull(
                state.Fleet.Ships.Select(ship => new WebStoredShip(
                    ship.Type, Text(ship.Name), ship.HasSystem ? ship.StarSystem : null, Text(ship.StationName), ship.InTransit, ship.Value)))
            : null;

        var flown = state.FlownShip is { IsKnown: true } ship
            ? new WebFlownShip
            {
                TypeName = Text(ship.TypeName),
                Name = Text(ship.Name),
                Ident = Text(ship.Ident),
                Rebuy = ship.Rebuy,
                MaxJumpRange = ship.MaxJumpRange,
                CargoCapacity = ship.CargoCapacity,
                FuelCapacity = ship.FuelCapacity,
                UnladenMass = ship.UnladenMass,
                TotalValue = ship.TotalValue,
                HullHealth = ship.HullHealth,
                Modules = OrNull(
                    ship.Modules.Select(module => new WebModule(
                        module.Slot,
                        module.Item,
                        Text(module.Blueprint),
                        module.BlueprintLevel,
                        Text(module.Experimental),
                        module.Health))),
            }
            : null;

        return fleet is null && flown is null ? null : new WebShips { Fleet = fleet, Flown = flown };
    }

    private static WebCarrier? Carrier(CarrierState carrier) => carrier.Owned
        ? new WebCarrier
        {
            CallSign = Text(carrier.CallSign),
            Name = Text(carrier.Name),
            Balance = carrier.Balance,
            StarSystem = Text(carrier.StarSystem),
            DestinationSystem = Text(carrier.DestinationSystem),
            DepartureTime = carrier.DepartureTime,
            JumpRange = carrier.JumpRange,
            MaxJumpRange = carrier.MaxJumpRange,
            Capacity = carrier.Capacity,
            FreeSpace = carrier.FreeSpace,
            CargoTonnes = carrier.CargoTonnes,
            DockingAccess = Text(carrier.DockingAccess),
            WeeklyUpkeep = carrier.WeeklyUpkeep,
            FuelLevel = carrier.FuelLevel,
            TritiumInHold = carrier.TritiumInHold,
            Services = OrNull(
                carrier.Services.Select(service => new WebCarrierService(
                    service.Role, service.Name, service.Activated, service.Enabled))),
        }
        : null;

    private static List<WebEngineer>? Engineers(CommanderGameState state)
    {
        if (!state.Engineers.IsKnown)
        {
            return null;
        }

        var report = UnlockPlanner.Of([], [], state);

        return OrNull(
            report.Directory.Select(entry => new WebEngineer
            {
                Name = entry.Engineer.Name,
                Station = Text(entry.Engineer.Station),
                System = Text(entry.Engineer.System),
                Specialities = OrNull(
                    entry.Engineer.Specialities.Select(speciality => new WebSpeciality(
                        speciality.Kind, speciality.IsGraded ? speciality.MaxGrade : null))),
                UnlockCost = Text(entry.Engineer.UnlockCost),
                ReferredBy = OrNull(entry.Engineer.ReferredBy),
                Reach = entry.Reach.ToString(),
                Standing = entry.Standing is { } standing
                    ? new WebEngineerStanding(standing.Progress, standing.Rank, standing.RankProgress)
                    : null,
                LightYears = entry.LightYears,
                Jumps = entry.Jumps,
                Prerequisites = OrNull(entry.Criteria.Select(Prerequisite)),
            }));
    }

    private static WebPrerequisite Prerequisite(UnlockCriterion criterion)
    {
        var state = criterion.State;
        int? percent = state == UnlockCriterionState.InProgress && criterion.Measure is { } measure
            ? (int)Math.Round(measure.Fill * 100, MidpointRounding.AwayFromZero)
            : null;

        return new WebPrerequisite(criterion.Text, state.ToString(), percent);
    }

    private static List<WebMaterial>? Materials(MaterialsInventory materials) => OrNull(
        materials.All
            .OrderBy(holding => holding.Category)
            .ThenBy(holding => holding.Speak(), StringComparer.Ordinal)
            .Select(holding => new WebMaterial(
                holding.Speak(), holding.Category.ToString(), holding.Count, MaterialGrades.GradeOf(holding.Name))));

    private static List<WebMission>? Missions(MissionBoard board) => OrNull(
        board.Missions.Select(mission => new WebMission
        {
            Title = mission.Title,
            Faction = Text(mission.Faction),
            TargetFaction = Text(mission.TargetFaction),
            DestinationStation = Text(mission.DestinationStation),
            DestinationSystem = Text(mission.DestinationSystem),
            Expiry = mission.Expiry,
            Reward = mission.Reward,
            Cargo = mission.Cargo is { } cargo ? new WebMissionCargo(cargo.Collected, cargo.Delivered, cargo.Total) : null,
            PassengerCount = mission.PassengerCount,
        }));

    private static WebChecklist? Checklist(CommanderGameState state, WebSnapshotStores stores)
    {
        var fid = state.Identity.FrontierId;
        List<WebChecklistList>? lists = null;

        if (stores.Checklists?.Documents.FirstOrDefault(document =>
                string.Equals(document.CommanderFid, fid, StringComparison.Ordinal)) is { } document)
        {
            var arranged = ChecklistOrdering.Arrange(document, state);

            lists = OrNull(
                ChecklistLists.Build(arranged, state).Select(list => new WebChecklistList
                {
                    Name = list.Name,
                    Home = list.Home.ToString(),
                    Open = list.Open,
                    Done = list.Done,
                    Next = Text(list.Next),
                    IsHere = list.IsHere,
                    Items = OrNull(
                        arranged
                            .Where(item => ChecklistLists.IdOf(item) == list.Id)
                            .Select(item => new WebChecklistItem(ChecklistWording.Said(item, state), item.State.ToString()))),
                }));
        }

        var goals = stores.Goals is { } book
            ? OrNull(
                book.BuiltInStandingsOf(state).Select(standing => new WebGoal
                {
                    Name = standing.Arc.Name,
                    Have = standing.Have,
                    Need = standing.Need,
                    Fraction = standing.Fraction,
                    Rung = standing.Rung,
                    NextRank = Text(standing.NextRank),
                    IsDone = standing.IsDone,
                }))
            : null;

        return lists is null && goals is null ? null : new WebChecklist { Lists = lists, Goals = goals };
    }

    private static List<WebWatch>? Watches(IReadOnlyList<Watch> watches, TimeZoneInfo zone) => OrNull(
        watches.Select(watch => new WebWatch
        {
            Name = watch.Name,
            Kind = watch.Kind.ToString(),
            Enabled = watch.Enabled,
            Station = Text(watch.Station),
            System = Text(watch.System),
            Commodity = Text(watch.Commodity),
            Comparison = watch.Comparison?.ToString(),
            Threshold = watch.Threshold,
            FiredAt = watch.FiredAt,
            Fired = WatchFired.Of(watch, zone) is var fired && fired != WatchFired.NotYet ? fired : null,
            Service = Text(watch.Service),
            ServiceKind = Text(watch.ServiceKind),
            MinPad = watch.MinPad?.ToString(),
        }));

    private static List<WebRoutePlan>? RoutePlans(IReadOnlyList<StoredRoutePlan> plans) => OrNull(
        plans.Select(plan => new WebRoutePlan
        {
            Kind = plan.Kind.ToString(),
            Headline = Text(plan.Headline),
            PlottedAt = plan.PlottedAt,
            Reached = plan.Reached,
            Jump = plan.Kind == RoutePlanKind.Jump && plan.Jump is { } jump
                ? new WebJumpRoute(
                    jump.Origin,
                    jump.Destination,
                    jump.TotalDistance,
                    jump.TotalJumps,
                    [.. jump.Waypoints.Select(waypoint => new WebWaypoint(
                        waypoint.System, waypoint.Jumps, waypoint.DistanceLeft, waypoint.IsNeutron))])
                : null,
        }));

    private static List<T>? OrNull<T>(IEnumerable<T> items) => items.ToList() is { Count: > 0 } list ? list : null;

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
