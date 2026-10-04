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

    private const string DockAtTheLantern = """
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "Another Dock", "function": "catalyst", "kind": "dock", "system": "Ossen's Lantern", "station": "Lantern Dock", "line": "Here instead."}
        ]}
        """;

    private const string DockAtTheAnchorage = """
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "Same Dock", "function": "catalyst", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Here again."}
        ]}
        """;

    /// <summary>A story on chapter two, the Lantern reached, waiting to dock at Maren Anchorage.</summary>
    private static StoryFixtures AtTheAnchorage(List<string> said, params string[] answers)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()));

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", ChapterKey],
        });

        Assert.Null(fixtures.Book.Write("F1", new Adventure
        {
            Key = ChapterKey,
            Name = "The Anchorage",
            Source = AdventureSource.Generated,
            Written = Now,
            Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed to you.", Ending = "It is forgiven." },
            Opening = "Go.",
            StoryId = Id,
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("The Anchorage", "catalyst", new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" }, "To one name."),
            ],
        }));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Director.Clock = () => DeniedAt;
        fixtures.Director.Says += said.Add;
        return fixtures;
    }

    private static JournalEvent Denied(long marketId, string reason, DateTimeOffset at) => Event(
        $$"""{ "timestamp":"{{Stamp(at)}}", "event":"DockingDenied", "Reason":"{{reason}}", "MarketID":{{marketId}}, "StationName":"Maren Anchorage", "StationType":"OnFootSettlement" }""");

    [Fact]
    public async Task DocksOfflineAtTheBeatsStationReplacesTheBeatAndSaysSo()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        var work = fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1");

        Assert.NotNull(work);
        Assert.Null(await work);

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Equal(["The Lantern", "Another Dock"], chapter.Beats.Select(beat => beat.Title));
        Assert.Equal(1001, chapter.Beats[1].Trigger.MarketId);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
        Assert.Equal(["The docks at Maren Anchorage are offline.", "Next: dock at Lantern Dock in Ossen's Lantern."], said);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

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
        Assert.Equal(1, fixtures.Provider.CallCount);
        Assert.Equal(2, said.Count);
    }

    [Fact]
    public void ADenialFromBeforeTheStoryWasPickedWritesNothing()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheLantern);

        Assert.Null(fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", Now.AddDays(-31)), "F1"));
        Assert.Empty(said);
    }

    [Fact]
    public async Task TheReplacementDoesNotNameTheClosedStation()
    {
        var said = new List<string>();
        using var fixtures = AtTheAnchorage(said, DockAtTheAnchorage, DockAtTheLantern);

        Assert.Null(await fixtures.Director.DockOffline(Denied(Anchorage, "DockOffline", DeniedAt), "F1")!);

        var first = fixtures.Provider.Requests[0].Prompt.History[0].Text;
        var second = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("Maren Anchorage's docks are offline", first);
        Assert.DoesNotContain("The beat the Commander refused", first);
        Assert.Contains("docks at Maren Anchorage, whose docks are offline", second);
        Assert.Equal(1001, fixtures.Book.Store.Find("F1", ChapterKey)!.Beats[1].Trigger.MarketId);
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
