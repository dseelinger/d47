using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Find lists every commodity the Market page offers, by name (#2445).</summary>
public class FindListsCommoditiesTests
{
    private static PanelFind Find()
    {
        var (_, settings, nav) = FindRanksPagesSettingsAndHelpTests.Build();

        return new PanelFind(nav, settings, new Dictionary<PanelTab, string>());
    }

    [Fact]
    public void ACommodityIsFoundByPartOfItsNameAndMarked()
    {
        var result = Find().Search("lithium hyd").Single(r => r.Kind == PanelFindKind.Commodity);

        Assert.Equal("Lithium Hydroxide", result.Title);
        Assert.Equal("Commodity", result.Path);
        Assert.Equal("Lithium Hydroxide", result.Target);
    }

    [Fact]
    public void ARareCommodityIsFoundToo()
    {
        var rare = MaterialCatalogue.All.First(entry => entry.Ledger == MaterialLedger.RareCargo).Name;

        Assert.Contains(Find().Search(rare), r => r.Kind == PanelFindKind.Commodity && r.Title == rare);
    }

    [Fact]
    public void AMaterialThatIsNotCargoIsNotACommodity()
    {
        var material = MaterialCatalogue.All.First(entry => entry.Ledger is not (MaterialLedger.Cargo or MaterialLedger.RareCargo)).Name;

        Assert.DoesNotContain(Find().Search(material), r => r.Kind == PanelFindKind.Commodity && r.Title == material);
    }
}
