using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests;

/// <summary>A story member heard only in recordings is left out of the STORY channel's cast (#1727).</summary>
public sealed class ARecordedStoryMemberIsNotOnTheStoryChannelTests
{
    private static StoryCastMember Member(string id, bool recorded) => new(
        "salt-road",
        "Salt Road",
        new StorySpeakerShown(id, id, "edge", "voice", $"salt-road.{id}") { Primary = true },
        new PinnedVoice("edge", "voice"),
        new PinnedVoice("edge", "voice"))
    {
        Recorded = recorded,
    };

    [Fact]
    public void ARecordedMemberIsLeftOutOfTheStoryCast()
    {
        var story = new RunningCast("salt-road", "Salt Road", [Member("wren", false), Member("ghost", true)]);

        var inputs = CommsFeed.Inputs(null, [], story, []);

        Assert.Equal(["wren"], inputs.StoryCast.Select(member => member.Id));
    }
}
