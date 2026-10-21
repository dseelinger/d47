using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests;

/// <summary>A written SLEF loadout reads back through the journal parser as the same ship.</summary>
public class ASlefWriterRoundTripsALoadoutTests
{
    private const string Line = """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "Ship_Localised":"Krait Mk II",
          "ShipID":12, "ShipName":"Nightjar", "ShipIdent":"NJ-01", "HullValue":42000000, "ModulesValue":9100000,
          "HullHealth":0.92, "UnladenMass":321.5, "CargoCapacity":16, "MaxJumpRange":31.25, "Rebuy":2500000,
          "FuelCapacity":{"Main":32.0,"Reserve":0.63},
          "Modules":[
            { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true, "Priority":2, "Health":0.57, "Value":1000,
              "Engineering":{"Engineer":"Felicity Farseer","EngineerID":300100,"BlueprintName":"Engine_Dirty","Level":5,
                "Quality":0.85,"ExperimentalEffect":"special_engine_overloaded","ExperimentalEffect_Localised":"Drag Drives",
                "Modifiers":[
                  {"Label":"Mass","Value":40.0,"OriginalValue":32.0,"LessIsGood":1},
                  {"Label":"DamageType","ValueStr":"$Thermal;","ValueStr_Localised":"Thermal"}]} },
            { "Slot":"Slot01_Size4", "Item":"int_shieldgenerator_size4_class5", "On":false, "Priority":0, "Health":1.0, "Value":500 } ] }
        """;

    private static ShipLoadout Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));
        return ShipLoadout.Unknown.Apply(parsed!);
    }

    [Fact]
    public void TheDataElementReadsBackAsTheSameShip()
    {
        var original = Parse(Line);
        var slef = SlefWriter.Write(original, "1.2.3");

        using var document = JsonDocument.Parse(slef);
        var data = document.RootElement[0].GetProperty("data").Clone();
        var back = ShipLoadout.Unknown.Apply(new JournalEvent(DateTimeOffset.MinValue, "Loadout", data));

        Assert.Equal(original.Modules.Count, back.Modules.Count);
        Assert.Equal(original with { Modules = [] }, back with { Modules = [] });

        for (var at = 0; at < original.Modules.Count; at++)
        {
            Assert.Equal(original.Modules[at] with { Modifiers = [] }, back.Modules[at] with { Modifiers = [] });
            Assert.Equal(original.Modules[at].Modifiers, back.Modules[at].Modifiers);
        }

        Assert.NotEmpty(original.Modules[0].Modifiers);
    }

    [Fact]
    public void TheOutputIsOneElementWithTheHeader()
    {
        using var document = JsonDocument.Parse(SlefWriter.Write(Parse(Line), "1.2.3"));
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var header = root[0].GetProperty("header");

        Assert.Equal("d47", header.GetProperty("appName").GetString());
        Assert.Equal("1.2.3", header.GetProperty("appVersion").GetString());
        Assert.Equal("https://github.com/dseelinger/d47", header.GetProperty("appURL").GetString());
    }

    [Fact]
    public void AnUnengineeredModuleHasNoEngineeringBlockAndANullFieldIsAbsent()
    {
        using var document = JsonDocument.Parse(SlefWriter.Write(Parse(Line), "1.2.3"));
        var modules = document.RootElement[0].GetProperty("data").GetProperty("Modules");

        Assert.True(modules[0].TryGetProperty("Engineering", out _));
        Assert.False(modules[1].TryGetProperty("Engineering", out _));

        var bare = new ShipLoadout { Type = "Anaconda" };
        using var sparse = JsonDocument.Parse(SlefWriter.Write(bare, "1.2.3"));
        var data = sparse.RootElement[0].GetProperty("data");

        Assert.False(data.TryGetProperty("ShipName", out _));
        Assert.False(data.TryGetProperty("FuelCapacity", out _));
        Assert.Equal(0, data.GetProperty("Modules").GetArrayLength());
    }

    [Fact]
    public void HealthAndPriorityAreWrittenInTheJournalsUnits()
    {
        using var document = JsonDocument.Parse(SlefWriter.Write(Parse(Line), "1.2.3"));
        var data = document.RootElement[0].GetProperty("data");
        var engine = data.GetProperty("Modules")[0];

        Assert.Equal(0.92, data.GetProperty("HullHealth").GetDouble(), 3);
        Assert.Equal(0.57, engine.GetProperty("Health").GetDouble(), 3);
        Assert.Equal(2, engine.GetProperty("Priority").GetInt32());
    }
}
