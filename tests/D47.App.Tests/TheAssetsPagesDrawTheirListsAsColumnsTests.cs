using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Characters, Materials and the two detail pages draw their list inside the page, never as a second sidebar.</summary>
public class TheAssetsPagesDrawTheirListsAsColumnsTests
{
    [Trait("Category", "Gate")]
    [Theory]
    [InlineData("CharactersPage.cs")]
    [InlineData("MaterialsPage.cs")]
    [InlineData("MaterialDetailPage.cs")]
    [InlineData("OnFootDetailPage.cs")]
    public void ThePageDrawsNoSidebarControl(string file)
    {
        var path = Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App", "Panel", file);
        var text = File.ReadAllText(path);

        Assert.DoesNotMatch(new Regex(@"\bSidebar\b"), text);
    }
}
