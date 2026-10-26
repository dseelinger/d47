using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class AStoryMayHaveAnyNumberOfCastTests
{
    [Fact]
    public void AHiddenEntryWithEightCastMembersHasNoFaults()
    {
        var cast = Enumerable.Range(1, 7)
            .Select(at => new StorySpeaker { Id = $"member-{at}", Name = $"Member {at}", Who = "A steady voice, short words.", Provider = StorySpeaker.Kokoro, Voice = "bm_george" });
        var secret = Secret with { Cast = [.. Secret.Cast, .. cast] };

        Assert.Equal(8, secret.Cast.Count);
        Assert.Empty(new StoryCatalog([Card], () => [secret]).Faults());
    }
}
