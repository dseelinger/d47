using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A primary cast member is named in the card's blurb or inYourWords and never speaks in the Commander's own voice; a
/// member with versions is the one exception to the naming rule, since the card calls it by role (#737).
/// </summary>
public sealed class EveryPrimaryCastMemberIsOnTheCardTests
{
    private static StorySecret WithDockHand(StorySpeaker dockHand) =>
        Secret with { Cast = [dockHand] };

    private static readonly StorySpeaker Ren = Secret.Cast.Single() with { Primary = true };

    private static StoryCard Pictured(StoryCard card, params string[] pictures) => card with { CastPictures = pictures };

    [Fact]
    public void APrimaryMemberNamedInTheBlurbPasses()
    {
        var card = Pictured(Card with { Blurb = "Ren hears a song." }, $"{Id}.dock-hand");

        Assert.Empty(StoryCatalog.Faults(card, WithDockHand(Ren)));
    }

    [Fact]
    public void APrimaryMemberNamedInTheCommandersWordsPasses()
    {
        var card = Pictured(Card with { InYourWords = "Ren sold me the Sidewinder." }, $"{Id}.dock-hand");

        Assert.Empty(StoryCatalog.Faults(card, WithDockHand(Ren)));
    }

    [Fact]
    public void APrimaryMemberTheCardDoesNotNameFails()
    {
        var faults = StoryCatalog.Faults(Pictured(Card, $"{Id}.dock-hand"), WithDockHand(Ren));

        Assert.Contains($"{Id}: dock-hand is primary, and neither blurb nor inYourWords names them.", faults);
    }

    [Fact]
    public void APrimaryMemberInTheCommandersOwnVoiceFails()
    {
        var own = Ren with { Provider = StorySpeaker.Chatterbox, Voice = StorySpeaker.Own };
        var faults = StoryCatalog.Faults(Pictured(Card with { Blurb = "Ren hears a song." }, $"{Id}.dock-hand"), WithDockHand(own));

        Assert.Contains($"{Id}: dock-hand is primary and speaks in the Commander's own voice.", faults);
    }

    [Fact]
    public void CrayWithVersionsIsPrimaryWithNeitherNameOnTheCard()
    {
        var secret = Versioned with
        {
            Cast = [Versioned.Cast[0], Versioned.Cast[1] with { Primary = true }],
        };
        var card = Pictured(Card, $"{Id}.cray.for-man", $"{Id}.cray.for-woman");

        Assert.Empty(StoryCatalog.Faults(card, secret));
    }
}
