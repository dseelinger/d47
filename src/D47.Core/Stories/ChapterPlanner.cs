using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>What a planned objective is in the chapter for.</summary>
public enum PlannedPurpose
{
    /// <summary>A drawn activity, with the place that hosts it as its first step where it needs one.</summary>
    Activity,

    /// <summary>The sightseeing chapter's sight.</summary>
    Sight,

    /// <summary>An extra story stop at a drawn place.</summary>
    Stop,

    /// <summary>The chapter's Goals step.</summary>
    Goal,

    /// <summary>The last stop inside the Sol band.</summary>
    Band,

    /// <summary>The last stop on the home leg or toward the chapter's target.</summary>
    Leg,

    /// <summary>The last stop of a finale chapter that starts beyond reach of the finale's destination: closer to it than the start.</summary>
    Closer,

    /// <summary>The last finale chapter's landing on the finale's destination.</summary>
    Destination,

    /// <summary>The board into an owned ship that can make the trip to the Guardian beacon, first in the chapter.</summary>
    Swap,

    /// <summary>The Guardian beacon scan that ends act one.</summary>
    Beacon,

    /// <summary>The last objective of an act-one chapter whose beacon is out of reach: a ship that can make the trip.</summary>
    Reach,

    /// <summary>The board into the ship a short story's brief names, first in the story.</summary>
    Ship,
}

/// <summary>One objective of a planned chapter: its function, its steps in order with places resolved and counts set, and what it is for.</summary>
public sealed record PlannedObjective(string Function, IReadOnlyList<AdventureTrigger> Steps, PlannedPurpose Purpose);

/// <summary>A drawn chapter's objectives in order, and the destination it carries when it has one.</summary>
public sealed record ChapterPlan(IReadOnlyList<PlannedObjective> Objectives, AdventureDestination? Destination);

/// <summary>Why no plan stands: the rule no candidate met, from <see cref="ChapterRules"/>, and a sentence for the log.</summary>
public sealed record ChapterPlanRefusal(string Rule, string Text);

/// <summary>A plan, or the refusal that stopped it.</summary>
public sealed record ChapterPlanResult(ChapterPlan? Plan, ChapterPlanRefusal? Refusal)
{
    public bool Succeeded => Plan is not null;
}

/// <summary>The names of the rules a planned chapter is held to, as a refusal names them.</summary>
public static class ChapterRules
{
    public const string Position = "position";
    public const string Count = "count";
    public const string Refused = "refused";
    public const string Hop = "hop";
    public const string Pad = "pad";
    public const string Permit = "permit";
    public const string ClosedMarket = "closed market";
    public const string Anarchy = "anarchy";
    public const string Frontline = "frontline";
    public const string War = "war";
    public const string PlaceBefore = "place before";
    public const string Sight = "sight";
    public const string Stop = "story stop";
    public const string Band = "band";
    public const string HomeLeg = "home leg";
    public const string Pull = "pull";
    public const string Finale = "finale";
    public const string Reserve = "credit reserve";
    public const string Team = "team";
}

/// <summary>Builds a drawn chapter's whole objective list from gathered places, with no galaxy search, model or clock.</summary>
public static class ChapterPlanner
{
    /// <summary>Most objectives a planned chapter holds.</summary>
    public const int MostObjectives = 8;

    /// <summary>Fewest objectives a planned chapter holds, so that it has a turn.</summary>
    public const int FewestObjectives = 3;

    /// <summary>Most extra story stops a chapter adds to its content.</summary>
    public const int MostStops = 2;

    /// <summary>Most objectives an act-one travel chapter holds, its stops included.</summary>
    public const int TravelObjectives = 5;

    /// <summary>Most stops a sightseeing objective makes on the way to a sight beyond reach.</summary>
    public const int MostSightHops = 3;

    /// <summary>Weight a notable place, a site's system or a sight's system is multiplied by.</summary>
    public const double NotableFactor = 3;

    /// <summary>Weight a system the Commander has not visited is multiplied by.</summary>
    public const double UnvisitedFactor = 2;

    /// <summary>Weight a mission held's destination is multiplied by.</summary>
    public const double MissionFactor = 3;

    /// <summary>Draws a plan makes before it refuses, each from a seed derived from the one given.</summary>
    public const int Attempts = 8;

