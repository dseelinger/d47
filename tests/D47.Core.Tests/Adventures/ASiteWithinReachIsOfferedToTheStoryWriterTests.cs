using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

/// <summary>The writer is offered the unvisited Guardian sites and tourist beacons within reach, and a site objective must name one of them.</summary>
public sealed class ASiteWithinReachIsOfferedToTheStoryWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const long FarReach = 2870300000002;

    private static readonly BodySummary GuardianBody = new()
    {
        Name = "Dyson's Hollow 3 b",
        SystemName = "Dyson's Hollow",
        SystemAddress = AdventureFixtures.Hollow,
        BodyId = 9,
        Distance = 12,
        IsLandable = true,
    };

    private static readonly string Tsv = string.Join(
        "\n",
        SignalIndex.Header,
        $"{AdventureFixtures.Lantern}\tOssen's Lantern\t10\t-40\t125\tTouristBeacon\tLantern Watch\t2026-10-10",
        $"{FarReach}\tFar Reach\t900\t0\t0\tTouristBeacon\tToo Far\t2026-10-10") + "\n";

    private const string GuardianBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Ruins", "function": "setup", "kind": "site", "reason": "The survey flagged a structure there.", "system": "Dyson's Hollow", "body": "Dyson's Hollow 3 b", "filter": "guardian", "line": "Stone older than the colonies."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private const string UnlistedBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Ruins", "function": "setup", "kind": "site", "reason": "The survey flagged a structure there.", "system": "Ossen's Lantern", "body": "Ossen's Lantern 2 a", "filter": "guardian", "line": "Stone older than the colonies."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    [Fact]
    public async Task BothPromptsListEachSiteAndTheSchemaOffersSite()
    {
        var provider = new RoundScriptedLlmProvider(Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats));

        await Generator(provider, bodies: [GuardianBody], index: Source(Tsv)).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        foreach (var request in provider.Requests.Take(2))
        {
            var instruction = request.Prompt.History[0].Text;

            Assert.Contains("Sites within reach", instruction);
            Assert.Contains("- Guardian site: Dyson's Hollow 3 b, in Dyson's Hollow (12 ly)", instruction);
            Assert.Contains("- Tourist beacon: Lantern Watch, in Ossen's Lantern (", instruction);
            Assert.DoesNotContain("Too Far", instruction);
        }

        Assert.Contains("\"site\"", provider.Requests[1].ResponseSchema!.SchemaJson);
        Assert.Contains("- \"site\":", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task WithNoSiteTheSectionAndTheKindAreLeftOutAndTheNotesSayWhy()
    {
        var provider = new RoundScriptedLlmProvider(Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats));

        var outcome = await Generator(provider, bodies: [], index: null).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.All(provider.Requests, request => Assert.DoesNotContain("Sites within reach", request.Prompt.History[0].Text));
        Assert.DoesNotContain("\"site\"", provider.Requests[1].ResponseSchema!.SchemaJson);
        Assert.Contains("The signal index is off, so no tourist beacon was offered.", outcome.Notes);
    }

    [Fact]
    public async Task ADraftNamingAListedGuardianSiteStandsOnThatBody()
    {
        var provider = new RoundScriptedLlmProvider(Saying(AdventureGeneratorTests.Spine), Saying(GuardianBeats));

        var outcome = await Generator(provider, bodies: [GuardianBody], index: null).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var trigger = outcome.Draft!.Beats[0].Trigger;
        Assert.Equal(TriggerKind.Site, trigger.Kind);
        Assert.Equal(AdventureTrigger.GuardianSite, trigger.Filter);
        Assert.Equal(AdventureFixtures.Hollow, trigger.SystemAddress);
        Assert.Equal(9, trigger.BodyId);
        Assert.Equal("Dyson's Hollow 3 b", trigger.Body);
        Assert.NotNull(outcome.Draft.Beats[0].Reason);
    }

    [Fact]
    public async Task ADraftNamingAListedTouristBeaconStandsOnItsSystemAndName()
    {
        const string beats = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Watch", "function": "setup", "kind": "site", "reason": "A beacon there has been logging visitors.", "system": "Ossen's Lantern", "body": null, "filter": "tourist", "line": "It logs every visitor."},
              {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
            ]}
            """;
        var provider = new RoundScriptedLlmProvider(Saying(AdventureGeneratorTests.Spine), Saying(beats));

        var outcome = await Generator(provider, bodies: [], index: Source(Tsv)).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var trigger = outcome.Draft!.Beats[0].Trigger;
        Assert.Equal(AdventureTrigger.TouristSite, trigger.Filter);
        Assert.Equal(AdventureFixtures.Lantern, trigger.SystemAddress);
        Assert.Equal("Lantern Watch", trigger.SiteName);
    }

    [Fact]
    public async Task ADraftNamingAnUnlistedSiteIsRefusedAndTheRepairNamesTheListedOnes()
    {
        var provider = new RoundScriptedLlmProvider(
            Saying(AdventureGeneratorTests.Spine),
            Saying(UnlistedBeats),
            Saying(Repairs.Replacing(GuardianBeats, 1)));

        var outcome = await Generator(provider, bodies: [GuardianBody], index: null).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var repair = provider.Requests[2].Prompt.History[0].Text;
        Assert.Contains("Objective 1 (The Ruins) is a \"site\" objective that is not one of the sites listed within reach.", repair);
        Assert.Contains("Guardian site Dyson's Hollow 3 b (\"filter\": \"guardian\", \"system\": \"Dyson's Hollow\", \"body\": \"Dyson's Hollow 3 b\")", repair);
        Assert.Equal(TriggerKind.Site, outcome.Draft!.Beats[0].Trigger.Kind);
    }

    [Fact]
    public async Task ASiteInAVisitedSystemIsNotOffered()
    {
        var provider = new RoundScriptedLlmProvider(Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats));
        const string root = "C:/d47-test/visits";
        var files = new MemoryFileSystem();
        files.WriteBytes(
            Path.Combine(root, "1", VisitedStarsCache.FileName),
            VisitedStarsCacheIsReadFromItsBytesTests.File(
                512,
                [
                    (AdventureFixtures.Hollow, 2, VisitedStarsCacheIsReadFromItsBytesTests.Second),
                    (AdventureFixtures.Lantern, 1, VisitedStarsCacheIsReadFromItsBytesTests.Fifth),
                ]));

        await Generator(provider, bodies: [GuardianBody], index: Source(Tsv), visited: new VisitedStarsBook(root, files))
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.All(provider.Requests, request => Assert.DoesNotContain("Sites within reach", request.Prompt.History[0].Text));
    }

    [Fact]
    public async Task WithTheIndexOffNothingIsRequestedFromIt()
    {
        var source = Source(Tsv);
        var provider = new RoundScriptedLlmProvider(Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats));

        await Generator(provider, bodies: [], index: source, indexOn: false).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.Equal(0, source.Requests);
    }

    [Fact]
    public void ADrawnChapterOffersSiteBesideItsSlots()
    {
        var kinds = StoryAnswer.Kinds([], beacon: false, reach: false, board: false, site: true, [TriggerKind.Bounty]);

        Assert.Contains("site", kinds);
        Assert.Contains("bounty", kinds);
        Assert.DoesNotContain("sell", kinds);
        Assert.DoesNotContain("site", StoryAnswer.Kinds([], beacon: false, reach: false, board: false, site: false, [TriggerKind.Bounty]));
    }

    private static AdventureGenerator Generator(
        ILlmProvider provider,
        IReadOnlyList<BodySummary> bodies,
        Index? index,
        bool indexOn = true,
        VisitedStarsBook? visited = null)
    {
        var state = AdventureGeneratorTests.State(1, []);
        var galaxy = new AdventureGeneratorTests.Galaxy { GuardianBodies = bodies };

        return new AdventureGenerator(
            () => provider,
            () => null,
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance,
            signalIndex: () => indexOn ? index : null,
            visited: visited);
    }

    private static Index Source(string tsv) => new(SignalIndex.Read(new StringReader(tsv)));

    private sealed class Index(SignalIndex index) : ISignalIndexSource
    {
        public int Requests { get; private set; }

        public Task<SignalIndex?> GetAsync(CancellationToken cancellationToken)
        {
            Requests++;

            return Task.FromResult<SignalIndex?>(index);
        }
    }
}
