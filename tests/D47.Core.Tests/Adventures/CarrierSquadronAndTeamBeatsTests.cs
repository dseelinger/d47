using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class CarrierSquadronAndTeamBeatsTests
{
    private const string Spine = """
        {"name": "The Long Way", "premise": "A debt.", "want": "To pay it.", "stake": "Whether it can be.", "turn": "It is owed.", "ending": "Forgiven."}
        """;

    private const string Startup = """{ "timestamp":"2026-08-22T11:00:40Z", "event":"SquadronStartup", "SquadronName":"S", "CurrentRank":1 }""";

    private const string Buy = """{ "timestamp":"2026-08-22T11:00:50Z", "event":"CarrierBuy", "CarrierType":"FleetCarrier", "CarrierID":5, "Callsign":"AAA-111" }""";

    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static AdventureStanding Fold(AdventureTrigger beat, params string[] events)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", ToLantern, "Scoop here."),
                Beat("The Beat", "catalyst", beat, "Done."),
            ],
        };

        var standing = AdventureFold.Apply(
            AdventureFold.Start(adventure),
            Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""));

        return events.Select((json, index) => Event(json.Replace("@", Stamp(Accepted.AddMinutes(2 + index)), StringComparison.Ordinal)))
            .Aggregate(standing, AdventureFold.Apply);
    }

    private static string Fleet(long system) =>
        $$"""{ "timestamp":"@", "event":"CarrierLocation", "CarrierType":"FleetCarrier", "CarrierID":11, "StarSystem":"X", "SystemAddress":{{system}}, "BodyID":10 }""";

    private static string Squadron(long system) =>
        $$"""{ "timestamp":"@", "event":"CarrierLocation", "CarrierType":"SquadronCarrier", "CarrierID":22, "StarSystem":"Y", "SystemAddress":{{system}}, "BodyID":10 }""";

    private static AdventureTrigger Of(TriggerKind kind, int count = 1) => new() { Kind = kind, Count = count };

    [Fact]
    public void ACarrierJumpBeatFiresOnTheSecondChangeOfSystemForTheFleetCarrier()
    {
        var trigger = Of(TriggerKind.CarrierJump, 2);

        Assert.Equal(0, Fold(trigger, Fleet(100)).Counted);
        Assert.Equal(0, Fold(trigger, Fleet(100), Fleet(100)).Counted);
        Assert.Equal(1, Fold(trigger, Fleet(100), Fleet(200)).Counted);
        Assert.True(Fold(trigger, Fleet(100), Fleet(200), Fleet(300)).IsDone);
    }

    [Fact]
    public void ACarrierJumpBeatIgnoresTheSquadronCarrier()
    {
        var trigger = Of(TriggerKind.CarrierJump, 1);

        Assert.Equal(1, Fold(trigger, Squadron(100), Squadron(200), Fleet(300), Squadron(400)).Current);
    }

    [Theory]
    [InlineData(TriggerKind.Wing, "WingJoin", "\"Others\":[\"A\"]")]
    [InlineData(TriggerKind.Wing, "WingAdd", "\"Name\":\"A\"")]
    [InlineData(TriggerKind.Multicrew, "JoinACrew", "\"Captain\":\"A\"")]
    [InlineData(TriggerKind.CarrierBuy, "CarrierBuy", "\"CarrierType\":\"FleetCarrier\",\"CarrierID\":5")]
    [InlineData(TriggerKind.Squadron, "JoinedSquadron", "\"SquadronName\":\"S\"")]
    [InlineData(TriggerKind.SquadronFound, "SquadronCreated", "\"SquadronName\":\"S\"")]
    public void EachTeamBeatFiresOnItsOwnEvent(TriggerKind kind, string journalEvent, string fields)
    {
        var trigger = new AdventureTrigger { Kind = kind, Count = AdventureTrigger.IsOnceKind(kind) ? null : 1 };

        Assert.True(Fold(trigger, $$"""{ "timestamp":"@", "event":"{{journalEvent}}", {{fields}} }""").IsDone);
        Assert.False(Fold(trigger, """{ "timestamp":"@", "event":"Music", "MusicTrack":"x" }""").IsDone);
    }

    [Fact]
    public void ACarrierBuyBeatIgnoresASquadronCarrier()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.CarrierBuy };

        Assert.False(Fold(trigger, """{ "timestamp":"@", "event":"CarrierBuy", "CarrierType":"SquadronCarrier", "CarrierID":5 }""").IsDone);
    }

    [Fact]
    public void ThePlainFormsAreWordsTheFileUses()
    {
        foreach (var word in new[] { "carrierbuy", "carrierjump", "wing", "multicrew", "squadron", "squadronfound" })
        {
            Assert.Contains(word, AdventureValidation.Kinds);
            Assert.True(AdventureValidation.TryKind(word, out _));
        }
    }

    [Theory]
    [InlineData(TriggerKind.CarrierBuy, 10_472_082_550, false, false, true)]
    [InlineData(TriggerKind.CarrierBuy, 6_000_000_000, false, false, false)]
    [InlineData(TriggerKind.CarrierBuy, 10_472_082_550, true, false, false)]
    [InlineData(TriggerKind.CarrierJump, 0, true, false, true)]
    [InlineData(TriggerKind.CarrierJump, 10_472_082_550, false, false, false)]
    [InlineData(TriggerKind.Wing, 0, false, true, true)]
    [InlineData(TriggerKind.Multicrew, 0, false, true, true)]
    [InlineData(TriggerKind.Squadron, 0, false, false, true)]
    [InlineData(TriggerKind.Squadron, 0, false, true, false)]
    [InlineData(TriggerKind.CarrierBuy, 5_600_000_000, false, false, false)]
    [InlineData(TriggerKind.SquadronFound, 20_000_000, false, false, true)]
    [InlineData(TriggerKind.SquadronFound, 10_000_000, false, false, false)]
    [InlineData(TriggerKind.SquadronFound, 10_000_000, false, true, false)]
    public void AKindIsAllowedOnlyWhereTheCommanderCanDoIt(TriggerKind kind, long credits, bool owns, bool inSquadron, bool allowed)
    {
        Assert.Equal(allowed, TeamBeats.Why(kind, owns, inSquadron, credits) is null);
    }

    [Fact]
    public void SquadronMembershipBeginsAtAStartupAndEndsAtALeaveOrTheNextLoad()
    {
        var state = SquadronState.None;

        SquadronState Next(string json) => state = state.Apply(Event(json));

        Assert.False(state.IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:00:00Z", "event":"SquadronStartup", "SquadronName":"GREYBEARD DELTA", "CurrentRank":3 }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:01:00Z", "event":"LeftSquadron", "SquadronName":"GREYBEARD DELTA" }""").IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:02:00Z", "event":"JoinedSquadron", "SquadronName":"X" }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:03:00Z", "event":"LoadGame" }""").IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:04:00Z", "event":"SquadronCreated", "SquadronName":"X" }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:05:00Z", "event":"DisbandedSquadron", "SquadronName":"X" }""").IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:06:00Z", "event":"JoinedSquadron", "SquadronName":"X" }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:07:00Z", "event":"KickedFromSquadron", "SquadronName":"X" }""").IsMember);
    }

    private static string Beats(string kind) => $$"""
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Job", "function": "finale", "kind": "{{kind}}", "count": 1, "line": "Do it."}
        ]}
        """;

    private static string LoadGame(long credits) =>
        $$"""{ "timestamp":"2026-08-22T11:00:30Z", "event":"LoadGame", "Credits":{{credits}} }""";

    private static async Task<(AdventureOutcome Outcome, string Brief)> Written(string kind, params string[] events)
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(Beats(kind)),
            RoundScriptedLlmProvider.Saying(Beats(kind)));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), 1, events)
            .GenerateAsync(
                new AdventureAsk(Story: new AdventureStory("s", "S", "public", "hidden", 1, 10, null)),
                Accepted,
                TestContext.Current.CancellationToken);

        return (outcome, provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ACarrierBuyChapterIsWrittenWithTheCreditsAndRefusedWithout()
    {
        var rich = await Written("carrierbuy", LoadGame(10_472_082_550));
        var poor = await Written("carrierbuy", LoadGame(6_000_000_000));

        Assert.NotNull(rich.Outcome.Draft);
        Assert.Contains("\"carrierbuy\"", rich.Brief);
        Assert.Null(poor.Outcome.Draft);
        Assert.Contains("fewer than 7,000,000,000 credits", poor.Outcome.Refusal);
        Assert.DoesNotContain("\"carrierbuy\"", poor.Brief);
    }

    [Fact]
    public async Task ACarrierBuyChapterIsRefusedOnceACarrierIsOwned()
    {
        var owner = await Written("carrierbuy", LoadGame(10_472_082_550), Buy);

        Assert.Null(owner.Outcome.Draft);
        Assert.Contains("already owns a fleet carrier", owner.Outcome.Refusal);
    }

    [Fact]
    public async Task ASquadronChapterIsRefusedAfterASquadronStartup()
    {
        var member = await Written("squadron", LoadGame(1_000), Startup);
        var found = await Written("squadronfound", LoadGame(50_000_000), Startup);
        var free = await Written("squadron", LoadGame(1_000));

        Assert.Null(member.Outcome.Draft);
        Assert.Null(found.Outcome.Draft);
        Assert.Contains("already in a squadron", found.Outcome.Refusal);
        Assert.NotNull(free.Outcome.Draft);
    }

    [Fact]
    public async Task AFoundingChapterNeedsTwentyMillionCredits()
    {
        var poor = await Written("squadronfound", LoadGame(19_999_999));
        var able = await Written("squadronfound", LoadGame(20_000_000));

        Assert.Null(poor.Outcome.Draft);
        Assert.NotNull(able.Outcome.Draft);
    }

    [Fact]
    public async Task TheWriterIsToldTheCommanderCanRefuseAWingBeat()
    {
        var wing = await Written("wing", LoadGame(1_000));

        Assert.NotNull(wing.Outcome.Draft);
        Assert.Contains("the Commander can refuse it", wing.Brief);
        Assert.DoesNotContain("\"carrierjump\"", wing.Brief);
    }
}