    /// <summary>
    /// A chapter's objectives for <paramref name="inputs"/> over <paramref name="places"/>; the same arguments give the same plan.
    /// A draw whose earlier picks leave a later rule no place is drawn again from a derived seed, up to <see cref="Attempts"/>
    /// times; the first draw's refusal is returned when none stands.
    /// </summary>
    public static ChapterPlanResult Plan(ChapterInputs inputs, ChapterPlaces places, uint seed)
    {
        ChapterPlanResult? first = null;

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var result = Draw(inputs, places, unchecked(seed + ((uint)attempt * 0x9E3779B9u)));

            if (result.Succeeded)
            {
                return result;
            }

            first ??= result;
        }

        return first!;
    }

    private static ChapterPlanResult Draw(ChapterInputs inputs, ChapterPlaces places, uint seed)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(places);

        if (inputs.Position is null)
        {
            return Refuse(ChapterRules.Position, "The Commander's position is unknown, so no hop can be measured.");
        }

        foreach (var slot in inputs.Slots)
        {
            if (RefusedActivities.Refuses(inputs.Refused, slot.Kind, slot.Family, slot.Event, RefusedActivities.ToolOf(slot.Where)))
            {
                return Refuse(ChapterRules.Refused, $"The drawn activity {RefusedActivities.Key(slot.Kind, slot.Family, slot.Event)} was refused for this story.");
            }
        }

        var goals = inputs.Goals.Select(step => step.Objective).OfType<AdventureTrigger>().ToList();

        foreach (var goal in goals)
        {
            if (goal is { Kind: TriggerKind.Board, ShipType: { } symbol } && ChapterFit.BoardWhy(symbol, inputs.Owns(symbol), inputs.Credits) is { } why)
            {
                return Refuse(ChapterRules.Reserve, $"The Goals step boards a hull the Commander cannot buy yet: {why}.");
            }

            if (TeamBeats.Why(goal.Kind, inputs.Carrier.Owned, inputs.InSquadron, inputs.Credits, inputs.Pledge) is { } team)
            {
                return Refuse(ChapterRules.Team, $"The Goals step is a {goal.Kind.ToString().ToLowerInvariant()} objective, but {team}.");
            }
        }

        var random = new Random32(seed);

        if (inputs is { FinaleChapter: 1, Destination: null })
        {
            var (chosen, at, why) = ChooseDestination(inputs, places, random);

            if (chosen is null)
            {
                return Refuse(ChapterRules.Finale, why!);
            }

            inputs = inputs with { Destination = chosen };
            places = places with { DestinationAt = at };
        }

        var sight = places.Sights.Count > 0;
        var landing = inputs.Closing ? inputs.Destination!.Landing() : null;
        var end = landing is null ? Ending(inputs, places) : null;
        var swap = inputs.Beacon?.Swap();
        var (last, lastPurpose) = inputs.Beacon is { } beacon
            ? (beacon.Trigger(), PlannedPurpose.Beacon)
            : inputs.BeaconAway is { } away ? (away.Trigger(), PlannedPurpose.Reach) : ((AdventureTrigger?)null, PlannedPurpose.Reach);

        if (end is { Purpose: PlannedPurpose.Closer, Area.State: ChapterAreaState.NoneFound or ChapterAreaState.Unavailable })
        {
            return Refuse(ChapterRules.Finale, $"No place on the way to the finale's destination in {inputs.Destination!.System} was found, so the chapter cannot end closer to it.");
        }

        var board = swap is null ? inputs.Board : null;
        var content = inputs.Slots.Count + (sight ? 1 : 0) + goals.Count + (end is null && landing is null ? 0 : 1) + (swap is null ? 0 : 1) + (board is null ? 0 : 1) + (last is null ? 0 : 1);
        var rewriting = inputs.Functions is not null;
        var most = inputs.Objectives?.Most ?? MostObjectives;

        if (content == 0 && !rewriting && inputs.Objectives is null)
        {
            return Refuse(ChapterRules.Count, "The chapter has no drawn activity, sight, Goals step or stop to plan.");
        }

        if (content > most)
        {
            return Refuse(ChapterRules.Count, $"The chapter's content needs {content.ToString(CultureInfo.InvariantCulture)} objectives; it may hold {most.ToString(CultureInfo.InvariantCulture)}.");
        }

        // A rewrite needs one objective at least, and aims for as many as it replaces; a short story's count is drawn from its range.
        var fewestStops = Math.Max(0, (rewriting ? 1 : inputs.Objectives?.Fewest ?? FewestObjectives) - content);
        var mostStops = Math.Max(fewestStops, Math.Min(
            rewriting ? inputs.Functions!.Count - content : inputs.Objectives is { } range ? range.Most - content : inputs.Travels ? TravelObjectives - content : MostStops,
            most - content));
        var stops = rewriting ? mostStops : fewestStops + random.Next(mostStops - fewestStops + 1);

        var middle = inputs.Slots.Select(slot => new Item(PlannedPurpose.Activity, slot)).ToList();

        if (sight)
        {
            middle.Insert(random.Next(middle.Count + 1), new Item(PlannedPurpose.Sight, null));
        }

        for (var stop = 0; stop < stops; stop++)
        {
            middle.Insert(random.Next(middle.Count + 1), new Item(PlannedPurpose.Stop, null) { Needed = stop < fewestStops });
        }

        var planning = new Planning(inputs, places, random);
        var built = new List<(PlannedPurpose Purpose, List<AdventureTrigger> Steps)>();

        if (swap is not null)
        {
            built.Add((PlannedPurpose.Swap, [planning.Swap(swap)]));
        }

        if (board is not null)
        {
            built.Add((PlannedPurpose.Ship, [planning.Goal(board)]));
        }

        foreach (var item in middle)
        {
            var (steps, refusal) = item.Purpose switch
            {
                PlannedPurpose.Activity => planning.Activity(item.Slot!),
                PlannedPurpose.Sight => planning.Sight(),
                _ => planning.Stop(item.Needed),
            };

            if (refusal is not null)
            {
                return new ChapterPlanResult(null, refusal);
            }

            if (steps is not null)
            {
                built.Add((item.Purpose, steps));
            }
        }

        foreach (var goal in goals)
        {
            built.Add((PlannedPurpose.Goal, [planning.Goal(goal)]));
        }

        if (end is not null)
        {
            var (steps, refusal) = planning.End(end);

            if (refusal is not null)
            {
                return new ChapterPlanResult(null, refusal);
            }

            built.Add((end.Purpose, steps!));
        }

        if (landing is not null)
        {
            built.Add((PlannedPurpose.Destination, [landing]));
        }

        if (built.Count > 0 && planning.BackToBase() is { } back)
        {
            built[^1].Steps.Add(back);
        }

        if (last is not null)
        {
            built.Add((lastPurpose, [last]));
        }

        var functions = inputs.Functions is { } labels
            ? ChapterFunctions.Relabel(labels, built.Count)
            : inputs.Length == AdventureLength.Long ? ChapterFunctions.Long(built.Count) : ChapterFunctions.For(built.Count, inputs.Stage);

        return new ChapterPlanResult(
            new ChapterPlan([.. built.Select((objective, index) => new PlannedObjective(functions[index], objective.Steps, objective.Purpose))], inputs.Destination),
            null);
    }

    /// <summary>
    /// The landable body finale chapter 1 ends the story on: no permit, within the limit, drawn by weight from those at least half the
    /// limit away, or from the nearer ones when none is.
    /// </summary>
    private static (AdventureDestination? Destination, StarPosition? At, string? Refusal) ChooseDestination(ChapterInputs inputs, ChapterPlaces places, Random32 random)
    {
        var here = inputs.Position!.Value;
        var limit = AdventureDestination.Limit(inputs.Reach, inputs.FinaleChapters);
        var open = places.Destinations
            .Where(body => body is { IsLandable: true, Position: not null, BodyId: not null, SystemAddress: not null }
                && body.Position.Value.DistanceTo(here) <= limit
                && !PermitSystemTable.ClosedTo(body.SystemName, inputs.Ranks, () => places.WasVisited(body.SystemName)))
            .ToList();

        if (open.Count == 0)
        {
            return (null, null, $"No landable body without a permit was found within {limit.ToString("0", CultureInfo.InvariantCulture)} light years for the finale's destination.");
        }

        var far = open.Where(body => body.Position!.Value.DistanceTo(here) >= limit / 2).ToList();
        var pool = far.Count > 0 ? far : open;
        var picked = pool[random.Weighted([.. pool.Select(body =>
        {
            var weight = places.Notable.Concat(places.Sights).Any(place => place.SystemAddress is { } address ? address == body.SystemAddress : string.Equals(place.System, body.SystemName, StringComparison.OrdinalIgnoreCase))
                ? NotableFactor
                : 1.0;

            return places.WasVisited(body.SystemName) ? weight / UnvisitedFactor : weight;
        })])];

        return (new AdventureDestination(picked.SystemAddress!.Value, picked.SystemName, picked.BodyId!.Value, picked.Name), picked.Position, null);
    }

    private static ChapterPlanResult Refuse(string rule, string text) => new(null, new ChapterPlanRefusal(rule, text));

    /// <summary>
    /// The last-stop rule the chapter is held to: the system a changed objective asks for, closer to the finale's destination, the Sol
    /// band, or the home leg or pull; null for none.
    /// </summary>
    private static EndRule? Ending(ChapterInputs inputs, ChapterPlaces places)
    {
        if (inputs.Heading is { Home: false } heading && !string.Equals(heading.System, inputs.System, StringComparison.OrdinalIgnoreCase))
        {
            return new EndRule(PlannedPurpose.Leg, ChapterRules.Pull, ChapterArea.NotSearched, _ => true)
            {
                Asked = new AdventureTrigger { Kind = TriggerKind.Arrive, System = heading.System, SystemAddress = heading.SystemAddress },
                AskedAt = heading.Position,
            };
        }

        if (inputs is { FinaleChapter: > 1, Destination: not null, Position: { } here } && places.DestinationAt is { } there
            && here.DistanceTo(there) is var away && away > inputs.Reach)
        {
            return new EndRule(PlannedPurpose.Closer, ChapterRules.Finale, places.Pull, position => position.DistanceTo(there) < away);
        }

        if (inputs.Band is { } band && places.InBand.State != ChapterAreaState.NoneFound)
        {
            return new EndRule(PlannedPurpose.Band, ChapterRules.Band, places.InBand, position =>
                position.DistanceTo(StarPosition.Origin) is var fromSol && fromSol >= band.Min && fromSol <= band.Max);
        }

        if (inputs.Leg is { Arrived: false } leg && inputs.Position is { } start
            && (places.Pull.State == ChapterAreaState.Listed || leg is { Asked: true, Home: false }))
        {
            var from = start.DistanceTo(leg.There);

            return new EndRule(PlannedPurpose.Leg, leg.Home ? ChapterRules.HomeLeg : ChapterRules.Pull, places.Pull, position =>
                leg is { Asked: true, Home: false }
                    ? position.DistanceTo(leg.There) <= leg.Within
                    : ChapterPull.Meets(from, position.DistanceTo(leg.There), inputs.Reach, leg.Within));
        }

        return null;
    }

    private sealed record Item(PlannedPurpose Purpose, DrawnActivity? Slot)
    {
        /// <summary>For a story stop: whether the chapter needs it to reach <see cref="FewestObjectives"/>.</summary>
        public bool Needed { get; init; }
    }

    private sealed record EndRule(PlannedPurpose Purpose, string Rule, ChapterArea Area, Func<StarPosition, bool> Meets)
    {
        /// <summary>The arrival in the system a changed objective asks for, which is not held to the reach; null for any other rule.</summary>
        public AdventureTrigger? Asked { get; init; }

        public StarPosition AskedAt { get; init; }
    }

    /// <summary>A place a step may go to, with what the draw weighs it by.</summary>
    private sealed record Stop(AdventureTrigger Trigger, StarPosition Position, IReadOnlyList<string> Tags, StationSummary? Station = null, bool Mission = false)
    {
        public string System => Trigger.System!;
    }

    /// <summary>One plan being built: where the Commander is after each step, and what has been used.</summary>
    private sealed class Planning(ChapterInputs inputs, ChapterPlaces places, Random32 random)
    {
        private readonly HashSet<string> bodies = new(StringComparer.OrdinalIgnoreCase);

        private string? system = inputs.System;

        private StarPosition position = inputs.Position!.Value;

        private string? flown;

        private double travelled;

        private bool missionUsed;

        public (List<AdventureTrigger>? Steps, ChapterPlanRefusal? Refusal) Activity(DrawnActivity slot)
        {
            var activity = new AdventureTrigger
            {
                Kind = slot.Kind,
                Count = slot.Count,
                MissionFamily = slot.Family,
                Organic = slot.Organic,
                Event = slot.Event,
                Where = slot.Where,
                Faction = slot.Faction,
                System = slot.System,
                SystemAddress = slot.SystemAddress,
            };

            if (slot.Kind is TriggerKind.Map or TriggerKind.Signal or TriggerKind.Organic)
            {
                if (Supplied(slot) is { } supplied)
                {
                    Move(supplied);
                    activity = activity with { System = supplied.System, SystemAddress = supplied.Trigger.SystemAddress };
                }

                return ([activity], null);
            }

            var (rule, what, tag, docksOnly) = slot switch
            {
                { Kind: TriggerKind.OnFootBond } => (ChapterRules.Frontline, "a station with Frontline Solutions in a system at war, for a kill bond on foot", ChapterTags.Frontline, true),
                { Kind: TriggerKind.Bond or TriggerKind.Conflict } => (ChapterRules.War, "a system at war, for a conflict", ChapterTags.War, false),
                { Kind: TriggerKind.Mission } when MissionFamilies.IsIllegal(slot.Family) || CrimeObjectives.IsFamily(slot.Family) => (ChapterRules.Anarchy, "an Anarchy system, for an illegal mission", ChapterTags.Anarchy, false),
                { PlaceBefore: true } => (ChapterRules.PlaceBefore, "a place before the activity", (string?)null, false),
                _ => (null, null, null, false),
            };

            if (rule is null)
            {
                return ([activity], null);
            }

            var candidates = docksOnly ? Docks() : Arrivals().Concat(Docks());
            var (host, refusal) = Draw(candidates.Where(stop => tag is null || stop.Tags.Contains(tag)), rule!, what!);

            if (host is null)
            {
                return (null, refusal);
            }

            Move(host);

            return ([host.Trigger, activity], null);
        }

        /// <summary>A stop at one of the sights; when none is within reach, first the stops on the way to the nearest, each the listed place within reach nearest it.</summary>
        public (List<AdventureTrigger>? Steps, ChapterPlanRefusal? Refusal) Sight()
        {
            var steps = new List<AdventureTrigger>();

            if (places.Sights.All(sight => position.DistanceTo(sight.Position) > inputs.Reach))
            {
                var target = places.Sights.MinBy(sight => position.DistanceTo(sight.Position))!.Position;

                for (var hop = 0; hop < MostSightHops && position.DistanceTo(target) > inputs.Reach; hop++)
                {
                    var left = position.DistanceTo(target);
                    var way = Arrivals().Concat(Lands())
                        .Where(stop => !places.Sights.Any(sight => AtSight(sight, stop.Trigger)) && !Closed(stop.System) && Fresh(stop) && Within(stop) && InTravel(stop) && stop.Position.DistanceTo(target) < left)
                        .MinBy(stop => stop.Position.DistanceTo(target));

                    if (way is null)
                    {
                        break;
                    }

                    Move(way);
                    steps.Add(way.Trigger);
                }
            }

            var sights = Arrivals().Concat(Lands()).Concat(Scans()).Where(stop => places.Sights.Any(sight => AtSight(sight, stop.Trigger)));
            var (stop, refusal) = Draw(sights, ChapterRules.Sight, "the system of one of the chapter's sights");

            if (stop is null)
            {
                return (null, refusal);
            }

            Move(stop);
            steps.Add(stop.Trigger);

            return (steps, null);
        }

        public (List<AdventureTrigger>? Steps, ChapterPlanRefusal? Refusal) Stop(bool needed)
        {
            var candidates = Arrivals().Concat(Docks()).Concat(Lands()).Concat(Scans()).Concat(missionUsed ? [] : MissionStops()).Concat(inputs.Travels ? Sites() : [])
                .Where(stop => !places.Sights.Any(sight => AtSight(sight, stop.Trigger)));
            var (stop, refusal) = Draw(candidates, ChapterRules.Stop, "a place for a story stop");

            if (stop is null)
            {
                return (null, needed ? refusal : null);
            }

            Move(stop);
            missionUsed |= stop.Mission;

            return ([stop.Trigger], null);
        }

        /// <summary>The board into the ship that can make the trip; later hops are measured from where it is parked, when that is known.</summary>
        public AdventureTrigger Swap(AdventureTrigger board)
        {
            if (places.SwapAt is { } parked)
            {
                system = inputs.Beacon?.SwapSystem;
                position = parked;
            }

            return Goal(board);
        }

        public AdventureTrigger Goal(AdventureTrigger goal)
        {
            if (goal is { Kind: TriggerKind.Board, ShipType: { } symbol })
            {
                flown = symbol;
            }

            return goal;
        }

        public (List<AdventureTrigger>? Steps, ChapterPlanRefusal? Refusal) End(EndRule end)
        {
            if (end.Asked is { } asked)
            {
                Move(new Stop(asked, end.AskedAt, TagsOf(asked.System!)));

                return ([asked], null);
            }

            var listed = end.Area.Stations.Select(DockAt).Concat(end.Area.Bodies.Select(body => BodyStop(TriggerKind.Land, body)));
            var candidates = listed.OfType<Stop>().Concat(Arrivals()).Concat(Docks()).Concat(Lands())
                .Where(stop => end.Meets(stop.Position))
                .DistinctBy(stop => (stop.Trigger.Kind, stop.System, stop.Trigger.MarketId, stop.Trigger.BodyId));
            var what = end.Purpose switch
            {
                PlannedPurpose.Band => "a place inside the Sol band",
                PlannedPurpose.Closer => "a place closer to the finale's destination than the chapter's start",
                _ => "a place far enough toward the chapter's target",
            };
            var (stop, refusal) = Draw(candidates, end.Rule, what);

            if (stop is null)
            {
                return (null, refusal);
            }

            Move(stop);

            return ([stop.Trigger], null);
        }

        /// <summary>The step back to the base, when the chapter is held to one and its last stop is beyond the near-here reach of it; otherwise null.</summary>
        public AdventureTrigger? BackToBase()
        {
            if (inputs.Base is null || places.Base is not { } home || string.Equals(system, home.System, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (home.Position is { } at && position.DistanceTo(at) <= ShipBases.Near(inputs.Ship.MaxJumpRange))
            {
                return null;
            }

            return home is { Station: { } station, MarketId: { } market }
                ? new AdventureTrigger { Kind = TriggerKind.Dock, System = home.System, SystemAddress = home.SystemAddress, Station = station, MarketId = market }
                : new AdventureTrigger { Kind = TriggerKind.Arrive, System = home.System, SystemAddress = home.SystemAddress };
        }

        /// <summary>The system a map, signal or organic activity is held to: one whose gathered supply meets its count; null for anywhere.</summary>
        private Stop? Supplied(DrawnActivity slot)
        {
            var supplied = places.Systems
                .Where(found => Supply(found, slot.Kind) >= slot.Count && found.Position is not null)
                .Select(found => new Stop(new AdventureTrigger { Kind = TriggerKind.Arrive, System = found.Name, SystemAddress = found.Address }, found.Position!.Value, TagsOf(found.Name)))
                .Where(stop => !Closed(stop.System) && Within(stop) && InTravel(stop))
                .ToList();

            return supplied.Count == 0 ? null : Pick(supplied);
        }

        private static int? Supply(ChapterSystem found, TriggerKind kind) => kind switch
        {
            TriggerKind.Map => found.Mappable,
            TriggerKind.Signal => found.Signals,
            _ => found.Species,
        };

        /// <summary>
        /// One of <paramref name="candidates"/> that passes every place rule, drawn by weight; or the refusal naming
        /// <paramref name="rule"/> when none matched it, or the place rule that removed the last of them.
        /// </summary>
        private (Stop? Stop, ChapterPlanRefusal? Refusal) Draw(IEnumerable<Stop> candidates, string rule, string what)
        {
            var left = candidates.ToList();

            if (left.Count == 0)
            {
                return (null, new ChapterPlanRefusal(rule, $"No place is {what}."));
            }

            var filters = new (string Rule, string Why, Func<Stop, bool> Keeps)[]
            {
                (ChapterRules.Permit, "needs a permit the Commander may not hold or is a starter system", stop => !Closed(stop.System)),
                (ChapterRules.ClosedMarket, "docks at a station whose docks are closed", stop => stop.Trigger.Kind != TriggerKind.Dock || !ClosedMarket(stop.Trigger.MarketId)),
                (ChapterRules.Refused, "is the place of the objective refused", stop => !Avoided(stop.Trigger)),
                (ChapterRules.Pad, "docks at a station with no pad for the ship", stop => stop.Station is null || inputs.NeedsPad(flown) is not { } pad || stop.Station.Admits(pad)),
                (rule, "arrives in the system the Commander is already in, or uses a body already used", stop => Fresh(stop)),
                (ChapterRules.Hop, $"is beyond the reach of {inputs.Reach.ToString("0", CultureInfo.InvariantCulture)} light years from the stop before", Within),
                (ChapterRules.Hop, $"takes the chapter's travel past {inputs.Travel?.ToString("0", CultureInfo.InvariantCulture)} light years", InTravel),
            };

            foreach (var (filter, why, keeps) in filters)
            {
                left = [.. left.Where(keeps)];

                if (left.Count == 0)
                {
                    return (null, new ChapterPlanRefusal(filter, $"Every place that is {what} {why}."));
                }
            }

            if (inputs.Tags.Count > 0 && left.Where(stop => inputs.Tags.All(stop.Tags.Contains)).ToList() is { Count: > 0 } tagged)
            {
                left = tagged;
            }

            return (Pick(left), null);
        }

        private Stop Pick(List<Stop> stops) => stops[random.Weighted([.. stops.Select(Weight)])];

        private double Weight(Stop stop)
        {
            var weight = 1.0;

            if (Notable(stop))
            {
                weight *= NotableFactor;
            }

            if (!places.WasVisited(stop.System))
            {
                weight *= UnvisitedFactor;
            }

            if (stop.Mission)
            {
                weight *= MissionFactor;
            }

            if (places.WasUsed(stop.System))
            {
                weight *= ChapterDraw.RecentFactor;
            }

            return weight;
        }

        private bool Notable(Stop stop) =>
            places.Notable.Concat(places.Sights).Any(place => AtSight(place, stop.Trigger))
            || places.Sites.Any(site => site.SystemAddress == stop.Trigger.SystemAddress || string.Equals(site.System, stop.System, StringComparison.OrdinalIgnoreCase));

        private bool Fresh(Stop stop) =>
            stop.Trigger.Kind switch
            {
                TriggerKind.Arrive => !string.Equals(stop.System, system, StringComparison.OrdinalIgnoreCase),
                TriggerKind.Land or TriggerKind.Scan or TriggerKind.Site => !bodies.Contains(BodyKey(stop.Trigger)),
                _ => true,
            };

        private bool Within(Stop stop) =>
            string.Equals(stop.System, system, StringComparison.OrdinalIgnoreCase) || position.DistanceTo(stop.Position) <= inputs.Reach;

        private bool InTravel(Stop stop) =>
            inputs.Travel is not { } most || string.Equals(stop.System, system, StringComparison.OrdinalIgnoreCase) || travelled + position.DistanceTo(stop.Position) <= most;

        private bool Closed(string name) =>
            PermitSystemTable.ClosedTo(name, inputs.Ranks, () => places.WasVisited(name));

        /// <summary>Whether a step goes where an avoided step went: the same system for an arrive, station for a dock, or body.</summary>
        private bool Avoided(AdventureTrigger trigger) =>
            inputs.Avoid.Any(avoided => avoided.Kind switch
            {
                TriggerKind.Arrive => trigger.Kind == TriggerKind.Arrive && string.Equals(trigger.System, avoided.System, StringComparison.OrdinalIgnoreCase),
                TriggerKind.Dock => trigger.Kind == TriggerKind.Dock && trigger.MarketId is { } market && market == avoided.MarketId,
                TriggerKind.Land or TriggerKind.Scan or TriggerKind.Site => trigger.Kind is TriggerKind.Land or TriggerKind.Scan or TriggerKind.Site && BodyKey(trigger) == BodyKey(avoided),
                _ => false,
            });

        private bool ClosedMarket(long? market) =>
            market is { } id && (id == inputs.ClosedMarketId || places.ClosedMarkets.Contains(id));

        private void Move(Stop stop)
        {
            if (!string.Equals(stop.System, system, StringComparison.OrdinalIgnoreCase))
            {
                travelled += position.DistanceTo(stop.Position);
            }

            system = stop.System;
            position = stop.Position;

            if (stop.Trigger.Kind is TriggerKind.Land or TriggerKind.Scan or TriggerKind.Site)
            {
                bodies.Add(BodyKey(stop.Trigger));
            }
        }

        private static string BodyKey(AdventureTrigger trigger) => $"{trigger.SystemAddress?.ToString(CultureInfo.InvariantCulture) ?? trigger.System}|{trigger.BodyId?.ToString(CultureInfo.InvariantCulture) ?? trigger.Body}";

        private IEnumerable<Stop> Arrivals() =>
            places.Systems
                .Where(found => found.Position is not null)
                .Select(found => (found.Name, found.Address, Position: found.Position!.Value))
                .Concat(places.Stations.Where(station => station.Position is not null).Select(station => (Name: station.SystemName, Address: station.SystemAddress, Position: station.Position!.Value)))
                .Concat(places.Bodies.Where(body => body.Position is not null).Select(body => (Name: body.SystemName, Address: body.SystemAddress, Position: body.Position!.Value)))
                .Concat(places.Sights.Concat(places.Notable).Select(place => (Name: place.System, Address: place.SystemAddress, place.Position)))
                .DistinctBy(found => found.Name, StringComparer.OrdinalIgnoreCase)
                .Select(found => new Stop(new AdventureTrigger { Kind = TriggerKind.Arrive, System = found.Name, SystemAddress = found.Address }, found.Position, TagsOf(found.Name)));

        private IEnumerable<Stop> Docks() => places.Stations.Select(DockAt).OfType<Stop>();

        private Stop? DockAt(StationSummary station) =>
            station is { Position: { } at, MarketId: not null, IsFixed: true }
                ? new Stop(
                    new AdventureTrigger
                    {
                        Kind = TriggerKind.Dock,
                        MarketId = station.MarketId,
                        SystemAddress = station.SystemAddress,
                        System = station.SystemName,
                        Station = station.Name,
                        LargestPad = station.KnownPads.Count > 0 ? station.KnownPads[^1] : null,
                    },
                    at,
                    [.. TagsOf(station.SystemName), .. AdventureResolver.HasFrontlineSolutions(station) && places.System(station.SystemName)?.AtWar == true ? [ChapterTags.Frontline] : Array.Empty<string>()],
                    station)
                : null;

        private IEnumerable<Stop> Lands() => places.Bodies.Select(body => BodyStop(TriggerKind.Land, body)).OfType<Stop>();

        private IEnumerable<Stop> Scans() => places.Bodies.Select(body => BodyStop(TriggerKind.Scan, body)).OfType<Stop>();

        private Stop? BodyStop(TriggerKind kind, BodySummary body) =>
            body is { Position: { } at, BodyId: not null, SystemAddress: not null } && (kind == TriggerKind.Scan || body.IsLandable)
                ? new Stop(new AdventureTrigger { Kind = kind, SystemAddress = body.SystemAddress, BodyId = body.BodyId, System = body.SystemName, Body = body.Name }, at, TagsOf(body.SystemName))
                : null;

        /// <summary>The Guardian sites, tourist beacons and generation ships within reach whose position is known, each as a site step.</summary>
        private IEnumerable<Stop> Sites() =>
            places.Sites
                .Select(site => (Site: site, At: site.Position ?? places.System(site.System)?.Position))
                .Where(found => found.At is not null && (found.Site.Filter != AdventureTrigger.GuardianSite || found.Site.BodyId is not null))
                .Select(found => new Stop(SiteStep(found.Site), found.At!.Value, TagsOf(found.Site.System)));

        private static AdventureTrigger SiteStep(ChapterSite site) => site.Filter switch
        {
            AdventureTrigger.GuardianSite => new AdventureTrigger { Kind = TriggerKind.Site, Filter = site.Filter, SystemAddress = site.SystemAddress, System = site.System, BodyId = site.BodyId, Body = site.Name },
            AdventureTrigger.GenerationSite => new AdventureTrigger { Kind = TriggerKind.Site, Filter = site.Filter, SystemAddress = site.SystemAddress, System = site.System, SiteName = site.Name },
            _ => new AdventureTrigger { Kind = TriggerKind.Site, Filter = site.Filter, SystemAddress = site.SystemAddress, System = site.System },
        };

        private IEnumerable<Stop> MissionStops() =>
            places.Missions
                .Where(stop => stop.Position is not null)
                .Select(stop => new Stop(
                    stop is { Station: { } station, MarketId: { } market }
                        ? new AdventureTrigger { Kind = TriggerKind.Dock, System = stop.System, SystemAddress = stop.SystemAddress, Station = station, MarketId = market }
                        : new AdventureTrigger { Kind = TriggerKind.Arrive, System = stop.System, SystemAddress = stop.SystemAddress },
                    stop.Position!.Value,
                    TagsOf(stop.System),
                    places.Stations.FirstOrDefault(listed => stop.MarketId is not null && listed.MarketId == stop.MarketId),
                    Mission: true));

        private IReadOnlyList<string> TagsOf(string name) =>
            places.System(name) is { } found
                ? [.. string.Equals(found.Government, "Anarchy", StringComparison.OrdinalIgnoreCase) ? [ChapterTags.Anarchy] : Array.Empty<string>(), .. found.AtWar ? [ChapterTags.War] : Array.Empty<string>()]
                : [];

        private static bool AtSight(NotablePlace sight, AdventureTrigger trigger) =>
            sight.SystemAddress is { } address
                ? trigger.SystemAddress == address
                : string.Equals(sight.System, trigger.System, StringComparison.OrdinalIgnoreCase);
    }
}
