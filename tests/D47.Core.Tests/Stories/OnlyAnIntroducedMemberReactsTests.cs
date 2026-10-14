using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member with a role neither reacts nor is answered until the narrator has introduced them.</summary>
public sealed class OnlyAnIntroducedMemberReactsTests
{
    private const string DockHand = "dock-hand";

    private static readonly StorySecret Introducible = Secret with
    {
        Cast = [Secret.Cast[0] with { Primary = true, Role = "the dock hand who signs the Commander's berth" }],
    };

    private static StoryFixtures Picked()
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            Introducible);

        Assert.Null(fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None).GetAwaiter().GetResult());
        return fixtures;
    }

    [Fact]
    public void AMemberNotYetIntroducedNeitherReactsNorIsAnswered()
    {
        using var fixtures = Picked();

        Assert.Null(fixtures.Director.ReactionWindow("F1"));
        Assert.Empty(fixtures.Director.ReactionCast("F1", new StoryReactionDue(Id, 0)));
        Assert.Null(fixtures.Director.Answering("F1", DockHand));
    }

    [Fact]
    public void OnceIntroducedTheMemberReactsAndIsAnswered()
    {
        using var fixtures = Picked();

        fixtures.Director.Introduced("F1", Id, DockHand);

        var window = fixtures.Director.ReactionWindow("F1");

        Assert.NotNull(window);
        Assert.Equal(DockHand, Assert.Single(fixtures.Director.ReactionCast("F1", window.Due)).Id);

        var answering = fixtures.Director.Answering("F1", DockHand);

        Assert.NotNull(answering);
        Assert.Equal("the dock hand who signs the Commander's berth", answering.Role);
    }
}
