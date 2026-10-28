using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A <c>ModuleBuyAndStore</c> adds the module to storage at once, and the next <c>StoredModules</c>
/// snapshot replaces the list (#1828).
/// </summary>
public class AModuleBoughtIntoStorageIsStoredTests
{
    private const string Docked =
        """
        { "timestamp":"2026-10-04T09:00:00Z", "event":"Docked", "StationName":"Jameson Memorial",
          "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "MarketID":128666762 }
        """;

    private const string Bought =
        """
        { "timestamp":"2026-10-04T09:10:00Z", "event":"ModuleBuyAndStore",
          "BuyItem":"$int_engine_size5_class5_name;", "BuyItem_Localised":"Thrusters",
          "MarketID":128666762, "BuyPrice":5103953, "Ship":"krait_mkii", "ShipID":12 }
        """;

    private static ModuleStore Modules(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-04T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!.Modules;
    }

    [Fact]
    public void APurchaseWithNoSnapshotAfterItIsStoredWhereTheCommanderIsDocked()
    {
        var module = Assert.Single(Modules(Docked, Bought).Modules);

        Assert.Equal("int_engine_size5_class5", module.Symbol);
        Assert.Equal("5A", module.Class);
        Assert.Equal("Shinrarta Dezhra", module.StarSystem);
        Assert.Equal("Jameson Memorial", module.StationName);
    }

    [Fact]
    public void APurchaseWithNoKnownLocationIsStoredSomewhereUnknown()
    {
        var module = Assert.Single(Modules(Bought).Modules);

        Assert.Equal("unknown", module.StarSystem);
        Assert.Null(module.StationName);
    }

    [Fact]
    public void TheNextSnapshotReplacesThePurchase()
    {
        const string snapshot =
            """
            { "timestamp":"2026-10-04T09:11:00Z", "event":"StoredModules", "StarSystem":"Shinrarta Dezhra",
              "MarketID":128666762, "StationName":"Jameson Memorial",
              "Items":[ { "Name":"$int_cargorack_size4_class1_name;", "Name_Localised":"Cargo Rack",
                          "StarSystem":"Shinrarta Dezhra" } ] }
            """;

        var module = Assert.Single(Modules(Docked, Bought, snapshot).Modules);

        Assert.Equal("int_cargorack_size4_class1", module.Symbol);
    }
}
