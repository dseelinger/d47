using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheRareIndexIsReadFromItsFileTests
{
    private const string Text =
        "# built 2026-11-03T09:00:00Z\n"
        + "# far prices 2026-11-02T21:00:00Z\n"
        + "symbol\tfar_sell_price\tfar_reports\tbuy_price\tstock\treported_at\n"
        + "SoontillRelics\t33000\t7\t17000\t80\t2026-11-03T08:30:00Z\n"
        + "DisoMaCorn\t\t2\t4000\t\t2026-11-03T08:00:00Z\n";

    [Fact]
    public void ABlankFarPriceAndABlankStockAreUnknown()
    {
        var index = Assert.IsType<RareIndex>(RareIndex.Parse(Text));

        var row = Assert.IsType<RareIndexRow>(index.Find("DisoMaCorn"));

        Assert.Null(row.FarSellPrice);
        Assert.Equal(2, row.FarReports);
        Assert.Equal(4000, row.BuyPrice);
        Assert.Null(row.Stock);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero), row.ReportedAt);
    }

    [Fact]
    public void TheTwoTimesAndEachRowAreKept()
    {
        var index = Assert.IsType<RareIndex>(RareIndex.Parse(Text));

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), index.Built);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 21, 0, 0, TimeSpan.Zero), index.FarPricesAt);
        Assert.Equal(2, index.Rows.Count);
        Assert.Equal(33000, index.Find("SoontillRelics")!.FarSellPrice);
    }

    [Fact]
    public void TextWithoutTheHeaderLineIsNotAnIndex()
    {
        Assert.Null(RareIndex.Parse("<html>Not found</html>"));
        Assert.Null(RareIndex.Parse(string.Empty));
    }
}
