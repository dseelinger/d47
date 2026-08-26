using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A tool handed a neighbour's input says so and answers
/// (<a href="https://github.com/dseelinger/d47/issues/58">#58</a>), generalising the fix that
/// shipped in v0.73.0 for <a href="https://github.com/dseelinger/d47/issues/54">#54</a>.
/// <para>
/// Three tools read three tables and a Commander does not speak in ledgers. "Where do I get X" is
/// one sentence whichever of the three X lives in, so every one of the six directions is asserted
/// — a pair checked one way round is a pair that drifts.
/// </para>
/// </summary>
public class TwoTablesOneQuestionTests
{
    /// <summary>
    /// A representative of each ledger, taken from the catalogue rather than written down here.
    /// A name hardcoded in a test is a second list of names, which is the rule the seam itself
    /// keeps: read the declared fact.
    /// </summary>
    private static MaterialEntry AnyOf(MaterialLedger ledger) =>
        MaterialCatalogue.All.First(entry => entry.Ledger == ledger);

    public static TheoryData<MaterialLedger, string> WrongTools
    {
        get
        {
            var data = new TheoryData<MaterialLedger, string>();

            // Every ledger against every tool that is not its own — six of the nine combinations.
            // Only the ledgers the generated table actually uses. Unknown is a guard value that
            // no entry carries, so there is nothing to hand a tool for it.
            foreach (var ledger in MaterialCatalogue.All.Select(entry => entry.Ledger).Distinct())
            {
                foreach (var tool in new[]
                         {
                             MaterialSeam.MaterialTool,
                             MaterialSeam.MicroResourceTool,
                             MaterialSeam.MarketTool,
                         })
                {
                    if (MaterialSeam.ToolFor(ledger) != tool)
                    {
                        data.Add(ledger, tool);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Every direction says three things: what it actually is, which tool answers, and — the part
    /// that makes it an answer rather than a redirect — what that tool will tell them.
    /// </summary>
    [Theory]
    [MemberData(nameof(WrongTools))]
    public void EveryWrongDirectionNamesTheToolThatAnswers(MaterialLedger ledger, string askedOf)
    {
        var material = AnyOf(ledger);

        var said = MaterialSeam.NotThisOne(material, askedOf);

        Assert.Contains(material.Name, said, StringComparison.Ordinal);
        Assert.Contains(MaterialSeam.ToolFor(ledger), said, StringComparison.Ordinal);

        // And never names the tool that could not answer as though it could.
        Assert.DoesNotContain($"Ask {askedOf}", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule the shipped one established: answer, do not redirect. A redirect is a fourth turn
    /// when the catalogue was holding the answer all along.
    /// </summary>
    [Fact]
    public void WhereTheTableKnowsWhereToGetItItSaysSo()
    {
        var material = MaterialCatalogue.All
            .First(entry => entry.Ledger == MaterialLedger.Material && entry.Origins.Count > 0);

        var said = MaterialSeam.NotThisOne(material, MaterialSeam.MarketTool);

        Assert.Contains("Found at:", said, StringComparison.Ordinal);
        Assert.Contains(material.Origins[0], said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And an Odyssey good gets both halves — where it comes from and which buildings hold it —
    /// so the answer is as complete on the on-foot side as on the ship side.
    /// </summary>
    [Fact]
    public void AnOdysseyGoodSaysWhichBuildingsHoldIt()
    {
        // 163 of the 196 ship-locker entries carry buildings, and every one of those carries
        // origins as well — so the seam says both rather than choosing, and a test that expected
        // one or the other would be asserting a branch nothing reaches.
        var found = MaterialCatalogue.All.First(entry =>
            entry.Ledger == MaterialLedger.ShipLocker && entry.Buildings.Count > 0);

        var said = MaterialSeam.NotThisOne(found, MaterialSeam.MaterialTool);

        Assert.Contains("Found at:", said, StringComparison.Ordinal);
        Assert.Contains("Held in:", said, StringComparison.Ordinal);
        Assert.Contains(found.Buildings[0], said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reason a Commander was told no is said in terms of what the tool they hit is for,
    /// rather than as a bare "wrong tool" — they did not choose the tool and should not have to
    /// care that one was chosen.
    /// </summary>
    [Theory]
    [InlineData(MaterialSeam.MarketTool, "no station trades it")]
    [InlineData(MaterialSeam.MaterialTool, "no engineering search applies")]
    [InlineData(MaterialSeam.MicroResourceTool, "not in any ship locker")]
    public void TheDenialIsInTheTermsOfTheToolThatWasAsked(string askedOf, string because)
    {
        var ledger = askedOf == MaterialSeam.MarketTool ? MaterialLedger.Material : MaterialLedger.Cargo;

        Assert.Contains(because, MaterialSeam.NotThisOne(AnyOf(ledger), askedOf), StringComparison.Ordinal);
    }

    /// <summary>
    /// The seam reads <see cref="MaterialEntry.Ledger"/> and nothing else, so a material added to
    /// the generated table tomorrow is routed without anybody adding it anywhere. This is the rule
    /// that keeps a second list of names from growing beside the first.
    /// </summary>
    [Fact]
    public void EveryLedgerHasAToolAndNoneFallsThrough()
    {
        foreach (var ledger in MaterialCatalogue.All.Select(entry => entry.Ledger).Distinct())
        {
            var tool = MaterialSeam.ToolFor(ledger);

            Assert.Contains(
                tool,
                new[] { MaterialSeam.MaterialTool, MaterialSeam.MicroResourceTool, MaterialSeam.MarketTool });
        }
    }

    /// <summary>
    /// One seam is closed by construction rather than by a check, and this is where that is
    /// recorded: <c>find_material_trader</c> takes its <c>type</c> from a closed set with
    /// <c>AllowedValues</c> on the parameter, so a material name cannot land on it at all. If that
    /// parameter ever becomes free text, this test is what says the seam reopened.
    /// </summary>
    [Fact]
    public void TheTraderToolCannotReceiveAMaterialName()
    {
        Assert.NotEmpty(StationQuery.TraderTypes);

        // Every accepted value is a kind of trader, not a thing to trade. A material name in this
        // list would mean the parameter had started accepting the neighbouring tool's input.
        Assert.All(
            StationQuery.TraderTypes,
            type => Assert.Null(MaterialCatalogue.Find(type)));
    }
}
