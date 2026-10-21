using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter whose failsOn trigger matched is stored as failed, archived, and written again in its place.</summary>
public sealed class AFailedStoryChapterIsReplacedTests
{
    private static readonly AdventureTrigger Bounty = new() { Kind = TriggerKind.Bounty };

    private static StoryFixtures Fixtures(params string[] rounds) =>
        new(new RoundScriptedLlmProvider([.. rounds.Select(round => RoundScriptedLlmProvider.Saying(round))]));

    private static D47.Core.Journal.JournalEvent BountyAt(DateTimeOffset at) =>
        D47.Core.Tests.Adventures.AdventureFixtures.Event($$"""{ "timestamp":"{{D47.Core.Tests.Adventures.AdventureFixtures.Stamp(at)}}", "event":"Bounty", "Rewards":[{"Faction":"Federation","Reward":500}], "Target":"sidewinder", "TotalReward":500, "VictimFaction":"Pirates" }""");

    private static void FailOnBounty(StoryFixtures fixtures, string key)
    {
        var chapter = fixtures.Book.Store.Find("F1", key)!;

        Assert.Null(fixtures.Book.Write("F1", chapter with { Beats = [.. chapter.Beats.Select((beat, index) => index == 0 ? beat with { FailsOn = [Bounty] } : beat)] }));
    }

    private static async Task Replace(StoryFixtures fixtures, DateTimeOffset at)
    {
        var storing = fixtures.Director.Tick("F1", at);
        Assert.NotNull(storing);
        Assert.Null(await storing);

        var writing = fixtures.Director.Tick("F1", at);
        Assert.NotNull(writing);
        Assert.Null(await writing);
    }

    [Fact]
    public async Task TheFirstChapterFailsAndAnotherTakesItsPlace()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon, NothingObjected, Spine, BeatsToTheBeacon, NothingObjected);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        FailOnBounty(fixtures, first);

        fixtures.Book.Observe(BountyAt(Now.AddMinutes(1)), "F1");
        Assert.True(fixtures.Book.Standing("F1", first)!.IsFailed);

        await Replace(fixtures, Now.AddMinutes(2));

        var story = fixtures.Stories.Current("F1")!;
        var replacement = Assert.Single(story.Chapters);
        Assert.NotEqual(first, replacement);
        Assert.True(fixtures.Book.Store.Find("F1", replacement)!.IsActive);
        Assert.Null(fixtures.Book.Store.Find("F1", first));

        var archived = fixtures.Director.Archive.Find("F1", story, first)!;
        Assert.Equal(AdventureEnding.Failed, archived.Adventure.Ending!.Name);
        Assert.Equal(story.PickedAt, archived.PickedAt);

        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(3)));
    }

    [Fact]
    public async Task ALaterChapterFailsAndTheFinaleDoesNotAdvance()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon, NothingObjected, NextSpine, NextBeats, NothingObjected, NextSpine, NextBeats, NothingObjected);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        fixtures.Finish("F1", first, Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);

        var before = fixtures.Stories.Current("F1")!;
        var failed = before.Chapters[1];
        FailOnBounty(fixtures, failed);

        var failedAt = Now.AddDays(3).AddMinutes(1);
        fixtures.Book.Observe(BountyAt(failedAt), "F1");

        await Replace(fixtures, failedAt.AddMinutes(1));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Chapters.Count);
        Assert.Equal(first, story.Chapters[0]);
        Assert.NotEqual(failed, story.Chapters[1]);
        Assert.Equal(before.FinaleFrom, story.FinaleFrom);
        Assert.Equal(before.FinaleChapter, story.FinaleChapter);

        var replacement = fixtures.Book.Store.Find("F1", story.Chapters[1])!;
        Assert.Equal(first, replacement.Follows);
        Assert.Contains(failed, fixtures.Director.Archive.For("F1").Where(chapter => chapter.Of(story)).Select(chapter => chapter.Adventure.Key));

        var ask = fixtures.Asks[^1];
        Assert.Equal(first, ask.Chapter!.Previous.Key);
        Assert.Equal(failed, ask.Failed!.Previous.Key);
        Assert.Contains("The Lantern Again", ask.Failed.Previous.Ending!.Reason);

        var prompts = fixtures.Provider.Requests.Skip(6).Select(request => request.Prompt.History[0].Text).ToList();
        Assert.Contains(prompts, prompt => prompt.Contains("An earlier attempt at this chapter failed", StringComparison.Ordinal));
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("Write the next chapter, not a retelling", StringComparison.Ordinal));
    }
}
