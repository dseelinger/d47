using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A detour is one objective held outside the chapter count. It sets the chapter it interrupts aside, and once it fires that
/// chapter is written again at the same number, told it was set aside rather than failed.
/// </summary>
public sealed class ADetourSetsItsChapterAsideTests
{
    private const long DetourAddress = 9_900_000_001;

    private const string Words = "I need to fit a fuel scoop before going any further.";

    private static readonly DateTimeOffset Third = Now.AddDays(6);

    private static readonly AdventureTrigger Arrive = new() { Kind = TriggerKind.Arrive, System = "Detour Star", SystemAddress = DetourAddress };

    private static IReadOnlyList<D47.Core.Conversation.LlmStreamEvent>[] Rounds(params string[] rounds) =>
        [.. rounds.Select(round => RoundScriptedLlmProvider.Saying(round))];

    /// <summary>A story on its third chapter with that chapter's first objective done; <paramref name="rewrite"/> are the rounds after.</summary>
    private static async Task<(StoryFixtures Fixtures, string Chapter)> OnChapterThree(params string[] rewrite)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(Rounds(
            [Spine, BeatsToTheBeacon, NothingObjected, NextSpine, NextBeats, NothingObjected, NextSpine, NextBeats, NothingObjected, .. rewrite])));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now.AddDays(3));
        Assert.Null(await fixtures.Director.Tick("F1", Third)!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(3, story.Chapters.Count);

        var chapter = fixtures.Book.Store.Find("F1", story.CurrentChapter!)!;
        fixtures.Book.Observe(AdventureFixtures.Jump(chapter.Beats[0].Trigger.SystemAddress!.Value, Third.AddMinutes(1)), "F1");
        Assert.Equal(1, fixtures.Book.Standing("F1", chapter.Key)!.Current);

        return (fixtures, chapter.Key);
    }

    [Fact]
    public async Task TheDetourIsTheOnlyObjectiveAndNoChapterIsAdded()
    {
        var (fixtures, third) = await OnChapterThree();
        using var _ = fixtures;
        var asks = fixtures.Asks.Count;
        fixtures.Book.Drain();

        Assert.Null(fixtures.Director.SetDetour("F1", Arrive, "Fit a scoop", Words, Third.AddMinutes(2)));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(3, story.Chapters.Count);
        Assert.Equal(third, story.CurrentChapter);
        Assert.Equal(third, story.Detour!.Chapter);
        Assert.Contains(third, story.ChangedChapters);

        var active = Assert.Single(fixtures.Book.Active("F1"));
        Assert.Equal(story.Detour.Key, active.Adventure.Key);
        Assert.Equal(Arrive, active.CurrentTrigger);
        Assert.Empty(active.Adventure.Beats.SelectMany(beat => beat.Lines));
        Assert.Equal(story.Detour.Key, fixtures.Director.CurrentObjective("F1")!.Standing.Adventure.Key);

        var setAside = fixtures.Book.Standing("F1", third)!;
        Assert.True(setAside.Adventure.IsSetAside);
        Assert.Equal(Words, setAside.Adventure.Ending!.Reason);
        Assert.False(setAside.IsFailed);
        Assert.False(setAside.Adventure.IsFailed);
        Assert.DoesNotContain(fixtures.Book.Drain(), moment => moment.IsFailure);

        // The set-aside chapter's next objective no longer counts, and no chapter is written while the detour is open.
        var dock = setAside.Adventure.Beats[1].Trigger;
        fixtures.Book.Observe(AdventureFixtures.Docked(dock.MarketId!.Value, Third.AddMinutes(3)), "F1");
        Assert.Equal(1, fixtures.Book.Standing("F1", third)!.Current);
        Assert.Null(fixtures.Director.Tick("F1", Third.AddMinutes(4)));
        Assert.Equal(asks, fixtures.Asks.Count);
        Assert.Equal("A detour is under way.", await fixtures.Director.WriteNextAsync("F1", Third.AddMinutes(4), CancellationToken.None));
        Assert.Equal("A detour is already under way.", fixtures.Director.SetDetour("F1", Arrive, "Again", Words, Third.AddMinutes(5)));
    }

    [Fact]
    public async Task TheFiredDetourWritesTheChapterAgainAtTheSameNumber()
    {
        var (fixtures, third) = await OnChapterThree(NextSpine, NextBeats, NothingObjected);
        using var _ = fixtures;
        var before = fixtures.Stories.Current("F1")!;

        Assert.Null(fixtures.Director.SetDetour("F1", Arrive, "Fit a scoop", Words, Third.AddMinutes(2)));
        var detour = fixtures.Stories.Current("F1")!.Detour!;

        fixtures.Book.Observe(AdventureFixtures.Jump(DetourAddress, Third.AddMinutes(10), "Detour Star"), "F1");
        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(11))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Null(story.Detour);
        Assert.Equal(3, story.Chapters.Count);
        Assert.Equal(before.Chapters.Take(2), story.Chapters.Take(2));
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(before.FinaleFrom, story.FinaleFrom);
        Assert.Contains(story.CurrentChapter!, story.ChangedChapters);
        Assert.True(fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.IsActive);
        Assert.Null(fixtures.Book.Store.Find("F1", detour.Key));
        Assert.Null(fixtures.Book.Store.Find("F1", third));
        Assert.Equal(AdventureEnding.SetAside, fixtures.Director.Archive.Find("F1", story, third)!.Adventure.Ending!.Name);

        var ask = fixtures.Asks[^1];
        Assert.Equal(3, ask.Story!.Chapter);
        Assert.Null(ask.Failed);
        Assert.Equal(third, ask.SetAside!.Chapter.Previous.Key);
        Assert.Equal(["The Lantern Again"], ask.SetAside.Done);
        Assert.Equal(Words, ask.SetAside.Words);

        var prompts = fixtures.Provider.Requests.Skip(9).Select(request => request.Prompt.History[0].Text).ToList();
        Assert.Contains(prompts, prompt => prompt.Contains("set an earlier version of this chapter aside to go somewhere else", StringComparison.Ordinal));
        Assert.Contains(prompts, prompt => prompt.Contains("Objectives already done: The Lantern Again", StringComparison.Ordinal));
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("failed", StringComparison.OrdinalIgnoreCase) && prompt.Contains("earlier attempt", StringComparison.Ordinal));
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("Write the next chapter, not a retelling", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADetourInAFinaleChapterKeepsThatFinaleChapter()
    {
        var (fixtures, third) = await OnChapterThree(NextSpine, NextBeatsNamingTheEnd, NothingObjected);
        using var _ = fixtures;
        fixtures.Stories.Update("F1", Id, story => story with { FinaleFrom = 3 });
        Assert.Equal(1, fixtures.Stories.Current("F1")!.FinaleChapter);

        Assert.Null(fixtures.Director.SetDetour("F1", Arrive, "Fit a scoop", Words, Third.AddMinutes(2)));

        // The detour's opening has been said, as the callout does before Mark done is offered.
        fixtures.Book.Quiet("F1", fixtures.Stories.Current("F1")!.Detour!.Key);
        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", Third.AddMinutes(10)));
        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(11))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(3, story.Chapters.Count);
        Assert.Equal(3, story.FinaleFrom);
        Assert.Equal(1, story.FinaleChapter);
        Assert.Equal(1, fixtures.Asks[^1].Story!.FinaleChapter);
    }

    [Fact]
    public async Task AnOpenDetourSurvivesARestart()
    {
        var (fixtures, third) = await OnChapterThree();
        using var _ = fixtures;
        Assert.Null(fixtures.Director.SetDetour("F1", Arrive, "Fit a scoop", Words, Third.AddMinutes(2)));
        var detour = fixtures.Stories.Current("F1")!.Detour!;

        using var reopened = new StoryFixtures(new RoundScriptedLlmProvider(Rounds(NextSpine, NextBeats, NothingObjected)), reopening: fixtures);
        reopened.Book.Store.Poll();
        reopened.Director.ArchiveChapters();

        var reloaded = reopened.Stories.Current("F1")!.Detour!;
        Assert.Equal((detour.Key, detour.Chapter, detour.Words, detour.At), (reloaded.Key, reloaded.Chapter, reloaded.Words, reloaded.At));
        Assert.Equal(detour.Done, reloaded.Done);
        // The reopened book walks no journals, so only what the files hold is checked: the detour is under way and its chapter is not.
        var active = reopened.Book.Active("F1").Select(standing => standing.Adventure.Key).ToList();
        Assert.Contains(detour.Key, active);
        Assert.DoesNotContain(third, active);
        Assert.True(reopened.Book.Store.Find("F1", third)!.IsSetAside);
        Assert.Null(reopened.Director.Tick("F1", Third.AddMinutes(5)));

        reopened.Book.Observe(AdventureFixtures.Jump(DetourAddress, Third.AddMinutes(10), "Detour Star"), "F1");
        Assert.Null(await reopened.Director.Tick("F1", Third.AddMinutes(11))!);

        var story = reopened.Stories.Current("F1")!;
        Assert.Null(story.Detour);
        Assert.Equal(3, story.Chapters.Count);
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(["The Lantern Again"], reopened.Asks[^1].SetAside!.Done);
    }

    [Fact]
    public async Task AnAbandonedDetourPausesTheStoryAndResumeBeginsItAgain()
    {
        var (fixtures, third) = await OnChapterThree();
        using var _ = fixtures;
        Assert.Null(fixtures.Director.SetDetour("F1", Arrive, "Fit a scoop", Words, Third.AddMinutes(2)));
        var detour = fixtures.Stories.Current("F1")!.Detour!;

        Assert.Null(fixtures.Book.Abandon("F1", detour.Key, Third.AddMinutes(3)));
        Assert.Null(fixtures.Director.Tick("F1", Third.AddMinutes(4)));
        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", Third.AddMinutes(5), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(StoryState.Running, story.State);
        Assert.Equal(detour.Key, story.Detour!.Key);
        Assert.True(fixtures.Book.Standing("F1", detour.Key)!.Adventure.IsActive);
        Assert.True(fixtures.Book.Store.Find("F1", third)!.IsSetAside);
    }

    [Fact]
    public async Task ADetourNoLongerOnFilePausesTheStoryAndResumeWritesTheChapterAgain()
    {
        var (fixtures, third) = await OnChapterThree(NextSpine, NextBeats, NothingObjected);
        using var _ = fixtures;
        Assert.Null(fixtures.Director.SetDetour("F1", Arrive, "Fit a scoop", Words, Third.AddMinutes(2)));
        var asks = fixtures.Asks.Count;

        fixtures.Book.Remove("F1", fixtures.Stories.Current("F1")!.Detour!.Key);
        Assert.Null(fixtures.Director.Tick("F1", Third.AddMinutes(3)));
        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);
        Assert.Equal(asks, fixtures.Asks.Count);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", Third.AddMinutes(4), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(StoryState.Running, story.State);
        Assert.Null(story.Detour);
        Assert.Equal(3, story.Chapters.Count);
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(third, fixtures.Asks[^1].SetAside!.Chapter.Previous.Key);
    }

    [Fact]
    public async Task ADetourMayNotAskForARefusedActivity()
    {
        var (fixtures, third) = await OnChapterThree();
        using var _ = fixtures;
        fixtures.Stories.Update("F1", Id, story => story with { Refused = ["bounty"] });

        var refusal = fixtures.Director.SetDetour("F1", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 3 }, "Hunt", Words, Third.AddMinutes(2));

        Assert.NotNull(refusal);
        Assert.Null(fixtures.Stories.Current("F1")!.Detour);
        Assert.True(fixtures.Book.Standing("F1", third)!.Adventure.IsActive);
        Assert.Equal(third, Assert.Single(fixtures.Book.Active("F1")).Adventure.Key);
    }
}
