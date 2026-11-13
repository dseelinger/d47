using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheGuardianSitesCatalogueReadsEveryRowTests
{
    private const string TwoBodies = "Col 173 Sector HR-M b23-3";

    private static List<string> DataLines()
    {
        using var stream = typeof(GuardianSites).Assembly.GetManifestResourceStream("D47.Core.GuardianSites")!;
        using var reader = new StreamReader(stream);

        return [.. reader.ReadToEnd().Split('\n').Where(line => line.Length > 0 && line[0] != '#' && !line.StartsWith("system\t", StringComparison.Ordinal))];
    }

    [Fact]
    public void EveryLineOfTheTableIsARow()
    {
        var lines = DataLines();

        Assert.NotEmpty(lines);
        Assert.Equal(lines.Count, GuardianSites.All.Count);
    }

    [Fact]
    public void EveryRowHasASystemAnAddressAPositionAndABody()
    {
        Assert.All(GuardianSites.All, site =>
        {
            Assert.False(string.IsNullOrWhiteSpace(site.System));
            Assert.True(site.Address > 0, site.System);
            Assert.NotEqual(StarPosition.Origin, site.At);
            Assert.StartsWith(site.System, site.Body, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ASystemWithTwoBodiesIsOneSystemWithTwoBodies()
    {
        var bodies = GuardianSites.All.Where(site => site.System == TwoBodies).ToList();
        var systems = StarCharts.Systems(StarChartKind.GuardianSites);

        Assert.Equal(2, bodies.Select(site => site.Body).Distinct().Count());
        Assert.Single(systems, system => system.Name == TwoBodies);
        Assert.Equal(bodies[0].At, systems.Single(system => system.Name == TwoBodies).At);
        Assert.Equal(bodies[0].Address, systems.Single(system => system.Name == TwoBodies).Address);
        Assert.Equal(["1 body with ruins", "1 with a structure"], GuardianSites.Counted(bodies));
    }

    [Fact]
    public void TheChartIsOneSystemForEachAddressInTheTableInNameOrder()
    {
        var systems = StarCharts.Systems(StarChartKind.GuardianSites);

        Assert.Equal(GuardianSites.All.Select(site => site.Address).Distinct().Count(), systems.Count);
        Assert.Equal(systems.Select(system => system.Name).Order(StringComparer.Ordinal), systems.Select(system => system.Name));
    }

    [Fact]
    public void ARowReadsItsKindAndLayouts()
    {
        const string tsv = "# a comment\n"
            + "system\taddress\tx\ty\tz\tbody\tkind\tlayouts\n"
            + "Synuefe EU-Q c21-10\t2869440882065\t758.65625\t-176.90625\t-111.09375\tSynuefe EU-Q c21-10 3\tstructure\tBear;Squid\n"
            + "Synuefe GV-T b46-1\t2869440554457\t761.5\t-175.5\t-112.25\tSynuefe GV-T b46-1 2 a\truin\t\n";

        using var reader = new StringReader(tsv);
        var sites = GuardianSites.Parse(reader);

        Assert.Equal(2, sites.Count);
        Assert.Equal(GuardianSiteKind.Structure, sites[0].Kind);
        Assert.Equal(["Bear", "Squid"], sites[0].Layouts);
        Assert.Equal(new StarPosition(758.65625, -176.90625, -111.09375), sites[0].At);
        Assert.Equal(GuardianSiteKind.Ruin, sites[1].Kind);
        Assert.Empty(sites[1].Layouts);
    }

    [Theory]
    [InlineData(2, 0, "2 bodies with ruins")]
    [InlineData(0, 1, "1 body with a structure")]
    [InlineData(0, 2, "2 bodies with structures")]
    [InlineData(3, 2, "3 bodies with ruins|2 with structures")]
    public void TheCountsNameRuinsBeforeStructures(int ruins, int structures, string said)
    {
        var bodies = Enumerable.Range(0, ruins).Select(index => Site(GuardianSiteKind.Ruin, index))
            .Concat(Enumerable.Range(0, structures).Select(index => Site(GuardianSiteKind.Structure, index)));

        Assert.Equal(said.Split('|'), GuardianSites.Counted(bodies));
    }

    private static GuardianSite Site(GuardianSiteKind kind, int index) =>
        new("Example", 1, new StarPosition(1, 2, 3), $"Example {kind} {index}", kind, []);
}
