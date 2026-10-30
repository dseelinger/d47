using Xunit;

namespace D47.App.Tests;

/// <summary>A path written with " › " is read out of its page or string, without the words around it.</summary>
public sealed class APathIsReadOutOfTheProseAroundItTests
{
    private static string[] Read(string text) => [.. PanelPathReader.Read(text).Select(path => path.ToString())];

    [Fact]
    public void APathInProseIsReadWithoutTheWordsAroundIt()
    {
        Assert.Equal(["Asset Mgmt › Carrier"], Read("You choose the pictures on Asset Mgmt › Carrier, under Captain and tower."));
    }

    [Fact]
    public void ATrailingGroupIsReadSeparately()
    {
        var path = Assert.Single(PanelPathReader.Read("Open Settings › Hearing and sound › Voice Input, group Microphone."));

        Assert.Equal(["Open Settings", "Hearing and sound", "Voice Input"], path.Parts);
        Assert.Equal("Microphone", path.Group);
    }

    [Fact]
    public void APathWithNoGroupHasNone()
    {
        Assert.Null(Assert.Single(PanelPathReader.Read("See Settings › About.")).Group);
    }

    [Fact]
    public void ASeparatorAtTheEndOfALineJoinsTheNextLine()
    {
        Assert.Equal(["Settings › Your core › In flight › Flight callouts"], Read("answers Settings › Your core › In flight ›\nFlight callouts."));
    }

    [Fact]
    public void BoldMarkersAreNotPartOfAPath()
    {
        Assert.Equal(["Settings › Acting on the game › Macros and switches"], Read("**Settings › Acting on the game › Macros and switches**, under"));
    }

    [Fact]
    public void TwoPathsInOneSentenceAreBothRead()
    {
        Assert.Equal(["On Fleet › Crew", "Commander › Missions"], Read("On Fleet › Crew, and on Commander › Missions."));
    }

    [Fact]
    public void FencedCodeAndCommentsAreLeftOut()
    {
        var page = "Text\n\n```\nSettings › Hidden\n```\n\n<!-- Settings › Commented -->\n\nSettings › Shown.\n";

        Assert.Equal(["Settings › Shown"], [.. PanelPathReader.ReadMarkdown(page).Select(path => path.ToString())]);
    }
}
