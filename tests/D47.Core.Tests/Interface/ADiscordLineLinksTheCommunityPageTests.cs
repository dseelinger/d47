using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class ADiscordLineLinksTheCommunityPageTests
{
    private const string Line = "Come and say hello in the D47 Discord, Commander.";

    [Fact]
    public void TheCommunityLineLinksThePhrase()
    {
        var place = Community.Find(Line, Community.SourceKey);

        Assert.NotNull(place);
        Assert.Equal(Line.IndexOf(Community.Phrase, StringComparison.Ordinal), place.Start);
        Assert.Equal(Community.Phrase.Length, place.Length);
        Assert.Equal(PanelTab.Settings, place.Tab);
        Assert.Equal(AboutCapability.CommunityKey, place.SettingKey);
        Assert.Equal(Community.Url, place.Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("raven.colonial")]
    public void AnyOtherSourceKeyLinksNothing(string? key) =>
        Assert.Null(Community.Find(Line, key));

    [Fact]
    public void ALineWithoutThePhraseLinksNothing() =>
        Assert.Null(Community.Find("Come and say hello, Commander.", Community.SourceKey));
}
