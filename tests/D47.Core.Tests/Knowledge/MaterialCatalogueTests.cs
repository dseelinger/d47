using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The generated material table, and the four things it carries that the journal does not.
/// <para>
/// These run against the real embedded resource rather than a fixture. A table is only worth
/// having if it is right, and a test asserting against its own copy of the data would prove
/// the parser works and nothing about the game.
/// </para>
/// </summary>
public class MaterialCatalogueTests
{
    [Fact]
    public void TheShippedTableCarriesAllFourInventories()
    {
        var byLedger = MaterialCatalogue.All
            .GroupBy(entry => entry.Ledger)
            .ToDictionary(group => group.Key, group => group.Count());

        // Every bucket populated. An empty one means a source moved and the generator wrote a
        // table that parses perfectly and answers half the questions.
        Assert.Equal(137, byLedger[MaterialLedger.Material]);
        Assert.True(byLedger[MaterialLedger.ShipLocker] > 150);
        Assert.True(byLedger[MaterialLedger.Cargo] > 200);
        Assert.True(byLedger[MaterialLedger.RareCargo] > 100);
        Assert.False(byLedger.ContainsKey(MaterialLedger.Unknown));
    }

    [Fact]
    public void GoldIsCargoRatherThanSomethingWithAMaterialCap()
    {
        // Lei Cheung wants Gold ×200, which is two hundred tonnes. Filing it under a 300-unit
        // material cap produces a feasibility verdict that is nonsense and sounds confident.
        var gold = MaterialCatalogue.Find("gold");

        Assert.NotNull(gold);
        Assert.Equal(MaterialLedger.Cargo, gold.Ledger);
        Assert.Null(gold.Grade);
        Assert.Null(gold.Line);
        Assert.False(gold.IsTradeable);
    }

    [Fact]
    public void EveryGradeAgreesWithTheTableTheRestOfCoreAlreadyUses()
    {
        // Two generated tables from the same source file. If they ever disagree, one of them is
        // stale and a capacity or a trade rate is being computed off the wrong number.
        foreach (var entry in MaterialCatalogue.All.Where(e => e.Ledger == MaterialLedger.Material))
        {
            Assert.Equal(MaterialGrades.GradeOf(entry.Symbol), entry.Grade);
        }
    }

    [Fact]
    public void TheTraderLinesAreTwentyThreeLaddersWithOneMaterialPerGrade()
    {
        var lines = MaterialCatalogue.Lines;

        Assert.Equal(23, lines.Count);

        foreach (var line in lines)
        {
            var ladder = MaterialCatalogue.InLine(line);
            var grades = ladder.Select(entry => entry.Grade).ToArray();

            // The whole point of a line: one material per grade. Two at the same grade would
            // make "trade up one" ambiguous, and the rate is computed off the grade delta.
            Assert.Equal(grades.Length, grades.Distinct().Count());
            Assert.All(ladder, entry => Assert.Equal(MaterialLedger.Material, entry.Ledger));
        }

        // Raw lines stop at grade 4. Reported rather than padded — there is no grade 5 Raw.
        var raw = lines.Where(line => line.StartsWith("raw-", StringComparison.Ordinal)).ToArray();

        Assert.Equal(7, raw.Length);
        Assert.All(raw, line => Assert.Equal(4, MaterialCatalogue.InLine(line).Count));
    }

    [Fact]
    public void TheLineIsTheLadderTheTraderActuallyShows()
    {
        // Checked against the in-game grid rather than against the generator's own output.
        Assert.Equal(
            ["Carbon", "Vanadium", "Niobium", "Yttrium"],
            MaterialCatalogue.InLine("raw-1").Select(entry => entry.Name));

        Assert.Equal(
            ["Salvaged Alloys", "Galvanising Alloys", "Phase Alloys", "Proto Light Alloys", "Proto Radiolic Alloys"],
            MaterialCatalogue.InLine("manufactured-alloys").Select(entry => entry.Name));
    }

    [Fact]
    public void IronAndVanadiumAreDifferentLinesEvenThoughTheJournalCallsThemOneCategory()
    {
        // The trap the line column exists for. Both are Raw, so the journal's Category says
        // "same", and this is in fact a 6x cross-line trade — the commonest trade there is.
        var iron = MaterialCatalogue.Find("iron");
        var vanadium = MaterialCatalogue.Find("vanadium");

        Assert.NotNull(iron);
        Assert.NotNull(vanadium);
        Assert.Equal(iron.Category, vanadium.Category);
        Assert.NotEqual(iron.Line, vanadium.Line);
    }

