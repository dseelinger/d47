using D47.App.Voice;
using D47.Core.Callouts;
using Xunit;

namespace D47.App.Tests;

public sealed class ATranslatedLineShowsItsEnglishInTheTranscriptTests
{
    [Fact]
    public void TheEnglishFollowsTheLineThatWasHeard()
    {
        var line = new Announcement("story.clue", "無事でよかった。") { Language = "ja", Translation = "Good to see you made it." };

        Assert.Equal("無事でよかった。\nGood to see you made it.", Announcer.WithTranslation(line));
    }

    [Fact]
    public void ALineWithoutATranslationIsShownAsHeard()
    {
        Assert.Equal("Fuel low.", Announcer.WithTranslation(new Announcement("fuel.low", "Fuel low.")));
    }
}
