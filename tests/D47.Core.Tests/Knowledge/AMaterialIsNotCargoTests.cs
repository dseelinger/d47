using D47.Core.Capabilities;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A name that no station trades is answered as what it actually is.</summary>
public class AMaterialIsNotCargoTests
{
    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    private static async Task<string> AskedAsync(string tool, string parameter, string value)
    {
        using var install = new TempInstall();

        var result = await TestSurface.For(install).Registry.InvokeAsync(
            tool,
            Args((parameter, value)),
            TestContext.Current.CancellationToken);

        return result.Content ?? string.Empty;
    }

    private static Task<string> AskedForAsync(string commodity) =>
        AskedAsync("find_nearest_station", "commodity", commodity);

    [Fact]
    public async Task TheReportedNameIsAnsweredAsAMaterial()
    {
        var said = await AskedForAsync("Core Dynamics Composites");

        Assert.Contains("not a commodity", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("engineering material", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>And it answers the question rather than only refusing it.</summary>
    [Fact]
    public async Task AndSaysWhereItComesFrom()
    {
        var said = await AskedForAsync("Core Dynamics Composites");

        Assert.Contains("Found at:", said, StringComparison.Ordinal);
        Assert.Contains("find_material", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Yttrium")]
    [InlineData("Imperial Shielding")]
    [InlineData("Datamined Wake Exceptions")]
    public async Task AnyShipMaterialIsCaughtTheSameWay(string material)
    {
        Assert.Contains("not a commodity", await AskedForAsync(material), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The half that must not break: a real commodity still reaches the market search.</summary>
    [Theory]
    [InlineData("Gold")]
    [InlineData("Tritium")]
    [InlineData("Painite")]
    public async Task ARealCommodityStillGoesToTheMarket(string commodity)
    {
        var said = await AskedForAsync(commodity);

        Assert.DoesNotContain("not a commodity", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The ledger is what decides it, rather than a list of names kept here — the catalogue is the one
    /// place that knows, and a second copy would be a second thing to keep true.
    /// </summary>
    [Fact]
    public void TheDistinctionIsTheCataloguesRatherThanThisTests()
    {
        Assert.Equal(MaterialLedger.Material, MaterialCatalogue.Find("Core Dynamics Composites")?.Ledger);
        Assert.Equal(MaterialLedger.Cargo, MaterialCatalogue.Find("Gold")?.Ledger);
    }

 /// <summary>The other direction, and this assertion has inverted on purpose.</summary>
    [Fact]
    public async Task AndACommodityHandedToTheMaterialToolIsAnsweredRatherThanJustCorrected()
    {
        var said = await AskedAsync("find_material", "material", "Gold");

        Assert.Contains("not a ship material", said, StringComparison.Ordinal);
        Assert.Contains("find_nearest_station", said, StringComparison.Ordinal);

        // No engineering search ran.
        Assert.DoesNotContain("trader", said, StringComparison.OrdinalIgnoreCase);
    }
}
