using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A notable thing the Commander does while a stock story runs queues one cast reaction, said at the next dock and at
/// most once a play session. The marker carries no event text, and the model is told the event's recorded facts only.
/// </summary>
public sealed class ACastMemberReactsOnceASessionTests
{
    private const string Story = "the-test-story";

    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Flying = GameStatus.Unknown with { Flags = StatusFlags.InMainShip, ReadAt = Now };

    private static readonly GameStatus Docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip, ReadAt = Now };

    private static readonly IReadOnlyList<StoryReactor> Cast =
    [
        new("dock-hand", "Ren", "A tired dock hand, short words."),
        new("cray", "Ellie", "A test engineer, brief and warm."),
    ];

    private static JournalEvent Interdicted(DateTimeOffset at) => Event(
        $$"""{ "timestamp":"{{Stamp(at)}}", "event":"Interdicted", "Submitted":false, "Interdictor":"Dedy Sofyan", "IsPlayer":false, "Faction":"The Brothers" }""");

    private static JournalEvent Promoted(DateTimeOffset at) => Event(
        $$"""{ "timestamp":"{{Stamp(at)}}", "event":"Promotion", "Combat":3 }""");

    private static StoryReactionWindow Window(int session = 1, bool reacted = false) => new("F1", Story, session, reacted);

    private static CalloutContext Tick(DateTimeOffset now, GameStatus status, params JournalEvent[] events) =>
        new(now, false, null, status, NavRoute.None, events, null);

    [Fact]
    public void ANotableEventIsReactedToAtTheNextDockAndNotBefore()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window() };

        Assert.Empty(callout.Examine(Tick(Now, Flying, Interdicted(Now))));
        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(5), Flying)));

        var marker = Assert.Single(callout.Examine(Tick(Now.AddMinutes(10), Docked)));

        Assert.Equal(VoiceRole.Crew, marker.Voice);
        Assert.Equal((new StoryReactionDue(Story, 1), (string?)null), StoryReactionCallout.Parse(marker.Key));
    }

    [Fact]
    public void TheMarkerCarriesNoEventText()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window() };

        callout.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();
        var marker = Assert.Single(callout.Examine(Tick(Now.AddMinutes(10), Docked)));

        Assert.Equal(string.Empty, marker.Text);
        Assert.DoesNotContain("Brothers", marker.Key);
        Assert.DoesNotContain("Interdict", marker.Key);
    }

    [Fact]
    public void ASecondNotableEventInTheSameSessionQueuesNothing()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window() };

        callout.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();
        Assert.Single(callout.Examine(Tick(Now.AddMinutes(10), Docked)));

        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(20), Docked, Promoted(Now.AddMinutes(20)))));
        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(40), Docked)));
    }

    [Fact]
    public void AReactionNotSpokenIsTriedAgainNextSession()
    {
        var session = 1;
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window(session) };

        callout.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();
        Assert.Single(callout.Examine(Tick(Now.AddMinutes(10), Docked)));

        session = 2;

        var again = Assert.Single(callout.Examine(Tick(Now.AddHours(5), Docked)));
        Assert.Equal((new StoryReactionDue(Story, 2), (string?)null), StoryReactionCallout.Parse(again.Key));
    }

    [Fact]
    public void ASessionThatHasReactedQueuesNothing()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window(reacted: true) };

        callout.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();

        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(10), Docked)));
    }

    [Fact]
    public void TheNewestEventSinceTheLastReactionIsTheOneReactedTo()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window() };

        callout.Examine(Tick(Now, Flying, Interdicted(Now), Promoted(Now.AddMinutes(1)))).ToList();

        Assert.Equal(NotableKind.Promotion, callout.Newest("F1", new StoryReactionDue(Story, 1))?.Kind);

        callout.Reacted("F1", new StoryReactionDue(Story, 1));

        Assert.Null(callout.Newest("F1", new StoryReactionDue(Story, 1)));
    }

    [Fact]
    public void AnEventFoldedDuringTheStartupCatchUpGetsNoReaction()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window() };

        callout.Examine(Tick(Now, Flying, Interdicted(Now)) with { IsPriming = true }).ToList();

        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(10), Docked)));
    }

    [Fact]
    public void NothingIsNotedWhileTheStoryTakesNoReactions()
    {
        StoryReactionWindow? window = null;
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => window };

        callout.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();
        window = Window();

        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(10), Docked)));
    }

    [Fact]
    public void AReactionWaitsForTwoMinutesOfQuiet()
    {
        var callout = new StoryReactionCallout(new NearbyFight()) { Window = () => Window() };

        callout.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();

        Assert.Empty(callout.Examine(Tick(Now.AddMinutes(10), Docked) with { LastChatter = new ChatterSaid(Now.AddMinutes(9), TimeSpan.FromMinutes(2)) }));
        Assert.Single(callout.Examine(Tick(Now.AddMinutes(12), Docked) with { LastChatter = new ChatterSaid(Now.AddMinutes(9), TimeSpan.FromMinutes(2)) }));
    }

    [Fact]
    public void AClueQueuedOnTheSameTickGoesFirst()
    {
        var fight = new NearbyFight();
        var clue = new StoryClueCallout(fight) { Due = _ => new StoryClueDue(Story, 0) };
        var reaction = new StoryReactionCallout(fight) { Window = () => Window() };
        var callouts = new CalloutEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<CalloutEngine>.Instance).Add(clue).Add(reaction);

        reaction.Examine(Tick(Now, Flying, Interdicted(Now))).ToList();
        callouts.Tick(Tick(Now.AddMinutes(10), Docked));

        Assert.Equal(StoryClueCallout.Key(new StoryClueDue(Story, 0)), Assert.Single(callouts.Drain()).Key);

        callouts.Tick(Tick(Now.AddMinutes(12), Docked));

        Assert.Equal(StoryReactionCallout.Key(new StoryReactionDue(Story, 1)), Assert.Single(callouts.Drain()).Key);
    }

    [Fact]
    public void TheModelIsToldTheRecordedFactsAndNoName()
    {
        var events = new[]
        {
            Event($$"""{ "timestamp":"{{Stamp(Now)}}", "event":"Died", "KillerName":"CMDR Hidden Killer", "KillerShip":"federation_gunship", "KillerRank":"Dangerous" }"""),
            Event($$"""{ "timestamp":"{{Stamp(Now)}}", "event":"Interdicted", "Submitted":true, "Interdictor":"Hidden Interdictor", "IsPlayer":false, "Faction":"The Brothers" }"""),
            Event($$"""{ "timestamp":"{{Stamp(Now)}}", "event":"Bounty", "PilotName":"$npc_name_decorate;", "PilotName_Localised":"Hidden Pilot", "Target":"sidewinder", "TotalReward":9000 }"""),
        };

        foreach (var journalEvent in events)
        {
            var notable = ChapterNotables.Of(journalEvent)!;
            var brief = StoryReactions.Asking(notable, Cast);
            var prompt = $"{brief.Speaker}\n{brief.Instruction}";

            Assert.Contains(notable.Text, prompt);
            Assert.Contains("Ren. A tired dock hand, short words.", prompt);
            Assert.Contains("cray: Ellie", prompt);
            Assert.DoesNotContain("Hidden Killer", prompt);
            Assert.DoesNotContain("Hidden Interdictor", prompt);
            Assert.DoesNotContain("Hidden Pilot", prompt);
            Assert.DoesNotContain("npc_name", prompt);
            Assert.False(brief.NeedsGameState);
        }
    }

    [Theory]
    [InlineData("dock-hand: Heard they pulled you out of the black.", "dock-hand", "Heard they pulled you out of the black.")]
    [InlineData("**Cray**: \"Still in one piece, then.\"", "cray", "Still in one piece, then.")]
    [InlineData("Ren: Back again.", "dock-hand", "Back again.")]
    [InlineData("[sighs] ren: Heard you got pulled out.", "dock-hand", "[sighs] Heard you got pulled out.")]
    [InlineData("Ren (dock hand): Back again.", "dock-hand", "Back again.")]
    [InlineData("[cray]: Still flying.", "cray", "Still flying.")]
    public void AReplyNamingAPrimaryMemberIsTheirLine(string reply, string castId, string line) =>
        Assert.Equal((castId, line), StoryReactions.Parse(reply, Cast));

    [Theory]
    [InlineData("narrator: The black is quiet.")]
    [InlineData("ship: Hull holding.")]
    [InlineData("Heard they pulled you out of the black.")]
    [InlineData("dock-hand:   ")]
    [InlineData(null)]
    public void AReplyNamingAnyoneElseIsNotSpoken(string? reply) =>
        Assert.Null(StoryReactions.Parse(reply, Cast));

    [Fact]
    public void ASpokenReactionKeyNamesTheMemberAndTheStory()
    {
        var due = new StoryReactionDue(Story, 4);

        Assert.Equal((due, "dock-hand"), StoryReactionCallout.Parse(StoryReactionCallout.Key(due, "dock-hand")));
        Assert.Equal((due, "d.hand"), StoryReactionCallout.Parse(StoryReactionCallout.Key(due, "d.hand")));
        Assert.Equal((due, "3"), StoryReactionCallout.Parse(StoryReactionCallout.Key(due, "3")));
        Assert.Equal((due, (string?)null), StoryReactionCallout.Parse(StoryReactionCallout.Key(due)));
        Assert.Null(StoryReactionCallout.Parse(StoryClueCallout.Key(new StoryClueDue(Story, 0))));
    }
}
