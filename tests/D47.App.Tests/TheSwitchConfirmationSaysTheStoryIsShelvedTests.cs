using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Catalogue's Switch confirmation says the current story is shelved, never abandoned.</summary>
public sealed class TheSwitchConfirmationSaysTheStoryIsShelvedTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheCurrentStoryIsShelved(bool toShelved)
    {
        var body = StoriesView.SwitchBody("The First", "The Second", toShelved, remembers: false);

        Assert.StartsWith("The First is shelved:", body, StringComparison.Ordinal);
        Assert.DoesNotContain("abandon", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AShelvedStoryComesBackPaused() =>
        Assert.Contains("The Second comes back paused", StoriesView.SwitchBody("The First", "The Second", toShelved: true, remembers: false), StringComparison.Ordinal);
}
