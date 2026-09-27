using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A species spansh lists at zero value still reaches the table, at the curated value (#528).</summary>
public class RadicoidaUnicaIsValuedThoughSpanshPricesItAtZeroTests
{
    [Fact]
    public void TheTableCarriesRadicoidaUnicaAtItsSaleValue()
    {
        var entry = Assert.Single(ExobiologyCatalogue.All, entry => entry.Species == "Radicoida Unica");

        Assert.Equal("Radicoida", entry.Genus);
        Assert.Equal(119_037, entry.Value);
    }
}
