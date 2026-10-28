using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

public sealed class ASurfaceMinedBodyShowsItsTonnesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static BodyProfile Planet() =>
        TheSystemPageListsItsStationsTests.Ltt7786Profile().Bodies.First(body => body.Type != "Star");

    private static SurfaceMiningSite Site(params (string Name, int Tonnes)[] materials)
    {
        var run = new SurfaceMiningRun(Start, null)
        {
            Refined = materials.ToImmutableDictionary(
                material => material.Name.ToLowerInvariant(),
                material => new RefinedMaterial(material.Name.ToLowerInvariant(), material.Name, material.Tonnes)),
        };

        return new SurfaceMiningSite(1, 1, null, null, [run]);
    }

    [Fact]
    public void TwoCommoditiesAreListedLargestFirstAfterTheSignals()
    {
        var facts = StarSystemPage.BodyFacts(Planet(), Site(("Silver", 3), ("Platinum", 40)));

        Assert.Equal("Surface mined: 40 t Platinum, 3 t Silver", facts[^1]);
    }

    [Fact]
    public void FourCommoditiesShowTheThreeLargestAndCountTheRest()
    {
        var facts = StarSystemPage.BodyFacts(Planet(), Site(("Gold", 1), ("Platinum", 40), ("Silver", 3), ("Palladium", 9)));

        Assert.Equal("Surface mined: 40 t Platinum, 9 t Palladium, 3 t Silver and 1 more", facts[^1]);
    }

    [Fact]
    public void TonnesAddUpOverEveryRunAtTheSite()
    {
        var second = Site(("Platinum", 5)).Runs[0] with { OpenedAt = Start.AddDays(1) };
        var site = Site(("Platinum", 40)) with { Runs = [.. Site(("Platinum", 40)).Runs, second] };

        Assert.Equal("Surface mined: 45 t Platinum", StarSystemPage.BodyFacts(Planet(), site)[^1]);
    }

    [Fact]
    public void WithNoSiteTheFactsAreUnchanged()
    {
        var body = Planet();

        Assert.Equal(StarSystemPage.BodyFacts(body), StarSystemPage.BodyFacts(body, null));
        Assert.DoesNotContain(StarSystemPage.BodyFacts(body), fact => fact.StartsWith("Surface mined", StringComparison.Ordinal));
    }
}
