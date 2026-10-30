using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A fixed seed draws the same slots, an event slot included, so a change to the weighted pick that changes the order or number of random draws fails here.</summary>
public sealed class AFixedSeedDrawsTheSameSlotsTests
{
    private const string EventRows = "journal\tDocked\tyes\t\t\tThe ship docked.\n"
        + "journal\tAsteroidCracked\tyes\tMiningKit\tMining\tA motherlode asteroid was broken up.\n"
        + "journal\tBounty\tyes\tDanger\tCombat\tA bounty was awarded.\n"
        + "d47\tToolRun\tyes\t\t\tA tool of the assistant ran.\n";

    private const string FieldRows = "Docked\tStationType\tstring\t\tyes\n"
        + "Docked\tStationType\tstring\tOrbis\tyes\n"
        + "Docked\tStationType\tstring\tCoriolis\tyes\n"
        + "ToolRun\tTool\tstring\tadd_log_entry\tyes\n"
        + "ToolRun\tTool\tstring\tset_volume\tyes\n";

    private static readonly StepEventCatalogue Catalogue = StepEventCatalogue.Parse(EventRows, FieldRows);

    private static readonly DrawableTool[] Tools = [new("add_log_entry", "log this"), new("set_volume", "set volume to forty percent")];

    private static readonly string[] Recent = [RefusedActivities.Key(TriggerKind.Event, null, "ToolRun", "set_volume") ?? string.Empty];

    private static string[] Draw(uint seed) =>
    [
        .. ChapterDraw.Draw(
                CareerWeights.Even, RankState.Empty, new ShipLoadout { Type = "cobramkiii", CargoCapacity = 64, Modules = [] }, odyssey: true, Recent, null,
                comfort: false, CareerStatistics.Empty, 0, seed, events: Catalogue, tools: Tools)
            .Select(slot => $"{slot.Kind}|{slot.Family}|{slot.Count}|{slot.Organic}|{slot.PlaceBefore}|{slot.Event}|{string.Join(",", slot.Where.Select(pair => $"{pair.Field}={pair.Value}"))}|{slot.Say}"),
    ];

    [Fact]
    public void ASeedWhoseEventSlotRunsAToolDrawsTheSameSlots() =>
        Assert.Equal(
            [
                "Salvage||3||False|||",
                "Mission|Mission_OnFoot_Sabotage|2||True|||",
                "Event||1||False|ToolRun|Tool=add_log_entry|log this",
            ],
            Draw(6));

    [Fact]
    public void ASeedWhoseEventSlotNamesAFieldDrawsTheSameSlots() =>
        Assert.Equal(
            [
                "Mission|Mission_Smuggle|3||True|||",
                "Discover||3||False|||",
                "Event||2||False|Docked|StationType=Orbis|",
            ],
            Draw(53));
}
