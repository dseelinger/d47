using D47.Core.Adventures;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// The Commander can have the current chapter written again from where they are, with no detour: the chapter is set aside and
/// written again at the same number, told the Commander asked for it, keeping its finale destination or beacon.
/// </summary>
public sealed class AChapterCanBeWrittenAgainFromHereTests
{
    private const long DetourAddress = 9_900_000_001;

    private static readonly DateTimeOffset Third = Now.AddDays(6);

    private static IReadOnlyList<D47.Core.Conversation.LlmStreamEvent>[] Rounds(params string[] rounds) =>
        [.. rounds.Select(round => RoundScriptedLlmProvider.Saying(round))];

    /// <summary>A story on its third chapter with that chapter's first objective done, so it waits on its last; <paramref name="after"/> are the rounds after.</summary>
    private static async Task<(StoryFixtures Fixtures, string Chapter)> OnChapterThree(params string[] after)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(Rounds(
            [Spine, BeatsToTheBeacon, NothingObjected, NextSpine, NextBeats, NothingObjected, NextSpine, NextBeats, NothingObjected, .. after])));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now.AddDays(3));
        Assert.Null(await fixtures.Director.Tick("F1", Third)!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(3, story.Chapters.Count);

        var chapter = fixtures.Book.Store.Find("F1", story.CurrentChapter!)!;
        fixtures.Book.Observe(AdventureFixtures.Jump(chapter.Beats[0].Trigger.SystemAddress!.Value, Third.AddMinutes(1)), "F1");
        Assert.Equal(chapter.Beats.Count - 1, fixtures.Book.Standing("F1", chapter.Key)!.Current);

        return (fixtures, chapter.Key);
    }

    [Fact]
    public async Task TheChapterIsWrittenAgainAtTheSameNumberAndToldTheCommanderAskedForIt()
    {
        var (fixtures, third) = await OnChapterThree(NextSpine, NextBeats, NothingObjected);
        using var _ = fixtures;
        var barsPath = Path.Combine(MemoryInstall.FakeRoot, "activity-bars.json");
        fixtures.Director.Bars = new ActivityBars(barsPath, fixtures.Files, NullLogger.Instance);
        var before = fixtures.Stories.Current("F1")!;
        var requests = fixtures.Provider.Requests.Count;
        fixtures.Book.Drain();

        Assert.Null(fixtures.Director.RewriteChapter("F1", Third.AddMinutes(2)));

        var marked = fixtures.Stories.Current("F1")!;
        Assert.Equal(third, marked.Rewrite!.Chapter);
        Assert.Equal(["The Lantern Again"], marked.Rewrite.Done);
        Assert.True(fixtures.Book.Store.Find("F1", third)!.IsSetAside);
        Assert.DoesNotContain(fixtures.Book.Drain(), moment => moment.IsFailure);

        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(3))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Null(story.Rewrite);
        Assert.Equal(1, story.Rewrites);
        Assert.Equal(3, story.Chapters.Count);
        Assert.Equal(before.Chapters.Take(2), story.Chapters.Take(2));
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(before.FinaleFrom, story.FinaleFrom);
        Assert.Contains(third, story.ChangedChapters);
        Assert.Contains(story.CurrentChapter!, story.ChangedChapters);
        Assert.True(fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.IsActive);
        Assert.Equal(AdventureEnding.SetAside, fixtures.Director.Archive.Find("F1", story, third)!.Adventure.Ending!.Name);

        var ask = fixtures.Asks[^1];
        Assert.Equal(3, ask.Story!.Chapter);
        Assert.Null(ask.Failed);
        Assert.Equal(third, ask.SetAside!.Chapter.Previous.Key);
        Assert.Equal(["The Lantern Again"], ask.SetAside.Done);
        Assert.Null(ask.SetAside.Words);

        var prompts = fixtures.Provider.Requests.Skip(requests).Select(request => request.Prompt.History[0].Text).ToList();
        Assert.Contains(prompts, prompt => prompt.Contains("The Commander asked for this chapter to be written again from where they are now", StringComparison.Ordinal));
        Assert.Contains(prompts, prompt => prompt.Contains("Objectives already done: The Lantern Again", StringComparison.Ordinal));
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("to go somewhere else", StringComparison.Ordinal));
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("Why they set it aside", StringComparison.Ordinal));

        Assert.Null(fixtures.Files.ReadText(barsPath));
    }

    [Fact]
    public async Task EachRewriteAtTheSameNumberGetsADifferentDraw()
    {
        var (fixtures, _) = await OnChapterThree(NextSpine, NextBeats, NothingObjected, NextSpine, NextBeats, NothingObjected);
        using var __ = fixtures;

        Assert.Null(fixtures.Director.RewriteChapter("F1", Third.AddMinutes(2)));
        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(3))!);
        Assert.Equal(1, fixtures.Stories.Current("F1")!.Rewrites);
        var first = Drawn(fixtures.Asks[^1]);

        Assert.Null(fixtures.Director.RewriteChapter("F1", Third.AddMinutes(4)));
        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(5))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Rewrites);
        Assert.Equal(3, story.Chapters.Count);
        Assert.NotEqual(first, Drawn(fixtures.Asks[^1]));
        Assert.Equal(ChapterDraw.SightSeed(Id, 3), ChapterDraw.SightSeed(Id, 3, 0));
    }

    private static string Drawn(AdventureAsk ask) =>
        string.Join("|", Assert.IsAssignableFrom<IReadOnlyList<DrawnActivity>>(ask.Story!.Slots).Select(slot => $"{slot.Kind}{slot.Family}{slot.Count}"));

    [Fact]
    public async Task FinaleChapterOneKeepsItsDestination()
    {
        var (fixtures, third) = await OnChapterThree(NextSpine, NextBeatsNamingTheEnd, NothingObjected);
        using var _ = fixtures;
        var destination = new AdventureDestination(1_234_567, "Ossen's Lantern", 9, "Ossen's Lantern 3 b");
        fixtures.Stories.Update("F1", Id, story => story with { FinaleFrom = 3, FinaleDestination = destination });
        Assert.Equal(1, fixtures.Stories.Current("F1")!.FinaleChapter);

        Assert.Null(fixtures.Director.RewriteChapter("F1", Third.AddMinutes(2)));
        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(3))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(1, story.FinaleChapter);
        Assert.Equal(destination, story.FinaleDestination);
        Assert.Equal(destination, fixtures.Asks[^1].Story!.Destination);
    }

    [Fact]
    public async Task ABeaconChapterIsWrittenAgainTowardTheBeaconNearestTheCommanderNow()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(Rounds(Spine, BeatsToTheBeacon, NothingObjected, NextSpine, NextBeatsWithABeacon, NothingObjected)));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        Assert.Equal(BeaconAddress, fixtures.Asks[^1].Story!.Beacon!.SystemAddress);

        fixtures.Here = GuardianCores.BeaconPositions[SecondBeaconAddress];

        Assert.Null(fixtures.Director.RewriteChapter("F1", Now.AddMinutes(5)));
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddMinutes(6))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Single(story.Chapters);
        Assert.NotEqual(first, story.CurrentChapter);
        Assert.Null(story.BeaconScanAt);

        var ask = fixtures.Asks[^1];
        Assert.Equal(1, ask.Story!.Chapter);
        Assert.Equal(SecondBeaconAddress, ask.Story.Beacon!.SystemAddress);
        Assert.Null(ask.Story.BeaconAway);
        var beacon = fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.Beats[^1].Trigger;
        Assert.Equal(TriggerKind.Beacon, beacon.Kind);
        Assert.Equal(SecondBeaconAddress, beacon.SystemAddress);
    }

    [Fact]
    public async Task AShipThatCannotReachABeaconIsRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(Rounds(Spine, BeatsToTheBeacon, NothingObjected)));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        fixtures.Director.Game = () => Flying(7.6, scoop: false);

        Assert.Equal(
            "The ship you are in cannot reach a Guardian beacon: no fuel scoop is fitted.",
            fixtures.Director.RewriteChapter("F1", Now.AddMinutes(5)));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Null(story.Rewrite);
        Assert.Equal(first, story.CurrentChapter);
        Assert.True(fixtures.Book.Store.Find("F1", first)!.IsActive);
    }

    [Fact]
    public async Task ARewriteDuringADetourDropsTheDetourAndWritesTheChapterItSetAside()
    {
        var (fixtures, third) = await OnChapterThree(NextSpine, NextBeats, NothingObjected);
        using var _ = fixtures;
        var arrive = new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Detour Star", SystemAddress = DetourAddress };
        Assert.Null(fixtures.Director.SetDetour("F1", arrive, "Fit a scoop", "I need a fuel scoop first.", Third.AddMinutes(2)));
        var detour = fixtures.Stories.Current("F1")!.Detour!;

        Assert.Null(fixtures.Director.RewriteChapter("F1", Third.AddMinutes(3)));

        var marked = fixtures.Stories.Current("F1")!;
        Assert.Null(marked.Detour);
        Assert.Equal(third, marked.Rewrite!.Chapter);
        Assert.Null(fixtures.Book.Store.Find("F1", detour.Key));

        Assert.Null(await fixtures.Director.Tick("F1", Third.AddMinutes(4))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Null(story.Rewrite);
        Assert.Equal(3, story.Chapters.Count);
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(third, fixtures.Asks[^1].SetAside!.Chapter.Previous.Key);
        Assert.Equal(["The Lantern Again"], fixtures.Asks[^1].SetAside!.Done);
        Assert.Null(fixtures.Asks[^1].SetAside!.Words);
    }

    [Fact]
    public async Task ARewriteSurvivesARestart()
    {
        var (fixtures, third) = await OnChapterThree();
        using var _ = fixtures;
        Assert.Null(fixtures.Director.RewriteChapter("F1", Third.AddMinutes(2)));
        var marker = fixtures.Stories.Current("F1")!.Rewrite!;

        using var reopened = new StoryFixtures(new RoundScriptedLlmProvider(Rounds(NextSpine, NextBeats, NothingObjected)), reopening: fixtures);
        reopened.Book.Store.Poll();

        var reloaded = reopened.Stories.Current("F1")!.Rewrite!;
        Assert.Equal((marker.Chapter, marker.At), (reloaded.Chapter, reloaded.At));
        Assert.Equal(marker.Done, reloaded.Done);
        Assert.True(reopened.Book.Store.Find("F1", third)!.IsSetAside);

        Assert.Null(await reopened.Director.Tick("F1", Third.AddMinutes(5))!);

        var story = reopened.Stories.Current("F1")!;
        Assert.Null(story.Rewrite);
        Assert.Equal(1, story.Rewrites);
        Assert.Equal(3, story.Chapters.Count);
        Assert.NotEqual(third, story.CurrentChapter);
        Assert.Equal(["The Lantern Again"], reopened.Asks[^1].SetAside!.Done);
    }

    [Fact]
    public async Task APausedStoryIsRefused()
    {
        var (fixtures, third) = await OnChapterThree();
        using var _ = fixtures;
        fixtures.Stories.Update("F1", Id, story => story.Paused(Third.AddMinutes(2)));

        Assert.Equal("Resume the story first.", fixtures.Director.RewriteChapter("F1", Third.AddMinutes(3)));
        Assert.True(fixtures.Book.Store.Find("F1", third)!.IsActive);
    }
}
