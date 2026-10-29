using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The Features page's section headings keep their state in the view state's card ids.</summary>
public class CoverageSectionsAreRememberedTests
{
    public static TheoryData<string> Ids() =>
        ["coverage.failed", "coverage.never", "coverage.changed", "coverage.exercised"];

    [Theory]
    [MemberData(nameof(Ids))]
    public void ASectionNeverSetOpensExpanded(string id) =>
        Assert.True(new ViewState().IsExpanded(id, startCollapsed: false));

    [Theory]
    [MemberData(nameof(Ids))]
    public void ACollapsedSectionComesBackCollapsedAndReopens(string id)
    {
        var closed = new ViewState().With(id, false);
        Assert.False(closed.IsExpanded(id, startCollapsed: false));
        Assert.True(closed.With(id, true).IsExpanded(id, startCollapsed: false));
    }

    [Fact]
    public void CollapsingOneSectionLeavesTheOthersOpen()
    {
        var state = new ViewState().With("coverage.never", false);

        Assert.True(state.IsExpanded("coverage.exercised", startCollapsed: false));
    }
}
