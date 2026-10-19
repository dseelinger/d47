using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ATechBrokerUnlockIsRememberedTests
{
    private const string Booster = "Guardian FSD Booster";

    private const string ShockCannon = "Shock Cannon (Gimballed, Large)";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static TechBrokerUnlocks Fold(params string[] lines) =>
        lines.Aggregate(TechBrokerUnlocks.Empty, (unlocks, line) => unlocks.Apply(Event(line)));

    private static bool Has(TechBrokerUnlocks unlocks, string name) =>
        unlocks.IsKnownUnlocked(TechBrokerCatalogue.Rows.Single(row => row.Name == name));

    [Fact]
    public void ABrokerEventMarksTheUnlockedRow()
    {
        var unlocks = Fold(
            """{"timestamp":"2026-10-01T00:00:00Z","event":"TechnologyBroker","BrokerType":"guardian","ItemsUnlocked":[{"Name":"Int_GuardianFSDBooster_Size1"},{"Name":"Int_GuardianFSDBooster_Size2"}]}""");

        Assert.True(Has(unlocks, Booster));
        Assert.False(Has(unlocks, ShockCannon));
    }

    [Fact]
    public void AFittedModuleMarksTheUnlockedRow()
    {
        var unlocks = Fold(
            """{"timestamp":"2026-10-01T00:00:00Z","event":"Loadout","Ship":"anaconda","Modules":[{"Slot":"Slot01","Item":"int_guardianfsdbooster_size5","On":true}]}""");

        Assert.True(Has(unlocks, Booster));
    }

    [Fact]
    public void AStoredModuleMarksTheUnlockedRow()
    {
        var unlocks = Fold(
            """{"timestamp":"2026-10-01T00:00:00Z","event":"StoredModules","Items":[{"Name":"$hpt_plasmashockcannon_gimbal_large_name;","Name_Localised":"Shock Cannon"}]}""");

        Assert.True(Has(unlocks, ShockCannon));
        Assert.False(Has(unlocks, "Shock Cannon (Gimballed, Medium)"));
    }

    [Fact]
    public void ARecordedUnlockIsNotInTheNotKnownList()
    {
        var unlocks = Fold(
            """{"timestamp":"2026-10-01T00:00:00Z","event":"TechnologyBroker","BrokerType":"guardian","ItemsUnlocked":[{"Name":"Int_GuardianFSDBooster_Size1"}]}""");

        Assert.DoesNotContain(unlocks.NotKnownToBeUnlocked("guardian"), row => row.Name == Booster);
        Assert.Contains(TechBrokerUnlocks.Empty.NotKnownToBeUnlocked("guardian"), row => row.Name == Booster);
        Assert.All(unlocks.NotKnownToBeUnlocked("human"), row => Assert.Equal("human", row.BrokerType));
    }

    [Fact]
    public void TwoCommandersEachKeepTheirOwnUnlock()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "j/Journal.1.log",
            """
            {"timestamp":"2026-10-01T00:00:00Z","event":"Commander","FID":"F1","Name":"One"}
            {"timestamp":"2026-10-01T00:01:00Z","event":"TechnologyBroker","BrokerType":"guardian","ItemsUnlocked":[{"Name":"Int_GuardianFSDBooster_Size1"}]}
            {"timestamp":"2026-10-02T00:00:00Z","event":"Commander","FID":"F2","Name":"Two"}
            {"timestamp":"2026-10-02T00:01:00Z","event":"StoredModules","Items":[{"Name":"$hpt_plasmashockcannon_gimbal_large_name;"}]}
            """);

        var history = TechBrokerBackfill.FromHistory(files, ["j/Journal.1.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.True(Has(history["F1"], Booster));
        Assert.False(Has(history["F1"], ShockCannon));
        Assert.True(Has(history["F2"], ShockCannon));
        Assert.False(Has(history["F2"], Booster));
    }

    [Fact]
    public void TheUnjoinedRowsAreTheKnownOnes()
    {
        string[] expected =
        [
            "Engineered Detailed Surface Scanner V1",
            "Engineered FSD V1",
            "Engineered Missile Rack V1",
            "Guardian Power Distributor",
            "Javelin (Fighter)",
            "Lance (Fighter)",
            "Modified Gauss Cannon (Fixed, Medium)",
            "Modified Gauss Cannon (Fixed, Small)",
            "Modified Heat Sink Launcher",
            "Modified Mining Laser",
            "Modified Plasma Charger (Fixed, Medium)",
            "Modified Plasma Charger (Fixed, Small)",
            "Modified Shard Cannon (Fixed, Medium)",
            "Modified Shard Cannon (Fixed, Small)",
            "Sirius Modified AX Missile Rack (Large)",
            "Sirius Modified AX Missile Rack (Medium)",
            "Sirius Modified Heat Sink Launcher",
            "Trident (Fighter)",
        ];

        Assert.Equal(expected, TechBrokerCatalogue.Unjoined.Select(row => row.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoSymbolJoinsToTwoRows()
    {
        var symbols = TechBrokerCatalogue.Joined.SelectMany(row => row.Symbols).ToArray();

        Assert.Equal(symbols.Length, symbols.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(TechBrokerCatalogue.Rows.Select(row => row.Name).Distinct().Count(), TechBrokerCatalogue.Rows.Count);
    }
}
