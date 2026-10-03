using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A card lists the pictures of its primary cast members. The catalog check refuses a name that is not a primary
/// member's picture, and a primary member's picture the card leaves out.
/// </summary>
public sealed class AStoryCardShowsOnlyItsPrimaryCastTests
{
    private static readonly StorySecret Cast = Secret with
    {
        Cast =
        [
            new StorySpeaker { Id = "dock-hand", Name = "Ren", Who = "A tired dock hand.", Provider = StorySpeaker.Kokoro, Voice = "bm_george", Primary = true },
            new StorySpeaker { Id = "clerk", Name = "Tam", Who = "A clerk.", Provider = StorySpeaker.Kokoro, Voice = "bm_george" },
            new StorySpeaker { Id = "pilot", Name = "Ila", Who = "A pilot.", Provider = StorySpeaker.Kokoro, Voice = "af_heart", Primary = true },
        ],
        Clues = [.. Secret.Clues.Select(line => line.Speaker == "dock-hand" ? line with { Speaker = StorySpeaker.Ship } : line)],
    };

    private static readonly string[] Primary = [$"{Id}.dock-hand", $"{Id}.pilot"];

    [Fact]
    public void TheCardNamesThePicturesOfItsPrimaryMembersInCastOrder()
    {
        var card = Card with { Blurb = "Ren and Ila hear a song.", CastPictures = Primary };

        Assert.Empty(new StoryCatalog([card], () => [Cast]).Faults());
        Assert.Equal(Primary, card.PicturesFor(null));
    }

    [Fact]
    public void ANameThatIsNotAPrimaryMemberIsRefused()
    {
        var card = Card with { CastPictures = [.. Primary, $"{Id}.clerk", $"{Id}.nobody"] };

        var faults = new StoryCatalog([card], () => [Cast]).Faults();

        Assert.Contains(faults, fault => fault.Contains($"castPictures names {Id}.clerk,", StringComparison.Ordinal));
        Assert.Contains(faults, fault => fault.Contains($"castPictures names {Id}.nobody,", StringComparison.Ordinal));
    }

    [Fact]
    public void APrimaryMemberTheCardLeavesOutIsRefused()
    {
        var card = Card with { CastPictures = [Primary[0]] };

        var faults = new StoryCatalog([card], () => [Cast]).Faults();

        Assert.Contains(faults, fault => fault.Contains($"castPictures is missing {Id}.pilot,", StringComparison.Ordinal));
        Assert.DoesNotContain(faults, fault => fault.Contains($"castPictures is missing {Id}.dock-hand", StringComparison.Ordinal));
    }

    [Fact]
    public void ACardWithNoPrimaryMemberHasNoPictures()
    {
        Assert.Empty(Card.CastPictures);
        Assert.Empty(Card.PicturesFor(CommanderGender.Woman));
        Assert.Empty(new StoryCatalog([Card], () => [Secret]).Faults());
    }

    [Fact]
    public void ACardNamesBothVersionsOfAPrimaryMemberWithVersions()
    {
        var versioned = Versioned with
        {
            Cast = [.. Versioned.Cast.Select(speaker => speaker.Id == "cray" ? speaker with { Primary = true } : speaker)],
        };

        Assert.Contains(
            new StoryCatalog([Card], () => [versioned]).Faults(),
            fault => fault.Contains($"castPictures is missing {Id}.cray.for-man,", StringComparison.Ordinal));

        var card = Card with { CastPictures = [$"{Id}.cray.for-man", $"{Id}.cray.for-woman"] };

        Assert.Empty(new StoryCatalog([card], () => [versioned]).Faults());
    }
}
