using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A voice answer names at most three modules; the content the model reads stays whole.</summary>
public class ASpokenAnswerNamesAtMostThreeModulesTests
{
    private const string Commander =
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private static readonly string[] Items =
    [
        "int_hyperdrive_size5_class5",
        "int_engine_size5_class5",
        "int_powerplant_size6_class5",
        "int_shieldgenerator_size5_class5",
        "int_lifesupport_size4_class1",
        "int_sensors_size3_class1",
        "int_powerdistributor_size4_class1",
        "int_fuelscoop_size5_class5",
    ];

    private static readonly string Unpowered =
        $$"""
        {"timestamp":"3311-01-01T00:02:00Z","event":"Loadout","Ship":"fixturehull","ShipID":4,"ShipName":"Fixture",
         "Modules":[{{string.Join(",", Items.Select((item, index) =>
             $$"""{"Slot":"Slot0{{index + 1}}_Size5","Item":"{{item}}","On":false,"Health":1.0}"""))}}]}
        """;

    private static readonly string[] StoredNames =
        ["Beam Laser", "Shield Generator", "Frame Shift Drive", "Power Plant", "Thrusters", "Cargo Rack", "Life Support"];

    private static readonly string Stored =
        $$"""
        {"timestamp":"3311-01-01T00:02:00Z","event":"StoredModules","StationName":"Fixture Outpost",
         "StarSystem":"Fixture Nebula Point",
         "Items":[{{string.Join(",", StoredNames.Select((name, index) =>
             $$"""{"Name":"$mod{{index}}_name;","Name_Localised":"{{name}}","StarSystem":"{{
                 (index < 5 ? "Fixture Depot " + (char)('A' + index % 3) : "Fixture Nebula Point")}}","TransferCost":1000}"""))}}]}
        """;

    private static readonly string[] Modifications =
        ["suitmod_alpha", "suitmod_bravo", "suitmod_charlie", "weaponmod_delta", "weaponmod_echo", "weaponmod_foxtrot"];

    private const string OnFoot =
        """
        {"timestamp":"3311-01-01T00:02:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class5",
         "LoadoutName":"Ground",
         "SuitMods":["suitmod_alpha","suitmod_bravo","suitmod_charlie"],
         "Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":9,"ModuleName":"wpn_m_assaultrifle_kinetic_fauto",
                     "Class":5,"WeaponMods":["weaponmod_delta","weaponmod_echo","weaponmod_foxtrot"]}]}
        """;

    private static GameStateStore Store(params string[] events)
    {
        var store = new GameStateStore();

        foreach (var line in events)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static async Task<ToolResult> Call(CapabilityDescriptor capability, string tool, string? json = null)
    {
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            tool,
            ToolArguments.FromJson(json),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result;
    }

    private static int Named(string said, IEnumerable<string> names) =>
        names.Count(name => said.Contains(name, StringComparison.Ordinal));

    [Fact]
    public async Task MyShipNamesThreeUnpoweredModulesAndCountsTheRest()
    {
        var store = Store(Commander, Unpowered);
        var names = Items.Select(item => EliteSpecifications.ModuleName(item)!).ToList();

        var result = await Call(JournalCapability.Create(store), "get_ship");

        Assert.True(Named(result.Spoken, names) == 3, result.Spoken);
        Assert.Contains("8 modules fitted, 0 engineered.", result.Spoken, StringComparison.Ordinal);
        Assert.Contains("8 unpowered:", result.Spoken, StringComparison.Ordinal);
        Assert.Contains(", and 5 more.", result.Spoken, StringComparison.Ordinal);
        Assert.Contains("Asset Mgmt › Ships", result.Spoken, StringComparison.Ordinal);
        Assert.Equal(8, Named(result.Content, names));
    }

    [Fact]
    public async Task StoredModulesNameThreeAndCountSystemsAndTransit()
    {
        var store = Store(Commander, Stored);

        var result = await Call(JournalCapability.Create(store), "get_stored_modules");

        Assert.True(Named(result.Spoken, StoredNames) <= 3, result.Spoken);
        Assert.Contains("7 modules in storage", result.Spoken, StringComparison.Ordinal);
        Assert.Contains("in 4 systems:", result.Spoken, StringComparison.Ordinal);
        Assert.Contains(", and 4 more.", result.Spoken, StringComparison.Ordinal);
        Assert.Equal(7, Named(result.Content, StoredNames));
    }

    [Fact]
    public async Task StoredModulesNameTheOnesWhereTheCommanderIsFirst()
    {
        var store = Store(Commander, Stored);

        var result = await Call(JournalCapability.Create(store), "get_stored_modules");

        Assert.True(result.Spoken.Contains("Cargo Rack, Life Support and", StringComparison.Ordinal), result.Spoken);
    }

    [Fact]
    public async Task EngineeringNamesThreeEngineeredModulesAndCountsTheRest()
    {
        var store = Store(Commander, EngineeringCapabilityTests.Loadout);

        var result = await Call(EngineeringCapability.Create(() => store.Active), "get_module_engineering");

        var lines = result.Content.Split('\n').Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("  ", StringComparison.Ordinal))
            .Select(line => line.Trim().Split(" — ")[0])
            .ToList();

        Assert.Equal(5, lines.Count);
        Assert.Equal(3, Named(result.Spoken, lines));
        Assert.Contains("5 of 6 modules engineered", result.Spoken, StringComparison.Ordinal);
        Assert.Contains(", and 2 more.", result.Spoken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEngineeringMissListsAtMostThreeModules()
    {
        var store = Store(Commander, EngineeringCapabilityTests.Loadout);

        var result = await Call(
            EngineeringCapability.Create(() => store.Active), "get_module_engineering", """{"module":"nothing"}""");

        Assert.Contains("Nothing fitted matches 'nothing'.", result.Spoken, StringComparison.Ordinal);
        Assert.Contains(", and 2 more.", result.Spoken, StringComparison.Ordinal);
        Assert.DoesNotContain(", and 2 more", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSuitNamesThreeModificationsAcrossTheWholeAnswer()
    {
        var store = Store(Commander, OnFoot);
        var names = Modifications.Select(symbol => new FittedModification(symbol).Speak()).ToList();

        var result = await Call(OnFootCapability.Create(() => store.Active), "get_on_foot_loadout");

        Assert.Equal(3, Named(result.Spoken, names));
        Assert.Contains("Suit: 3 modifications.", result.Spoken, StringComparison.Ordinal);
        Assert.Contains("PrimaryWeapon1:", result.Spoken, StringComparison.Ordinal);
        Assert.Contains("3 modifications.", result.Spoken, StringComparison.Ordinal);
        Assert.Equal(6, Named(result.Content, names));
    }

    [Fact]
    public void TheSpokenListSaysHowManyMoreThereAre()
    {
        Assert.Equal("a", SpokenList.Names(["a"]));
        Assert.Equal("a and b", SpokenList.Names(["a", "b"]));
        Assert.Equal("a, b and c", SpokenList.Names(["a", "b", "c"]));
        Assert.Equal("a, b and c, and 2 more", SpokenList.Names(["a", "b", "c", "d", "e"]));
    }

    [Fact]
    public void ThePromptTellsTheModelToNameAtMostThreeModules()
    {
        var block = new PromptAssembly { Persona = "You are Warden." }.RenderCachedSystemBlock();

        Assert.Contains("name at most three modules and say how many more there are", block, StringComparison.Ordinal);
    }
}
