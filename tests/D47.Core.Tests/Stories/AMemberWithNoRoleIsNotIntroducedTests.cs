using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.AStrangerIsIntroducedBeforeTheyFirstSpeakTests;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member with no role is never introduced, and still reacts and is answered as before.</summary>
public sealed class AMemberWithNoRoleIsNotIntroducedTests
{
    private const string DockHand = "dock-hand";

    [Fact]
    public void NoIntroductionIsWrittenOrOwed()
    {
        var chapter = new Adventure
        {
            Key = "yard-paint",
            Name = "Yard Paint",
            OpeningLines = [new AdventureLine { Text = "Bay four.", Speaker = DockHand }],
            Beats =
            [
                new AdventureBeat
                {
                    Title = "The Lantern",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
                    Lines = [new AdventureLine { Text = "Still here.", Speaker = DockHand }],
                },
            ],
        };

        Assert.Equal(chapter.OpeningLines, StoryIntroductions.Introduce(chapter, Running(), Secret, null).OpeningLines);
        Assert.Null(StoryIntroductions.Line(Running(), Secret, null, DockHand));
        Assert.True(StoryIntroductions.Met(Running(), Secret, DockHand));
    }

    [Fact]
    public void TheMemberIsStillAnswerable()
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.Null(fixtures.Director.Introduction("F1", DockHand));
        Assert.NotNull(fixtures.Director.Answering("F1", DockHand));
    }

    [Fact]
    public async Task ThePrimaryMemberStillReacts()
    {
        using var fixtures = new StoryFixtures(
            new Conversation.RoundScriptedLlmProvider(
                Conversation.RoundScriptedLlmProvider.Saying(Spine),
                Conversation.RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            Secret with { Cast = [Secret.Cast[0] with { Primary = true }] });

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var window = fixtures.Director.ReactionWindow("F1");

        Assert.NotNull(window);
        Assert.Equal(DockHand, Assert.Single(fixtures.Director.ReactionCast("F1", window.Due)).Id);
    }
}
