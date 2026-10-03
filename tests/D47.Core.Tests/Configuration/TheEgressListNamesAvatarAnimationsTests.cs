using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesAvatarAnimationsTests
{
    private static EgressEntry Entry(bool on)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.AvatarClips,
            settings with { Ui = settings.Ui with { AvatarClips = on } },
            llmKeyPresent: true);
    }

    [Fact]
    public void OnItSaysTheCoreIdGoesToGitHubAtMostEightTimesPerCore()
    {
        var entry = Entry(on: true);

        Assert.True(entry.Active);
        Assert.Equal("Avatar animations", entry.Name);
        Assert.Contains("at most eight requests per core per session, to github.com", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OffItSaysNothingIsFetchedAndTheAvatarDrawsItsOwnMark() =>
        Assert.Equal(
            "Avatar animations are off, so nothing is fetched and the avatar draws its own mark.",
            Entry(on: false).What);

    [Fact]
    public void TheEntryFollowsHullPicturesInTheFixedList()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.HullArt) + 1, ids.IndexOf(EgressDisclosure.AvatarClips));
    }
}
