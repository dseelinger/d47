using D47.Core.Capabilities;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A name that no station trades is answered as what it actually is
/// (<a href="https://github.com/dseelinger/d47/issues/54">#54</a>).
/// <para>
/// Reported 2026-08-26. Asked <i>"where are the closest core dynamics composites?"</i>, the model
/// called <c>find_nearest_station</c> — reasonably, since "closest" plus a named thing to buy is
/// what that tool advertises — and the market answer came back <i>"Core Dynamics Composite isn't
/// trading within 50 light years"</i>. True of every engineering material that has ever existed,
/// and useless: it took three more turns of the Commander steering to get an answer.
/// </para>
/// <para>
/// <b>The tool surface was not the problem.</b> Measured across every <c>ControlContext</c>,
/// nothing is dropped and <c>find_material</c> is served everywhere; the largest profile sits at
/// 39,897 of 40,000. The model simply chose the other tool, which it started being able to do on
/// 2026-08-25 when <c>find_nearest_station</c> gained its <c>commodity</c> parameter.
/// </para>
/// <para>
/// So the fix is at runtime rather than in the prose: d47 holds the table that says which ledger
/// a name belongs to, and reading it costs nothing.
/// </para>
/// </summary>
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

    /// <summary>
    /// And it answers the question rather than only refusing it. The catalogue carries where the
    /// thing comes from, so a redirect that made the Commander ask again would be a worse answer
    /// than one that is already there.
    /// </summary>
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

    /// <summary>
    /// The half that must not break: a real commodity still reaches the market search. Catching
    /// too much here would turn a working tool into a lecture.
    /// </summary>
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
    /// The ledger is what decides it, rather than a list of names kept here — the catalogue is
    /// the one place that knows, and a second copy would be a second thing to keep true.
    /// </summary>
    [Fact]
    public void TheDistinctionIsTheCataloguesRatherThanThisTests()
    {
        Assert.Equal(MaterialLedger.Material, MaterialCatalogue.Find("Core Dynamics Composites")?.Ledger);
        Assert.Equal(MaterialLedger.Cargo, MaterialCatalogue.Find("Gold")?.Ledger);
    }

    /// <summary>
    /// The other direction was already right and stays asserted, so the pair cannot drift: a
    /// commodity handed to the engineering tool is told it is cargo rather than searched for.
    /// </summary>
    [Fact]
    public async Task AndACommodityHandedToTheMaterialToolIsToldSo()
    {
        var said = await AskedAsync("find_material", "material", "Gold");

        Assert.DoesNotContain("Found at:", said, StringComparison.Ordinal);
    }
}
