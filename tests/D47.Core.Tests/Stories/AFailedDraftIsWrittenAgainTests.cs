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
    [Fact]
    public async Task ASecondDraftIsToldWhyTheFirstFailedAndBegins()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsElsewhere, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsElsewhere, 2)),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        Assert.False(fixtures.Director.WriteFailed("F1"));
        Assert.Equal([StoryDirector.WritingAgainLine(2, told: true)], fixtures.WritingAgain);

        Assert.Null(fixtures.Asks[0].LastDraftRefused);
        Assert.Contains("The last objective must be \"beacon\"", fixtures.Asks[1].LastDraftRefused);

        var beats = fixtures.Provider.Requests[4].Prompt.History[0].Text;
        Assert.Contains("An earlier draft of this chapter could not be used", beats);
        Assert.Contains("The last objective must be \"beacon\"", beats);
    }

    [Fact]
    public async Task TheWriteStopsAfterTheLastDraftFails()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(EveryDraft(Spine, BeatsElsewhere, Repairs.Replacing(BeatsElsewhere, 2), Repairs.Replacing(BeatsElsewhere, 2))));

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
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal([StoryDirector.WritingAgainLine(2, told: false)], fixtures.WritingAgain);
        Assert.Null(fixtures.Asks[1].LastDraftRefused);
    }

    [Fact]
    public async Task ASecondDraftKeepsTheOutlineOfAFirstWhoseObjectivesNeverCameBack()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying("No JSON here."),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(4, fixtures.Provider.CallCount);
        Assert.Null(fixtures.Asks[0].Outline);
        Assert.NotNull(fixtures.Asks[1].Outline);
    }

    [Fact]
    public async Task AThirdDraftWritesANewOutline()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying("No JSON here."),
            RoundScriptedLlmProvider.Saying("No JSON here."),
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(6, fixtures.Provider.CallCount);
        Assert.NotNull(fixtures.Asks[1].Outline);
        Assert.Null(fixtures.Asks[2].Outline);
    }

    [Fact]
    public async Task ASecondDraftWritesANewOutlineWhenTheFirstOneWasUnreadable()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("""{"name": 3}"""),
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(4, fixtures.Provider.CallCount);
        Assert.Null(fixtures.Asks[1].Outline);
    }
}
