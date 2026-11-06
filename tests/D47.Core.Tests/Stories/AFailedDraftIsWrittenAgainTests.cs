using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A chapter draft that cannot be used is written again, up to <see cref="StoryDirector.Drafts"/> drafts, each told why the
/// one before failed and each announced; the write stops after the last.
/// </summary>
public sealed class AFailedDraftIsWrittenAgainTests
{
    private static readonly string Stranger = Written("Factory settings, holding.", null, ("Scoop here.", "stranger"));

    [Fact]
    public async Task ASecondDraftIsToldWhyTheFirstFailedAndBegins()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Stranger),
            RoundScriptedLlmProvider.Saying(Rewritten(1, "Scoop here.", "stranger")),
            RoundScriptedLlmProvider.Saying(Rewritten(1, "Scoop here.", "stranger")),
            RoundScriptedLlmProvider.Saying(Lines()),
            RoundScriptedLlmProvider.Saying(NothingObjected)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        Assert.False(fixtures.Director.WriteFailed("F1"));
        Assert.Equal([StoryDirector.WritingAgainLine(2, told: true)], fixtures.WritingAgain);

        Assert.Null(fixtures.Asks[0].LastDraftRefused);
        Assert.Contains("stranger", fixtures.Asks[1].LastDraftRefused);

        var second = fixtures.Provider.Requests[1 + AdventureGenerator.RepairRounds].Prompt.History[0].Text;
        Assert.Contains("An earlier draft of this chapter's words could not be used", second);
        Assert.Contains("stranger", second);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task EveryLaterDraftIsWrittenAgainstThePlanTheFirstReturned()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());
        var plan = new ChapterPlan([new PlannedObjective(ChapterFunctions.Resolution, [new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 }], PlannedPurpose.Activity)], null);
        fixtures.Answers = ask => new AdventureOutcome(null, null, "The lines could not be read.", []) { Internal = true, Plan = ask.Plan ?? plan };

        Assert.Equal(StoryDirector.NoDraftStood, await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(StoryDirector.Drafts, fixtures.Asks.Count);
        Assert.Null(fixtures.Asks[0].Plan);
        Assert.All(fixtures.Asks.Skip(1), ask => Assert.Same(plan, ask.Plan));
    }

    [Fact]
    public async Task TheWriteStopsAfterTheLastDraftFails()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(EveryDraft(Stranger, Rewritten(1, "Scoop here.", "stranger"), Rewritten(1, "Scoop here.", "stranger"))));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal(StoryDirector.NoDraftStood, refusal);
        Assert.Equal(StoryDirector.Drafts, fixtures.Asks.Count);
        Assert.Equal(
            [.. Enumerable.Range(2, StoryDirector.Drafts - 1).Select(draft => StoryDirector.WritingAgainLine(draft, told: true))],
            fixtures.WritingAgain);
        Assert.True(fixtures.Director.WriteFailed("F1"));
        Assert.False(fixtures.Director.IsWriting("F1"));
    }

    [Fact]
    public async Task ACancelledWriteIsNotWrittenAgain()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        fixtures.Throws = true;

        Assert.NotNull(await fixtures.Director.PickAsync("F1", Id, Now, cancelled.Token));

        Assert.Single(fixtures.Asks);
        Assert.Empty(fixtures.WritingAgain);
    }

    [Fact]
    public void EachRetrySaysWhyItCanSucceed()
    {
        Assert.Equal(
            "The first draft of the chapter could not be used, so I am writing another. "
            + "The new draft is told what was wrong with the one before, so it can avoid the same mistake.",
            StoryDirector.WritingAgainLine(2, told: true));
        Assert.Equal(
            "The second draft could not be used either, so I am writing a final one. "
            + "No draft came back that time, and a fault like that does not usually repeat.",
            StoryDirector.WritingAgainLine(3, told: false));
    }

    [Fact]
    public async Task ADraftThatNeverCameBackIsNotPassedOnAsAMistake()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("No JSON here."),
            RoundScriptedLlmProvider.Saying(Lines())));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal([StoryDirector.WritingAgainLine(2, told: false)], fixtures.WritingAgain);
        Assert.Null(fixtures.Asks[1].LastDraftRefused);
    }

}
