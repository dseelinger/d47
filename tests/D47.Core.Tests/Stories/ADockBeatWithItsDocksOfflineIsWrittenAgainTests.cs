using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Docking is denied at the current dock beat's station with its docks offline; d47 says so and writes the beat again.</summary>
public sealed class ADockBeatWithItsDocksOfflineIsWrittenAgainTests
{
    private const string ChapterKey = "the-anchorage";

    private static readonly DateTimeOffset DeniedAt = Now.AddMinutes(30);

    /// <summary>The moved objective's words, then the fidelity check's answer.</summary>
    private static readonly string[] DockAtTheLantern = [PlannedChapterWorld.NewWords(), PlannedChapterWorld.NoObjections];

    /// <summary>A story on chapter two, the Lantern reached, waiting to dock at Maren Anchorage.</summary>
    private static StoryFixtures AtTheAnchorage(List<string> said, params string[] answers)
    {
        var fixtures = StoryFixtures.ScriptedWith(answers);

        fixtures.SaveRunning(["chapter-one", ChapterKey]);

        Assert.Null(fixtures.Book.Write("F1", StoryFixtures.GeneratedChapter(ChapterKey, "The Anchorage", Id, DebtSpine,
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("The Anchorage", "catalyst", new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" }, "To one name."))));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Director.Clock = () => DeniedAt;
        fixtures.Director.Says += said.Add;
        return fixtures;
    }

    private static JournalEvent Denied(long marketId, string reason, DateTimeOffset at) => Event(
        $$"""{ "timestamp":"{{Stamp(at)}}", "event":"DockingDenied", "Reason":"{{reason}}", "MarketID":{{marketId}}, "StationName":"Maren Anchorage", "StationType":"OnFootSettlement" }""");

    [Trait("Category", "Integration")]
    [Fact]
    public async Task DocksOfflineAtTheBeatsStationReplacesTheBeatAndSaysSo()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        var work = fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1");

        Assert.NotNull(work);
        Assert.Null(await work);

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Equal(["The Lantern", "New One"], chapter.Beats.Select(beat => beat.Title));
        Assert.Equal(1001, chapter.Beats[1].Trigger.MarketId);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
        Assert.Equal(["The docks at Maren Anchorage are offline.", "The broker's ledger sends the Commander on to new part one. Next: dock at Lantern Dock in Ossen's Lantern."], said);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(1001L, "DockOffline")]
    [InlineData(Anchorage, "TooLarge")]
    [InlineData(Anchorage, "Hostile")]
    [InlineData(Anchorage, "NoSpace")]
    public void ADenialAtAnotherStationOrForAnotherReasonWritesNothing(long marketId, string reason)
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        Assert.Null(fixtures.Director.DockOffline(Denied(marketId, reason, DeniedAt), "F1"));
        Assert.Empty(said);
        Assert.Equal(0, fixtures.Provider.CallCount);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASecondDenialWhileTheBeatIsWrittenSaysNothingMore()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        var first = fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1");
        var second = fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt.AddSeconds(5)), "F1");

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Null(await first);
        Assert.Equal(3, fixtures.Provider.CallCount);
        Assert.Equal(2, said.Count);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ADenialFromBeforeTheStoryWasPickedWritesNothing()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        Assert.Null(fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", Now.AddDays(-31)), "F1"));
        Assert.Empty(said);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheClosedStationMovesToTheNearestOpenOneAndOnlyItsWordsAreWritten()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        Assert.Null(await fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1")!);

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Equal(["story_line_repairs", "story_fidelity", "story_plain"], fixtures.Provider.Requests.Select(request => request.ResponseSchema?.Name));
        Assert.Contains("Maren Anchorage, has its docks offline, so d47 moved that objective to an open station", prompt);
        Assert.Contains("1. catalyst: dock at Lantern Dock in Ossen's Lantern.", prompt);
        Assert.DoesNotContain("2. ", prompt[prompt.IndexOf("The new objectives", StringComparison.Ordinal)..]);
        Assert.Equal(1001, fixtures.Book.Store.Find("F1", ChapterKey)!.Beats[1].Trigger.MarketId);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task WithNoOpenStationFoundTheObjectivesArePlannedAgain()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);
        fixtures.Galaxy.Positioned = true;
        fixtures.Galaxy.WideStationsUnavailable = true;

        Assert.Null(await fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1")!);

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Contains("has its docks offline and no open station is within reach, so d47 built new objectives", fixtures.Provider.Requests[0].Prompt.History[0].Text);
        Assert.Equal("New One", chapter.Beats[1].Title);
        Assert.DoesNotContain(chapter.Beats.Skip(1), beat => beat.Trigger.MarketId == Anchorage);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AClosedDockThatIsOneStepOfSeveralMovesThatStepAloneWithNoModelCall()
    {
        var said = new List<string>();
        using var fixtures = StoryFixtures.ScriptedWith();

        fixtures.SaveRunning(["chapter-one", ChapterKey]);

        var bounty = new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 };
        var hollow = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Hollow, System = "Dyson's Hollow" };
        var dock = new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" };

        Assert.Null(fixtures.Book.Write("F1", StoryFixtures.GeneratedChapter(ChapterKey, "The Anchorage", Id, DebtSpine,
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            new AdventureBeat
            {
                Title = "The Anchorage",
                Function = "catalyst",
                Steps = [new BeatStep { Trigger = hollow }, new BeatStep { Trigger = dock }, new BeatStep { Trigger = bounty }],
                Lines = [new AdventureLine { Text = "To one name." }],
            })));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(Jump(Hollow, Now.AddMinutes(2)), "F1");
        fixtures.Director.Clock = () => DeniedAt;
        fixtures.Director.Says += said.Add;

        Assert.Equal(1, fixtures.Book.Standing("F1", ChapterKey)!.CurrentStep);
        Assert.Null(await fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1")!);

        var standing = fixtures.Book.Standing("F1", ChapterKey)!;

        Assert.Equal(0, fixtures.Provider.CallCount);
        Assert.Equal("The Anchorage", standing.CurrentBeat!.Title);
        Assert.Equal(1, standing.CurrentStep);
        Assert.Equal([TriggerKind.Arrive, TriggerKind.Dock, TriggerKind.Bounty], standing.CurrentBeat.Steps.Select(step => step.Trigger.Kind));
        Assert.Equal(1001, standing.CurrentBeat.Steps[1].Trigger.MarketId);
    }

    [Fact]
    public async Task TheResolverRefusesTheClosedStationAndAcceptsOthers()
    {
        var resolver = new AdventureResolver(new AdventureGeneratorTests.Galaxy()) { ClosedMarketId = Anchorage };

        var closed = await resolver.ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", "Maren Anchorage", null, "Beat 2", null, CancellationToken.None);
        var open = await resolver.ResolveAsync(TriggerKind.Dock, "Ossen's Lantern", "Lantern Dock", null, "Beat 2", null, CancellationToken.None);

        Assert.False(closed.Succeeded);
        Assert.Contains("offline", closed.Refusal);
        Assert.True(open.Succeeded);
    }
}
