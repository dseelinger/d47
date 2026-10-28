using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member is looked up by story id, so a story that is no longer current still names its members.</summary>
[Trait("Category", "Integration")]
public sealed class AFinishedStorysMemberIsFoundByItsStoryTests
{
    [Fact]
    public void AMemberOfAStoryThatIsNotCurrentIsFound()
    {
        using var fixtures = PauseSupport.Picked(out _);

        fixtures.Stories.Update("F1", Id, story => story with { State = StoryState.Finished });

        var member = fixtures.Director.Speaker("F1", Id, "dock-hand");

        Assert.NotNull(member);
        Assert.Equal("Ren", member.Name);
    }

    [Fact]
    public void AnUnknownStoryOrMemberIsNull()
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.Null(fixtures.Director.Speaker("F1", "no-such-story", "dock-hand"));
        Assert.Null(fixtures.Director.Speaker("F1", Id, "nobody"));
    }
}
