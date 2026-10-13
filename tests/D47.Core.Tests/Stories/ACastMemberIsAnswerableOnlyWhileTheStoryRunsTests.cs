using Xunit;
using static D47.Core.Tests.Stories.OdysseySupport;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member can be answered while their story runs, and not while it is paused, switched off or without Odyssey.</summary>
[Trait("Category", "Integration")]
public sealed class ACastMemberIsAnswerableOnlyWhileTheStoryRunsTests
{
    private const string DockHand = "dock-hand";

    [Fact]
    public void ARunningStorysMemberIsAnswerableAsTheCommanderMeetsThem()
    {
        using var fixtures = PauseSupport.Picked(out _);

        var member = fixtures.Director.Answering("F1", DockHand);

        Assert.NotNull(member);
        Assert.Equal("Ren", member.Name);
        Assert.Equal("A tired dock hand, short words.", member.Who);
        Assert.Null(fixtures.Director.Answering("F1", "nobody"));
    }

    [Fact]
    public void ASwitchedOffStorysMemberIsNot()
    {
        using var fixtures = PauseSupport.Picked(out _);

        fixtures.Director.SetOn("F1", false, Now);

        Assert.Null(fixtures.Director.Answering("F1", DockHand));
    }

    [Fact]
    public void APausedStorysMemberIsNot()
    {
        using var fixtures = PauseSupport.Picked(out _);

        fixtures.Stories.Update("F1", Id, story => story.Paused(Now));

        Assert.Null(fixtures.Director.Answering("F1", DockHand));
    }

    [Fact]
    public void AMemberIsNotAnswerableWithoutOdyssey()
    {
        using var fixtures = PauseSupport.Picked(out _);

        fixtures.Director.Observe(LoadGame(Now.AddHours(1), odyssey: false), "F1");

        Assert.Null(fixtures.Director.Answering("F1", DockHand));
    }
}
