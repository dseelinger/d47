using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

public class PlotTheRedirectedHandInTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, params (string Key, object Value)[] fields)
    {
        var payload = new Dictionary<string, object> { ["timestamp"] = "2026-09-30T12:00:00Z", ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Accept(long id) =>
        Event("MissionAccepted", ("MissionID", id), ("Name", "Mission_Courier"), ("LocalisedName", $"Courier {id}"),
            ("DestinationSystem", "Alpha"), ("DestinationStation", "Old Base"), ("Reward", 1_000_000L),
            ("Expiry", "2026-10-30T12:00:00Z"));

    private static JournalEvent Redirect(long id, string station, string system) =>
        Event("MissionRedirected", ("MissionID", id), ("LocalisedName", $"Courier {id}"),
            ("NewDestinationStation", station), ("NewDestinationSystem", system));

    private static CommanderGameState Commander(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static List<Announcement> Run(MissionCallout callout, CommanderGameState state, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(Now, false, state, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void ARedirectSpeaksTheNewHandInAndOpensTheOffer()
    {
        var offer = new HandInOffer();
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), redirect);

        var said = Run(new MissionCallout { Offer = offer }, state, redirect);

        Assert.Equal(
            "That's Courier 1 done. Hand-in moved to Jameson Memorial in Sol. Say plot it to set the course.",
            Assert.Single(said).Text);
        Assert.Equal("Sol", offer.System);
    }

    [Fact]
    public void PlotItRoutesToPlotCourseWithTheNewSystemAsTheCommander()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var offer = new HandInOffer();
        offer.Open(1, "Sol");
        var router = new KeywordRouter(surface.Registry, offer.Phrases);

        foreach (var phrase in HandInOffer.EveryPhrase)
        {
            var match = router.MatchToolCommand(phrase);

            Assert.NotNull(match);
            Assert.Equal(NavigationCapability.Id, match.CapabilityId);
            Assert.Equal("plot_course", match.ToolName);
            Assert.True(match.Arguments.TryGetString("system", out var system));
            Assert.Equal("Sol", system);
        }
    }

    [Fact]
    public void WithNoRedirectStandingPlotItIsNotClaimed()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry, new HandInOffer().Phrases);

        Assert.Null(router.MatchToolCommand("plot it"));
    }

    [Fact]
    public void ACompletedMissionWithdrawsTheOffer()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), redirect);
        Run(callout, state, redirect);

        var completed = Event("MissionCompleted", ("MissionID", 1L));
        state.Apply(completed);
        Run(callout, state, completed);

        Assert.False(offer.IsStanding);
    }

    [Fact]
    public void ALaterRedirectReplacesTheOffer()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var first = Redirect(1, "Jameson Memorial", "Sol");
        var second = Redirect(2, "Ohm City", "Alpha Centauri");
        var state = Commander(Accept(1), Accept(2), first, second);

        Run(callout, state, first);
        Run(callout, state, second);

        Assert.Equal("Alpha Centauri", offer.System);
    }
}
