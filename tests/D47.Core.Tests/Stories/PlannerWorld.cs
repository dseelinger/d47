using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;

namespace D47.Core.Tests.Stories;

/// <summary>A small invented galaxy laid along one line for chapter planner tests: the Commander at x = 100, a 50 light year reach.</summary>
internal static class PlannerWorld
{
    public const string Start = "Fixture Prime";

    public const double Reach = 50;

    public static readonly StarPosition Here = new(100, 0, 0);

    public static readonly DrawnActivity Bounty = new(TriggerKind.Bounty, null, 5);

    public static readonly DrawnActivity Codex = new(TriggerKind.Codex, null, 4);

    public static readonly DrawnActivity Salvage = new(TriggerKind.Salvage, null, 3);

    public static ChapterInputs Inputs(params DrawnActivity[] slots) => new()
    {
        System = Start,
        Position = Here,
        Reach = Reach,
        Ship = new ShipLoadout { Type = "python", MaxJumpRange = 20, Modules = [] },
        OwnedHulls = ["python"],
        Credits = 10_000_000,
        Slots = slots,
    };

    public static ChapterSystem Place(string name, long address, double x, string? government = null, bool war = false) =>
        new(name, address, new StarPosition(x, 0, 0)) { Government = government, AtWar = war };

    public static StationSummary Station(string name, string system, long address, long market, double x, bool frontline = false, int large = 2) => new()
    {
        Name = name,
        SystemName = system,
        SystemAddress = address,
        MarketId = market,
        Position = new StarPosition(x, 0, 0),
        Type = "Coriolis",
        SmallPads = 2,
        MediumPads = 2,
        LargePads = large,
        Services = frontline ? [AdventureResolver.FrontlineSolutions] : [],
    };

    public static BodySummary Body(string name, string system, long address, int id, double x) => new()
    {
        Name = name,
        SystemName = system,
        SystemAddress = address,
        BodyId = id,
        Position = new StarPosition(x, 0, 0),
        IsLandable = true,
    };

    /// <summary>Five systems within reach, with three stations and three landable bodies among them, and one system with a station beyond it.</summary>
    public static ChapterPlaces Places() => new()
    {
        Systems =
        [
            Place(Start, 1, 100),
            Place("Fixture Alpha", 2, 110),
            Place("Fixture Beta", 3, 120),
            Place("Fixture Gamma", 4, 90),
            Place("Fixture Delta", 5, 130),
            Place("Fixture Far", 6, 400),
        ],
        Stations =
        [
            Station("Alpha Port", "Fixture Alpha", 2, 200, 110),
            Station("Beta Port", "Fixture Beta", 3, 300, 120),
            Station("Gamma Port", "Fixture Gamma", 4, 400, 90),
            Station("Far Port", "Fixture Far", 6, 600, 400),
        ],
        Bodies =
        [
            Body("Fixture Alpha 1", "Fixture Alpha", 2, 1, 110),
            Body("Fixture Beta 2", "Fixture Beta", 3, 2, 120),
            Body("Fixture Delta 3", "Fixture Delta", 5, 3, 130),
        ],
    };

    public static ChapterPlan Planned(ChapterInputs inputs, ChapterPlaces places, uint seed = 7)
    {
        var result = ChapterPlanner.Plan(inputs, places, seed);

        return result.Plan ?? throw new InvalidOperationException($"No plan: {result.Refusal?.Rule}: {result.Refusal?.Text}");
    }

    public static IEnumerable<AdventureTrigger> Steps(ChapterPlan plan) => plan.Objectives.SelectMany(objective => objective.Steps);

    /// <summary>Every step that puts the Commander in a system, in order.</summary>
    public static IEnumerable<AdventureTrigger> Placed(ChapterPlan plan) =>
        Steps(plan).Where(step => step.Kind is TriggerKind.Arrive or TriggerKind.Dock or TriggerKind.Land or TriggerKind.Scan || (step.IsCounted && step.System is not null));

    /// <summary>The plan's steps as one comparable line.</summary>
    public static string Describe(ChapterPlan plan) =>
        string.Join(" / ", plan.Objectives.Select(objective =>
            $"{objective.Function}: " + string.Join(", ", objective.Steps.Select(step => $"{step.Kind} {step.System} {step.Station}{step.Body} {step.Count}"))));
}
