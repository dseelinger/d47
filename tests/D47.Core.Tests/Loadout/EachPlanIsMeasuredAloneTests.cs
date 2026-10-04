using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>The gap's per-build view: each live build against everything held, met materials included (#557).</summary>
public class EachPlanIsMeasuredAloneTests
{
    private static ShipBuild Ship(string id, string? name) =>
        new("F1", id, "anaconda", 12, name,
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5), new SlotPlan("FrameShiftDrive", "Increased Range", 5)]);

    private static OnFootBuild Suit(int? grade) =>
        new("kit-1", "Dominator Suit", OnFootKind.Suit, 7, grade is { } g ? [new KitPlan(OnFootBuild.GradeSlot, g)] : []);

    private static CommanderGameState Holding(MaterialEntry material, int count)
    {
        var store = new GameStateStore();
        var category = material.Category ?? "Raw";
        var empty = new[] { "Raw", "Manufactured", "Encoded" }
            .Where(name => !string.Equals(name, category, StringComparison.OrdinalIgnoreCase))
            .Select(name => $"\"{name}\":[]");

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","{{category}}":[{"Name":"{{material.Symbol}}","Count":{{count}}}],{{string.Join(",", empty)}}}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    [Fact]
    public void EveryLiveBuildIsAGroupWithItsKindAndPlans()
    {
        var report = PlanGap.Of([Ship("ship-1", "Sacred Wings")], [Suit(5)], null);

        Assert.Equal(2, report.Builds.Count);

        var ship = report.Builds[0];
        Assert.Equal(new GapBuildKey(GapBuildKind.Ship, "ship-1"), ship.Key);
        Assert.Equal("Sacred Wings", ship.Name);
        Assert.Equal("Anaconda", ship.Hull);
        Assert.Equal(2, ship.Plans);
        Assert.Equal(ship.Lines.Count, ship.Short);

        var suit = report.Builds[1];
        Assert.Equal(new GapBuildKey(GapBuildKind.Suit, "kit-1"), suit.Key);
        Assert.Equal(5, suit.Grade);
        Assert.All(suit.Lines, line => Assert.Equal(MaterialLedger.ShipLocker, line.Material.Ledger));
    }

    [Fact]
    public void EveryDemandNamesTheBuildItCameFrom()
    {
        var report = PlanGap.Of([Ship("ship-1", null)], [Suit(5)], null);
        var demands = report.Ledgers.SelectMany(ledger => ledger.Lines).SelectMany(line => line.Wanted).ToList();

        Assert.NotEmpty(demands);
        Assert.All(demands, demand => Assert.NotNull(demand.Build));
        Assert.Contains(demands, demand => demand.Build == new GapBuildKey(GapBuildKind.Ship, "ship-1"));
        Assert.Contains(demands, demand => demand.Build == new GapBuildKey(GapBuildKind.Suit, "kit-1"));
    }

    [Fact]
    public void TwoBuildsSharingAMaterialAreEachMetWhileTogetherShort()
    {
        var alone = PlanGap.Of([Ship("ship-1", null)], [], null).Builds[0].Lines
            .First(line => line.Material.Ledger == MaterialLedger.Material);

        var state = Holding(alone.Material, alone.Needed);
        var report = PlanGap.Of([Ship("ship-1", null), Ship("ship-2", null)], [], state);

        Assert.Equal(2, report.Builds.Count);
        Assert.All(report.Builds, build =>
        {
            var line = build.Lines.Single(line => line.Material.Symbol == alone.Material.Symbol);

            Assert.Equal(alone.Needed, line.Held);
            Assert.Equal(0, line.Short);
        });

        var together = report.Ledgers.SelectMany(ledger => ledger.Lines)
            .Single(line => line.Material.Symbol == alone.Material.Symbol);

        Assert.Equal(alone.Needed, together.Short);
    }

    [Fact]
    public void ABuildWithNoPlanIsNoGroup()
    {
        var report = PlanGap.Of([new ShipBuild("F1", "ship-1", "anaconda", 12)], [Suit(null)], null);

        Assert.Empty(report.Builds);
    }

    [Fact]
    public void AnIntendedShipSaysSo()
    {
        var report = PlanGap.Of([Ship("ship-1", null) with { ShipId = null }], [], null);

        Assert.Equal("Anaconda, intended", Assert.Single(report.Builds).Name);
    }
}
