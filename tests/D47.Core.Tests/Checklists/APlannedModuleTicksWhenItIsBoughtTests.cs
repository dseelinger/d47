using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// A module line ticks when the planned module is fitted in its slot or a matching one is in storage, not
/// when the slot merely holds something (#1828).
/// </summary>
public class APlannedModuleTicksWhenItIsBoughtTests
{
    private static string Loadout(string engine) =>
        $$"""
        { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
          "Modules":[ { "Slot":"MainEngines", "Item":"{{engine}}", "On":true } ] }
        """;

    private const string StoredThrusters =
        """
        { "timestamp":"2026-08-16T10:30:00Z", "event":"StoredModules", "StarSystem":"Deciat",
          "MarketID":1, "StationName":"Garay Terminal",
          "Items":[ { "Name":"$int_engine_size5_class5_name;", "Name_Localised":"Thrusters",
                      "StarSystem":"Deciat", "TransferCost":21000 } ] }
        """;

    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistItem Item(ChecklistIntent intent) => new()
    {
        Key = ChecklistKeys.For(intent),
        Scope = ChecklistScope.Ship(12),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = "a planned module",
        Intent = intent,
        Hull = "krait_mkii",
    };

    private static ChecklistIntent FiveAThrusters(string slot = "MainEngines") =>
        new(ChecklistIntentKind.Module, slot) { Module = "Thrusters", Variant = "int_engine_size5_class5" };

    [Fact]
    public void ADifferentModuleInThePlannedSlotLeavesTheLineOpen()
    {
        var verdict = ChecklistEvaluator.Evaluate(Item(FiveAThrusters()), State(Loadout("int_engine_size5_class1")));

        Assert.Equal(ChecklistState.Open, verdict!.Value.State);
        Assert.Equal("5E Thrusters is fitted rather than 5A Thrusters.", verdict.Value.Reason);
    }

    [Fact]
    public void ThePlannedVariantFittedTicksTheLine()
    {
        var verdict = ChecklistEvaluator.Evaluate(Item(FiveAThrusters()), State(Loadout("int_engine_size5_class5")));

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
    }

    [Fact]
    public void ALineNamingOnlyTheModuleIsMetByAnySizeAndRating()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Module, "MainEngines") { Module = "Thrusters" };

        var verdict = ChecklistEvaluator.Evaluate(Item(intent), State(Loadout("int_engine_size5_class1")));

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
    }

    [Fact]
    public void AMatchingModuleInStorageTicksTheLineWhileTheSlotHoldsAnother()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(FiveAThrusters()),
            State(Loadout("int_engine_size5_class1"), StoredThrusters));

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
        Assert.Equal("5A Thrusters is bought and stored at Garay Terminal in Deciat.", verdict.Value.Reason);
    }

    [Fact]
    public void AStoredModuleOfAnotherRatingDoesNotTickAVariant()
    {
        const string stored =
            """
            { "timestamp":"2026-08-16T10:30:00Z", "event":"StoredModules", "StarSystem":"Deciat",
              "MarketID":1, "StationName":"Garay Terminal",
              "Items":[ { "Name":"$int_engine_size5_class3_name;", "Name_Localised":"Thrusters",
                          "StarSystem":"Deciat" } ] }
            """;

        var verdict = ChecklistEvaluator.Evaluate(
            Item(FiveAThrusters()),
            State(Loadout("int_engine_size5_class1"), stored));

        Assert.Equal(ChecklistState.Open, verdict!.Value.State);
    }

    [Fact]
    public void AStoredModuleTicksAModuleLineAndStillReadsElsewhereForABlueprintLine()
    {
        var state = State(Loadout("int_engine_size5_class1"), StoredThrusters);

        var module = ChecklistEvaluator.Evaluate(Item(FiveAThrusters("Slot02_Size4")), state);
        var blueprint = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot02_Size4") { Detail = "Thrusters" }),
            state);

        Assert.Equal(ChecklistState.Done, module!.Value.State);
        Assert.Contains("bought and stored", module.Value.Reason, StringComparison.Ordinal);
        Assert.Contains("Deciat", module.Value.Reason, StringComparison.Ordinal);
        Assert.Equal(ChecklistState.Elsewhere, blueprint!.Value.State);
    }

    [Fact]
    public void ALineNamingNoModuleIsMetByWhateverIsFitted()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Module, "MainEngines")),
            State(Loadout("int_engine_size5_class1")));

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
    }
}
