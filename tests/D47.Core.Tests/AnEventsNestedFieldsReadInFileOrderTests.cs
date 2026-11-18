using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>Every field carries nested fields in file order (#2510).</summary>
public class AnEventsNestedFieldsReadInFileOrderTests
{
    private const string Docked =
        """{"timestamp":"2026-09-29T21:01:23Z","event":"Docked","StationName":"BNH-T2F","StationFaction":{"Name":"FleetCarrier"},"StationServices":["dock","autodock","commodities"],"DistFromStarLS":213.287350,"LandingPads":{"Small":4,"Medium":4,"Large":8}}""";

    private const string Loadout =
        """{"timestamp":"2026-09-29T21:01:23Z","event":"Loadout","Ship":"sidewinder","Modules":[{"Slot":"A","Item":"x"},{"Slot":"TinyHardpoint2","Item":"hpt","Engineering":{"EngineerID":1,"Modifiers":[{"Label":"Mass","Value":1.0,"OriginalValue":0.5}]}}]}""";

    private static IReadOnlyList<PlumbingField> Fields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var kind = document.RootElement.GetProperty("event").GetString()!;
        var log = new JournalLog();

        log.Add([new JournalEvent(DateTimeOffset.Parse("2026-09-29T21:01:23Z"), kind, document.RootElement.Clone())]);

        return EventReadings.For(Assert.Single(log.Read(noise: true))).Plumbing;
    }

    private static PlumbingField Field(IReadOnlyList<PlumbingField> fields, string name) =>
        Assert.Single(fields, field => field.Name == name);

    [Fact]
    public void AnObjectOfPlainValuesStartsOpenAndKeepsItsRawText()
    {
        var pads = Field(Fields(Docked), "LandingPads");

        Assert.Equal(PlumbingKind.Object, pads.Kind);
        Assert.True(pads.StartsOpen);
        Assert.Equal("""{"Small":4,"Medium":4,"Large":8}""", pads.Value);
        Assert.Equal(["Small", "Medium", "Large"], pads.Fields.Select(f => f.Name));
        Assert.Equal(["4", "4", "8"], pads.Fields.Select(f => f.Value));
        Assert.All(pads.Fields, f => Assert.Equal(PlumbingKind.Plain, f.Kind));
    }

    [Fact]
    public void AListOfPlainValuesReadsAsItsItemsJoinedByCommas()
    {
        var services = Field(Fields(Docked), "StationServices");

        Assert.Equal(PlumbingKind.Plain, services.Kind);
        Assert.Equal("dock, autodock, commodities", services.Value);
        Assert.Empty(services.Fields);
        Assert.False(services.StartsOpen);
    }

    [Fact]
    public void ANumberKeepsTheDigitsTheJournalWrote()
    {
        Assert.Equal("213.287350", Field(Fields(Docked), "DistFromStarLS").Value);
    }

    [Fact]
    public void AnEngineeredModuleReadsToDepthFiveWithTags()
    {
        var modules = Field(Fields(Loadout), "Modules");

        Assert.Equal(PlumbingKind.List, modules.Kind);
        Assert.False(modules.StartsOpen);
        Assert.Equal(["0", "1"], modules.Fields.Select(f => f.Name));

        var module = modules.Fields[1];

        Assert.Equal("TinyHardpoint2", module.Tag);
        Assert.Equal("A", modules.Fields[0].Tag);

        var modifiers = Field(Field(module.Fields, "Engineering").Fields, "Modifiers");
        var mass = modifiers.Fields[0];

        Assert.Equal("Mass", mass.Tag);
        Assert.Equal("1.0", Field(mass.Fields, "Value").Value);
        Assert.Equal("0.5", Field(mass.Fields, "OriginalValue").Value);
        Assert.True(mass.StartsOpen);
    }

    [Fact]
    public void AnObjectWithAGroupInsideDoesNotStartOpen()
    {
        var engineering = Field(Field(Fields(Loadout), "Modules").Fields[1].Fields, "Engineering");

        Assert.Equal(PlumbingKind.Object, engineering.Kind);
        Assert.False(engineering.StartsOpen);
        Assert.Null(engineering.Tag);
    }

    [Fact]
    public void AnEmptyListOrObjectIsEmpty()
    {
        var fields = Fields("""{"timestamp":"2026-09-29T21:01:23Z","event":"Missions","Failed":[  ],"Other":{}}""");

        var failed = Field(fields, "Failed");

        Assert.Equal(PlumbingKind.Empty, failed.Kind);
        Assert.Equal("empty list", failed.Value);
        Assert.Equal("empty object", Field(fields, "Other").Value);
    }

    [Fact]
    public void AnObjectWithoutAStringFieldHasNoTag()
    {
        var fields = Fields("""{"timestamp":"2026-09-29T21:01:23Z","event":"X","Items":[{"A":1},{"A":2}]}""");

        Assert.All(Field(fields, "Items").Fields, f => Assert.Null(f.Tag));
    }

    [Fact]
    public void AnObjectOfMoreThanFourFieldsDoesNotStartOpen()
    {
        var fields = Fields("""{"timestamp":"2026-09-29T21:01:23Z","event":"X","G":{"a":1,"b":2,"c":3,"d":4,"e":5}}""");

        Assert.False(Field(fields, "G").StartsOpen);
    }
}
