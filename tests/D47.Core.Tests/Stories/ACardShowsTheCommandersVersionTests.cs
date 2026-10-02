using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A card shows each unversioned picture, and for a member with versions the one for the Commander's gender, the other
/// when it is missing, and none while the gender is unset.
/// </summary>
public sealed class ACardShowsTheCommandersVersionTests
{
    private static readonly StoryCard Both = Card with
    {
        CastPictures = [$"{Id}.ren", $"{Id}.cray.for-man", $"{Id}.cray.for-woman", $"{Id}.ila"],
    };

    [Theory]
    [InlineData(CommanderGender.Woman, "the-test-story.cray.for-woman")]
    [InlineData(CommanderGender.Man, "the-test-story.cray.for-man")]
    public void AMemberWithVersionsShowsTheOneForTheGender(string gender, string shown) =>
        Assert.Equal([$"{Id}.ren", shown, $"{Id}.ila"], Both.PicturesFor(gender));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("other")]
    public void AMemberWithVersionsShowsNothingWhileTheGenderIsUnset(string? gender) =>
        Assert.Equal([$"{Id}.ren", $"{Id}.ila"], Both.PicturesFor(gender));

    [Fact]
    public void AMissingVersionFallsBackToTheOther()
    {
        var onlyMan = Card with { CastPictures = [$"{Id}.cray.for-man"] };
        var onlyWoman = Card with { CastPictures = [$"{Id}.cray.for-woman"] };

        Assert.Equal([$"{Id}.cray.for-man"], onlyMan.PicturesFor(CommanderGender.Woman));
        Assert.Equal([$"{Id}.cray.for-woman"], onlyWoman.PicturesFor(CommanderGender.Man));
    }
}
