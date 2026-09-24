using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>Every ship material and ship-locker row, catalogue row by catalogue row, grouped into tracker cards.</summary>
public class MaterialTrackerTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A Commander holding 300 iron and some Heat Exchangers, with no plan asking for either.</summary>
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":300}],"Manufactured":[{"Name":"heatexchangers","Count":4}],"Encoded":[]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static ShipBuild Ship() =>
        new("F1", "ship-1", "python", 12, "Bad Idea",
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5)]);

    private static IReadOnlyList<MaterialRow> ShipRows(MaterialTrackerReport report) =>
        [.. report.Ship.SelectMany(card => card.Rows)];

    /// <summary>Every ship-material catalogue row is on the ship side once, zero-held rows included.</summary>
    [Fact]
    public void EveryShipMaterialAppearsOnceOnTheShipSide()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var rows = ShipRows(report);
        var expected = MaterialCatalogue.All.Where(entry => entry.Ledger == MaterialLedger.Material).ToList();

        Assert.Equal(expected.Count, rows.Count);
        Assert.Equal(
            expected.Select(entry => entry.Symbol).OrderBy(symbol => symbol, StringComparer.Ordinal),
            rows.Select(row => row.Material.Symbol).OrderBy(symbol => symbol, StringComparer.Ordinal));
    }

    /// <summary>Every ship-locker catalogue row is on the on-foot side once, zero-held rows included.</summary>
    [Fact]
    public void EveryShipLockerRowAppearsOnceOnTheOnFootSide()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var rows = report.OnFoot.SelectMany(card => card.Rows).ToList();
        var expected = MaterialCatalogue.All.Where(entry => entry.Ledger == MaterialLedger.ShipLocker).ToList();

        Assert.Equal(expected.Count, rows.Count);
        Assert.Equal(
            expected.Select(entry => entry.Symbol).OrderBy(symbol => symbol, StringComparer.Ordinal),
            rows.Select(row => row.Material.Symbol).OrderBy(symbol => symbol, StringComparer.Ordinal));
    }

    /// <summary>Guardian and Thargoid pick up every Line-less material row, and nothing that has a Line.</summary>
    [Fact]
    public void GuardianAndThargoidAreTheLineLessMaterialRows()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var guardian = report.Ship.Single(card => card.Name == "Guardian");
        var thargoid = report.Ship.Single(card => card.Name == "Thargoid");

        Assert.Equal(13, guardian.Rows.Count);
        Assert.Equal(19, thargoid.Rows.Count);
        Assert.All(guardian.Rows, row => Assert.Null(row.Material.Line));
        Assert.All(thargoid.Rows, row => Assert.Null(row.Material.Line));
    }

    /// <summary>The five ship cards are reported Raw, Manufactured, Encoded, Guardian, Thargoid.</summary>
    [Fact]
    public void ShipCardsAreOrderedRawThroughThargoid()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        Assert.Equal(
            ["Raw", "Manufactured", "Encoded", "Guardian", "Thargoid"],
            report.Ship.Select(card => card.Name));
    }

    /// <summary>The four on-foot cards are reported Items, Components, Consumables, Data.</summary>
    [Fact]
    public void OnFootCardsAreOrderedItemsThroughData()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        Assert.Equal(["Items", "Components", "Consumables", "Data"], report.OnFoot.Select(card => card.Name));
    }

    /// <summary>A material held but wanted by no plan reads as needed nothing and short nothing.</summary>
    [Fact]
    public void AHeldMaterialNoPlanWantsHasNeededZeroAndShortZero()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var iron = ShipRows(report).Single(row => row.Material.Symbol == "iron");

        Assert.Equal(300, iron.Held);
        Assert.Equal(0, iron.Needed);
        Assert.Equal(0, iron.Short);
        Assert.Empty(iron.Wanted);
    }

    /// <summary>A material a plan is short of still carries its need, its askers and its trade offer.</summary>
    [Fact]
    public void AShortfallStillNamesWhatWantsIt()
    {
        var gap = PlanGap.Of([Ship()], [], State());
        var report = MaterialTracker.Of(State(), gap);

        var gapLine = gap.Ledgers.SelectMany(ledger => ledger.Lines).First();
        var row = ShipRows(report).Single(candidate => candidate.Material.Symbol == gapLine.Material.Symbol);

        Assert.Equal(gapLine.Needed, row.Needed);
        Assert.NotEmpty(row.Wanted);
        Assert.Equal(gapLine.Wanted.Count, row.Wanted.Count);
    }

    /// <summary>
    /// Heat Exchangers' trade down is from Heat Vanes, at the same rate the trade rules give for grade 4
    /// down to grade 3, not a copied literal.
    /// </summary>
    [Fact]
    public void HeatExchangersTradeDownIsFromHeatVanes()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var heatExchangers = ShipRows(report).Single(row => row.Material.Symbol == "heatexchangers");

        Assert.NotNull(heatExchangers.TradeDown);
        Assert.Equal("Heat Vanes", heatExchangers.TradeDown.From.Name);
        Assert.Equal(EngineeringRules.TradeRate(4, 3, sameLine: true), heatExchangers.TradeDown.Rate);
    }

    /// <summary>A grade-5 material, an off-line row and every on-foot row have no trade down.</summary>
    [Fact]
    public void TopGradeAndOffLineRowsHaveNoTradeDown()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var protoHeatRadiators = ShipRows(report).Single(row => row.Material.Symbol == "protoheatradiators");
        Assert.Null(protoHeatRadiators.TradeDown);

        var guardianAndThargoid = report.Ship
            .Where(card => card.Name is "Guardian" or "Thargoid")
            .SelectMany(card => card.Rows);
        Assert.All(guardianAndThargoid, row => Assert.Null(row.TradeDown));

        Assert.All(report.OnFoot.SelectMany(card => card.Rows), row => Assert.Null(row.TradeDown));
    }

    /// <summary>The Raw card's top source is what every one of its 28 rows carries.</summary>
    [Fact]
    public void TheRawCardsTopSourceIsSurfaceProspecting()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var raw = report.Ship.Single(card => card.Name == "Raw");

        Assert.Equal(28, raw.Rows.Count);
        Assert.Equal("Surface prospecting", raw.Sources[0].Origin);
        Assert.Equal(28, raw.Sources[0].Rows);
    }

    /// <summary>The Guardian card's rows carry exactly one origin between them, so it has exactly one source.</summary>
    [Fact]
    public void TheGuardianCardsOnlySourceIsAncientGuardianRuins()
    {
        var report = MaterialTracker.Of(State(), PlanGap.Of([], [], State()));

        var guardian = report.Ship.Single(card => card.Name == "Guardian");

        Assert.Equal([new MaterialOriginCount("Ancient/Guardian ruins", 13)], guardian.Sources);
    }
}
