using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Logbook;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Commander fires the objective a story is waiting on as if the journal had shown it, except the beacon scan.</summary>
public sealed class TheCommanderCanMarkAnObjectiveDoneTests
{
    private static StoryFixtures Fixtures(params string[] answers) =>
        new(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()));

    /// <summary>Tells every moment owed, so the next beat can fire.</summary>
    private static void Settle(StoryFixtures fixtures, DateTimeOffset at)
    {
        foreach (var moment in fixtures.Book.Drain())
        {
            fixtures.Book.Told("F1", moment.Adventure.Key, new AdventureTold
            {
                Kind = AdventureToldKind.Beat,
                Text = "Told.",
                At = at,
                Beat = moment.Beat,
            });
        }
    }

    /// <summary>Chapter one picked and the Lantern reached, waiting on the dock at the Anchorage.</summary>
    private static async Task<(StoryFixtures Fixtures, string Key)> AtTheDock()
    {
        var fixtures = Fixtures(Spine, BeatsToTheBeacon);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));

        var key = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        Settle(fixtures, Now);
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        Settle(fixtures, Now.AddMinutes(1));

        Assert.Equal(TriggerKind.Dock, fixtures.Book.Standing("F1", key)!.CurrentBeat!.Trigger.Kind);
        return (fixtures, key);
    }

    private static Adventure ShortStory(string key, string name) => LanternRoute() with { Key = key, Name = name, StoryId = null };

    [Fact]
    public async Task MarkingADockBeatDoneFiresItAndItsLinesAreTold()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", Now.AddMinutes(5)));

        var standing = fixtures.Book.Standing("F1", key)!;
        Assert.Equal(2, standing.Current);
        Assert.Equal(AdventureFold.CommanderSource, standing.FiredBy[1]);
        Assert.Equal(Now.AddMinutes(5), standing.Fired[1]);

        var moment = Assert.Single(fixtures.Book.Drain());
        Assert.Equal(1, moment.Beat);
    }

    [Fact]
    public async Task AMarkedBeatStaysFiredAfterARestart()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", key, Now.AddMinutes(5)));
        Assert.Contains("\"by\": \"Commander\"", fixtures.Files.ReadText(fixtures.AdventuresPath)!, StringComparison.OrdinalIgnoreCase);

        var reopened = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, NullLogger<AdventureStore>.Instance);
        reopened.Poll();

        var restarted = new AdventureBook(reopened, NullLogger<AdventureBook>.Instance);
        var journal = Path.Combine(Path.GetDirectoryName(fixtures.AdventuresPath)!, "Journal.2026-09-30T120000.01.log");
        fixtures.Files.WriteText(
            journal,
            string.Join('\n', new[] { Commander("F1", Now.AddMinutes(-1)), Jump(Lantern, Now.AddMinutes(1)) }.Select(e => e.Raw.GetRawText())) + "\n");
        restarted.CatchUp([journal]);

        var standing = restarted.Standing("F1", key)!;
        Assert.Equal(2, standing.Current);
        Assert.Equal(["FSDJump", AdventureFold.CommanderSource], standing.FiredBy);
    }

    [Fact]
    public async Task MarkingAChaptersLastBeatDoneMovesTheStoryOn()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon, NothingObjected, NextSpine, NextBeats, NothingObjected, NextSpine, NextBeats, NothingObjected);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));

        fixtures.Finish("F1", Assert.Single(fixtures.Stories.Current("F1")!.Chapters), Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(1))!);
        Settle(fixtures, Now.AddDays(1));

        var second = fixtures.Stories.Current("F1")!.Chapters[1];

        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", second, Now.AddDays(1).AddMinutes(1)));
        Settle(fixtures, Now.AddDays(1).AddMinutes(1));
        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", second, Now.AddDays(1).AddMinutes(2)));
        Settle(fixtures, Now.AddDays(1).AddMinutes(2));

        Assert.True(fixtures.Book.Standing("F1", second)!.IsDone);

        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(2))!);
        Assert.Equal(3, fixtures.Stories.Current("F1")!.Chapters.Count);
    }

    [Fact]
    public async Task ABeaconScanIsRefusedWithTheReasonAndTheBenchCanStillStepIt()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        fixtures.Book.Observe(Docked(Anchorage, Now.AddMinutes(2)), "F1");
        Settle(fixtures, Now.AddMinutes(2));
        Assert.Equal(TriggerKind.Beacon, fixtures.Book.Standing("F1", key)!.CurrentBeat!.Trigger.Kind);

        const string Refusal = $"The beacon scan is the one objective the journal has to show. Scan the beacon in {Beacon}.";

        Assert.Equal(Refusal, fixtures.Director.MarkObjectiveDone("F1", Now.AddMinutes(5)));
        Assert.Equal(Refusal, fixtures.Director.MarkDoneRefusal("F1", key));
        Assert.Equal(2, fixtures.Book.Standing("F1", key)!.Current);
        Assert.Empty(fixtures.Book.Store.Find("F1", key)!.Stepped);

        Assert.Null(fixtures.Book.Step("F1", key, Now.AddMinutes(6)));
        Assert.True(fixtures.Book.Standing("F1", key)!.IsDone);
    }

    [Fact]
    public void ByVoiceWithTwoShortStoriesAndNoStoryRunningItRefusesAndNamesBoth()
    {
        using var fixtures = Fixtures();

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-quiet-field", "The Quiet Field")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));
        Assert.Null(fixtures.Book.Begin("F1", "the-quiet-field", Accepted));

        var refusal = fixtures.Director.MarkObjectiveDone("F1", Accepted.AddMinutes(5));

        Assert.NotNull(refusal);
        Assert.Contains("The Anchorage Run", refusal);
        Assert.Contains("The Quiet Field", refusal);
        Assert.Contains("Mark done", refusal);
        Assert.Equal(0, fixtures.Book.Standing("F1", "the-anchorage-run")!.Current);
        Assert.Equal(0, fixtures.Book.Standing("F1", "the-quiet-field")!.Current);
    }

    [Fact]
    public void ByVoiceWithOneShortStoryAndNoStoryRunningItMarksThatOne()
    {
        using var fixtures = Fixtures();

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));
        Settle(fixtures, Accepted);

        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", Accepted.AddMinutes(5)));
        Assert.Equal(1, fixtures.Book.Standing("F1", "the-anchorage-run")!.Current);
    }

    [Fact]
    public async Task ByVoiceWithTheStoryPausedItRefusesRatherThanMarkTheShortStory()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Now));
        Settle(fixtures, Now);
        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddMinutes(3)));

        Assert.StartsWith("Your story is paused.", fixtures.Director.MarkObjectiveDone("F1", Now.AddMinutes(5)));
        Assert.Equal("Resume the story first.", fixtures.Director.MarkDoneRefusal("F1", key));
        Assert.Equal(0, fixtures.Book.Standing("F1", "the-anchorage-run")!.Current);
        Assert.Equal(1, fixtures.Book.Standing("F1", key)!.Current);
    }

    [Fact]
    public async Task ByVoiceWithTheChapterFinishedItSaysTheStoryIsNotWaiting()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));

        fixtures.Finish("F1", Assert.Single(fixtures.Stories.Current("F1")!.Chapters), Now);

        Assert.Equal("The story is not waiting on an objective.", fixtures.Director.MarkObjectiveDone("F1", Now.AddMinutes(10)));
    }

    [Fact]
    public async Task AChapterIsNotMarkedWhileTheGameRunsWithoutOdyssey()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        fixtures.Director.Observe(OdysseySupport.LoadGame(Now.AddMinutes(2), odyssey: false), "F1");

        Assert.Equal(StoryDirector.NeedsOdyssey, fixtures.Director.MarkObjectiveDone("F1", key, Now.AddMinutes(5)));
        Assert.Equal(1, fixtures.Book.Standing("F1", key)!.Current);
    }

    [Fact]
    public async Task APageMarkIsRefusedWhenTheStoryHasMovedOnFromThatBeat()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        Assert.Equal("The story has moved on from that objective.", fixtures.Director.MarkObjectiveDone("F1", key, Now.AddMinutes(5), beat: 0));
        Assert.Equal(1, fixtures.Book.Standing("F1", key)!.Current);
    }

    [Fact]
    public async Task TheLogbookShowsAMarkedBeatAsTheCommandersAndABenchStepAsTheBench()
    {
        var (fixtures, key) = await AtTheDock();
        using var _ = fixtures;

        Assert.Null(fixtures.Director.MarkObjectiveDone("F1", key, Now.AddMinutes(5)));

        var marked = LogStoryBeat.From([fixtures.Book.Standing("F1", key)!], _ => null);
        Assert.Equal(["FSDJump", AdventureFold.CommanderSource], marked.Select(beat => beat.Source.Event));

        var stepped = AdventureFold.Stepped(AdventureFold.Start(LanternRoute(Accepted)), Accepted.AddMinutes(1));
        Assert.Equal(AdventureFold.BenchSource, Assert.Single(LogStoryBeat.From([stepped], _ => null)).Source.Event);
    }

    [Fact]
    public void ASavedStepWithNoSourceReadsAsABenchStep()
    {
        using var fixtures = Fixtures();

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));
        Settle(fixtures, Accepted);
        Assert.Null(fixtures.Book.Step("F1", "the-anchorage-run", Accepted.AddMinutes(5)));

        Assert.DoesNotContain("\"by\"", fixtures.Files.ReadText(fixtures.AdventuresPath)!, StringComparison.OrdinalIgnoreCase);

        var reopened = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, NullLogger<AdventureStore>.Instance);
        reopened.Poll();

        var restarted = new AdventureBook(reopened, NullLogger<AdventureBook>.Instance);
        restarted.CatchUp([]);

        Assert.Equal([AdventureFold.BenchSource], restarted.Standing("F1", "the-anchorage-run")!.FiredBy);
    }

    [Theory]
    [InlineData("mark this objective done")]
    [InlineData("mark the objective done")]
    [InlineData("this objective is done")]
    public void EachPhraseReachesTheTool(string said)
    {
        var router = new KeywordRouter(CapabilityRegistry.Build([AdventureCapability.Create()]));

        Assert.Equal(AdventureCapability.MarkDoneTool, router.MatchToolCommand(said)?.ToolName);
        Assert.Equal(AdventureCapability.RefuseBeatTool, router.MatchToolCommand("this objective is not for me")?.ToolName);
    }

    [Fact]
    public async Task TheModelCannotMarkAnObjectiveDoneAndTheCommanderCan()
    {
        var marked = 0;
        var registry = CapabilityRegistry.Build([AdventureCapability.Create(objectiveMark: new AdventureCapability.ObjectiveMark
        {
            Mark = () =>
            {
                marked++;
                return null;
            },
        })]);

        var model = await registry.InvokeAsync(
            AdventureCapability.MarkDoneTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(model.IsError);
        Assert.Equal(0, marked);

        var commander = await registry.InvokeAsync(
            AdventureCapability.MarkDoneTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(commander.IsError, commander.Content);
        Assert.Equal(1, marked);
    }
}
