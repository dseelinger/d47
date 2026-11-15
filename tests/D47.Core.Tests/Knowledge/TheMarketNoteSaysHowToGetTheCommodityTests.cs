using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The Market page's note on how to obtain a commodity, in the words how_to_get uses (#2446).</summary>
public sealed class TheMarketNoteSaysHowToGetTheCommodityTests
{
    private static CommodityOffer Offer(string commodity) => new(
        new MarketSnapshot { Station = "Port", System = "Sol", Type = "Coriolis Starport" },
        100,
        1,
        5)
    {
        Lots = [new CommodityLot(commodity, 100, 1)],
    };

    private static CommodityPosting Posting(
        string commodity, bool stationAnswered = true, bool carriersOnly = false, params string[] more) =>
        new(
            new CommodityQuery(commodity, More: more.Length == 0 ? null : [.. more.Select(name => new CommodityWant(name, null))]),
            stationAnswered ? new CommodityAnswer([Offer(commodity)], 1, 0, true) : CommodityAnswer.Empty,
            "Sol",
            DateTimeOffset.UtcNow,
            carriersOnly);

    [Theory]
    [InlineData("Silver")]
    [InlineData("Palladium")]
    [InlineData("Haematite")]
    [InlineData("Gold")]
    [InlineData("Osmium")]
    public void ARingMinedCommodityWithoutHotspotsNamesRingMiningAndOffersNoSearch(string commodity)
    {
        Assert.Contains(AcquisitionMethod.RingMining, AcquisitionGuide.For(commodity)!.Methods);

        var line = Assert.Single(CommodityNote.For(Posting(commodity)));

        Assert.Equal("Mined from asteroid rings.", line.Sentence);
        Assert.Null(line.Hotspot);
    }

    [Fact]
    public void AHotspotCommodityOffersTheSearch()
    {
        var line = Assert.Single(CommodityNote.For(Posting("Painite")));

        Assert.Equal("Mined from asteroid rings.", line.Sentence);
        Assert.Equal("Painite", line.Hotspot);
    }

    [Fact]
    public void ALithiumHydroxideNoteHasNoSearchBecauseItHasNoHotspots()
    {
        var line = Assert.Single(CommodityNote.For(Posting("Lithium Hydroxide", stationAnswered: false, carriersOnly: false)));

        Assert.Null(line.Hotspot);
    }

    [Fact]
    public void ASurfaceMinedCommodityNamesTheRhinoAndOffersNoSearch()
    {
        var line = Assert.Single(CommodityNote.For(Posting("Bastnasite")));

        Assert.Equal(
            "Surface mining from a Rhino SRV, at a Planetary Mining Location the Detailed Surface Scanner finds.",
            line.Sentence);
        Assert.Null(line.Hotspot);
    }

    [Fact]
    public void AMissionRewardIsNotedOnlyWhenNoStationAnswered()
    {
        Assert.Empty(CommodityNote.For(Posting("Articulation Motors")));

        var carriers = Assert.Single(CommodityNote.For(Posting("Articulation Motors", carriersOnly: true)));
        var nothing = Assert.Single(CommodityNote.For(Posting("Articulation Motors", stationAnswered: false)));

        Assert.Equal("Given as a mission reward.", carriers.Sentence);
        Assert.Equal(carriers.Sentence, nothing.Sentence);
    }

    [Fact]
    public void AMarketOnlyCommodityAndOneWithNoMethodHaveNoNote()
    {
        Assert.Empty(CommodityNote.For(Posting("CMM Composite", stationAnswered: false)));
        Assert.Empty(CommodityNote.For(Posting("Advanced Catalysers", stationAnswered: false)));
    }

    [Fact]
    public void SeveralCommoditiesGetOneLineEachAndOnlyThoseWithAMethod()
    {
        var lines = CommodityNote.For(Posting("Gold", true, false, "Painite", "CMM Composite"));

        Assert.Equal(["Gold: mined from asteroid rings.", "Painite: mined from asteroid rings."], lines.Select(line => line.Sentence));
        Assert.Equal([null, "Painite"], lines.Select(line => line.Hotspot));
    }

    [Fact]
    public void ASellingAnswerHasNoNote()
    {
        var selling = Posting("Gold") with { Query = new CommodityQuery("Gold", TradeSide.Selling) };

        Assert.Empty(CommodityNote.For(selling));
    }
}