    [Fact]
    public void GuardianAndThargoidMaterialsHaveNoLineToPriceAgainst()
    {
        // They are grouped for display and the trader does not deal in them. No line is the
        // signal to decline; a made-up one would price an impossible trade.
        var guardian = MaterialCatalogue.Find("guardian_powercell");
        var thargoid = MaterialCatalogue.Find("unknownorganiccircuitry");

        Assert.NotNull(guardian);
        Assert.NotNull(thargoid);
        Assert.Null(guardian.Line);
        Assert.Null(thargoid.Line);
        Assert.False(guardian.IsTradeable);
        Assert.False(thargoid.IsTradeable);

        Assert.Equal(108, MaterialCatalogue.All.Count(entry => entry.IsTradeable));
    }

    [Fact]
    public void TheEncodedMaterialsThatDriftKeepTheirOrigins()
    {
        // Keying on EDEngineer's own id loses 22 of the 45 Encoded materials, silently, because
        // it keeps the leading adjective Frontier drops. This is the check that the join was
        // done on display name instead.
        var encoded = MaterialCatalogue.All
            .Where(entry => entry.Category == "Encoded")
            .ToArray();

        Assert.Equal(45, encoded.Length);

        var described = encoded.Where(entry => entry.Origins.Count > 0).ToArray();

        Assert.True(described.Length > 40, $"only {described.Length} of 45 Encoded have origins");
        Assert.NotEmpty(MaterialCatalogue.Find("bulkscandata")!.Origins);
        Assert.NotEmpty(MaterialCatalogue.Find("dataminedwake")!.Origins);
    }

    [Fact]
    public void TheFourAliasedNamesResolveToARealSymbol()
    {
        // Each is a display name one source spells differently. Unaliased they lose their
        // origins; aliased onto the wrong item they would gain somebody else's.
        Assert.NotEmpty(MaterialCatalogue.Find("compactemissionsdata")!.Origins);
        Assert.NotEmpty(MaterialCatalogue.Find("ballisticsdata")!.Origins);
        Assert.NotEmpty(MaterialCatalogue.Find("guardian_sentinel_wreckagecomponents")!.Origins);
        Assert.NotEmpty(MaterialCatalogue.Find("xihecompanions")!.Origins);
    }

    [Fact]
    public void GeographicalDataIsNotGeologicalDataAndIsSaidToBeUnkeyed()
    {
        // The near-miss that must not be aliased. Geological Data is a real ship-locker item
        // with its own sourcing; hanging Geographical Data's origins on it would be worse than
        // having none, and quieter.
        Assert.Contains("Geographical Data", MaterialCatalogue.KnownButUnkeyed);
        Assert.Null(MaterialCatalogue.Find("geographicaldata"));

        var geological = MaterialCatalogue.Find("geologicaldata");

        Assert.NotNull(geological);
        Assert.Equal(MaterialLedger.ShipLocker, geological.Ledger);
    }

    [Fact]
    public void AmbiguousDisplayNamesStillReachBothThingsBySymbol()
    {
        // "Wreckage Components" is a Thargoid material and a salvage commodity. The material
        // wins the spoken form because that is what an engineering question means, and the
        // commodity stays reachable by the symbol the journal writes.
        var material = MaterialCatalogue.Find("tg_wreckagecomponents");
        var commodity = MaterialCatalogue.Find("wreckagecomponents");

        Assert.NotNull(material);
        Assert.NotNull(commodity);
        Assert.Equal(MaterialLedger.Material, material.Ledger);
        Assert.Equal(MaterialLedger.Cargo, commodity.Ledger);
        Assert.Equal(MaterialLedger.Material, MaterialCatalogue.Find("Wreckage Components")?.Ledger);
    }

    [Fact]
    public void AMaterialIsFoundByTheJournalsSymbolAndByWhatAPersonSays()
    {
        var bySymbol = MaterialCatalogue.Find("protoradiolicalloys");
        var byName = MaterialCatalogue.Find("Proto Radiolic Alloys");

        Assert.NotNull(bySymbol);
        Assert.Equal(bySymbol, byName);
        Assert.Equal(5, bySymbol.Grade);
        Assert.Equal("manufactured-alloys", bySymbol.Line);
    }
}
