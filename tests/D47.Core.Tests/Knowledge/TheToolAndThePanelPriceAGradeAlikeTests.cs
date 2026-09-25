using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <c>get_on_foot_engineering</c> and the Suits panel's grade page quote one credit figure, computed in
/// one place (#464).
/// </summary>
public class TheToolAndThePanelPriceAGradeAlikeTests
{
    [Fact]
    public async Task TheToolSaysTheLineThePanelDraws()
    {
        var tool = Assert.Single(
            OnFootCapability.Create(() => null).Tools, tool => tool.Name == "get_on_foot_engineering");

        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string>
            {
                ["equipment"] = "Maverick Suit",
                ["grade"] = "5",
            }),
            CancellationToken.None);

        var entry = OnFootCatalogue.Named("Maverick Suit");
        var credits = OnFootCatalogue.UpgradeCredits(entry, entry!.Grade, 5);

        Assert.NotNull(credits);
        Assert.Contains($"{credits.Describe()}.", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownGradeIsPricedFromGradeOne()
    {
        var credits = OnFootCatalogue.UpgradeCredits(OnFootCatalogue.Named("Maverick Suit"), null, 3);

        Assert.Equal(1, credits?.From);
    }

    [Fact]
    public void NoClimbHasNoPrice()
    {
        Assert.Null(OnFootCatalogue.UpgradeCredits(OnFootCatalogue.Named("Maverick Suit"), 5, 5));
        Assert.Null(OnFootCatalogue.UpgradeCredits(null, 1, 5));
    }
}
