using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Docking is denied with the docks offline at the current dock beat of an adventure outside a story; d47 writes that adventure's beat again.</summary>
[Trait("Category", "Integration")]
public sealed class ADockBeatOutsideAStoryIsWrittenAgainTests
{
    private const string AdventureKey = "the-anchorage-run";

    private const string ChapterKey = "the-lantern-chapter";

    private const long LanternDock = 1001;

    private static readonly DateTimeOffset DeniedAt = Now.AddMinutes(30);

    /// <summary>A short story's new objective words, as plain lines, then the fidelity check's answer.</summary>
    private static readonly string[] ShortWords = [PlannedChapterWorld.NewWords(speakers: false), PlannedChapterWorld.NoObjections];

    /// <summary>A story chapter's new objective words, then the fidelity check's answer.</summary>
    private static readonly string[] ChapterWords = [PlannedChapterWorld.NewWords(), PlannedChapterWorld.NoObjections];

    private static Adventure TwoBeats(string key, string? storyId, AdventureTrigger dock) => StoryFixtures.GeneratedChapter(key, key, storyId, DebtSpine,
        Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
        Beat("The Dock", "catalyst", dock, "To one name."));

    private static readonly AdventureTrigger AtTheAnchorage =
        new() { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" };

    private static readonly AdventureTrigger AtTheLanternDock =
        new() { Kind = TriggerKind.Dock, MarketId = LanternDock, System = "Ossen's Lantern", Station = "Lantern Dock" };

    /// <summary>An adventure outside a story, the Lantern reached, waiting to dock at Maren Anchorage.</summary>
    private static StoryFixtures Standalone(List<string> said, params string[] answers)
    {
        var fixtures = StoryFixtures.ScriptedWith(answers);

        Assert.Null(fixtures.Book.Write("F1", TwoBeats(AdventureKey, null, AtTheAnchorage)));
        Assert.Null(fixtures.Book.Begin("F1", AdventureKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Director.Clock = () => DeniedAt;
        fixtures.Director.Says += said.Add;
        return fixtures;
    }

    /// <summary>The standalone adventure above, and a running story whose chapter waits to dock at Lantern Dock.</summary>
    private static StoryFixtures BesideAStory(List<string> said, params string[] answers)
    {
        var fixtures = Standalone(said, answers);

        fixtures.SaveRunning(["chapter-one", ChapterKey]);

        Assert.Null(fixtures.Book.Write("F1", TwoBeats(ChapterKey, Id, AtTheLanternDock)));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(2)), "F1");
        return fixtures;
    }

    private static JournalEvent Denied(long marketId, DateTimeOffset at) => Event(
        $$"""{ "timestamp":"{{Stamp(at)}}", "event":"DockingDenied", "Reason":"DockOffline", "MarketID":{{marketId}}, "StationName":"Somewhere", "StationType":"Coriolis" }""");

    [Fact]
    public async Task DocksOfflineAtTheBeatsStationReplacesTheBeatAndKeepsTheBeatsDone()
    {
        var said = new List<string>();
        using var fixtures = Standalone(said, ShortWords);

        var work = fixtures.Director.DockOffline(Denied(Anchorage, DeniedAt), "F1");

        Assert.NotNull(work);
        Assert.Null(await work);

        var adventure = fixtures.Book.Store.Find("F1", AdventureKey)!;
        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Equal(["The Lantern", "New One"], adventure.Beats.Select(beat => beat.Title));
        Assert.Equal(Lantern, adventure.Beats[0].Trigger.SystemAddress);
        Assert.Equal(LanternDock, adventure.Beats[1].Trigger.MarketId);
        Assert.DoesNotContain(adventure.Beats, beat => beat.Trigger.Station == "Maren Anchorage");
        Assert.Contains("You are writing new objectives for a short story the Commander is flying", prompt);
        Assert.Contains("Maren Anchorage, has its docks offline, so d47 moved that objective to an open station", prompt);
        Assert.Equal(3, fixtures.Provider.CallCount);
        Assert.Equal(["The docks at Maren Anchorage are offline.", "The broker's ledger sends the Commander on to new part one. Next: dock at Lantern Dock in Ossen's Lantern."], said);
    }

    [Fact]
    public void ADenialAtAStationNoCurrentBeatNamesWritesNothing()
    {
        var said = new List<string>();
        using var fixtures = Standalone(said, ShortWords);

        Assert.Null(fixtures.Director.DockOffline(Denied(LanternDock, DeniedAt), "F1"));
        Assert.Empty(said);
        Assert.Equal(0, fixtures.Provider.CallCount);
    }

    [Fact]
    public void ADenialFromBeforeTheAdventureBeganWritesNothing()
    {
        var said = new List<string>();
        using var fixtures = Standalone(said, ShortWords);

        Assert.Null(fixtures.Director.DockOffline(Denied(Anchorage, Now.AddDays(-1)), "F1"));
        Assert.Empty(said);
    }

    [Fact]
    public async Task ASecondDenialWhileTheBeatIsWrittenWritesNothingMore()
    {
        var said = new List<string>();
        using var fixtures = Standalone(said, ShortWords);

        var first = fixtures.Director.DockOffline(Denied(Anchorage, DeniedAt), "F1");
        var second = fixtures.Director.DockOffline(Denied(Anchorage, DeniedAt.AddSeconds(5)), "F1");

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Null(await first);
        Assert.Equal(3, fixtures.Provider.CallCount);
    }

    [Fact]
    public async Task ADenialAtTheStoryBeatsStationRewritesOnlyTheStory()
    {
        var said = new List<string>();
        using var fixtures = BesideAStory(said, ChapterWords);

        Assert.Null(await fixtures.Director.DockOffline(Denied(LanternDock, DeniedAt), "F1")!);

        Assert.Equal("New One", fixtures.Book.Store.Find("F1", ChapterKey)!.Beats[1].Title);
        Assert.Equal(Anchorage, fixtures.Book.Store.Find("F1", ChapterKey)!.Beats[1].Trigger.MarketId);
        Assert.Equal("The Dock", fixtures.Book.Store.Find("F1", AdventureKey)!.Beats[1].Title);
        Assert.Equal(3, fixtures.Provider.CallCount);
    }

    [Fact]
    public async Task ADenialAtTheStandaloneBeatsStationRewritesOnlyTheStandaloneAdventure()
    {
        var said = new List<string>();
        using var fixtures = BesideAStory(said, ShortWords);

        Assert.Null(await fixtures.Director.DockOffline(Denied(Anchorage, DeniedAt), "F1")!);

        Assert.Equal("The Dock", fixtures.Book.Store.Find("F1", ChapterKey)!.Beats[1].Title);
        Assert.Equal("New One", fixtures.Book.Store.Find("F1", AdventureKey)!.Beats[1].Title);
        Assert.Equal(3, fixtures.Provider.CallCount);
    }
}
