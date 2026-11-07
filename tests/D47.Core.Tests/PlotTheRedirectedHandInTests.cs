using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests;

public class PlotTheRedirectedHandInTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields) =>
        JournalScript.EventOf(kind, "2026-09-30T12:00:00Z", fields);

    private static JournalEvent Accept(long id) =>
        Event("MissionAccepted", ("MissionID", id), ("Name", "Mission_Courier"), ("LocalisedName", $"Courier {id}"),
            ("DestinationSystem", "Alpha"), ("DestinationStation", "Old Base"), ("Reward", 1_000_000L),
            ("Expiry", "2026-10-30T12:00:00Z"));

    private static JournalEvent Redirect(long id, string station, string system) =>
        Event("MissionRedirected", ("MissionID", id), ("LocalisedName", $"Courier {id}"),
            ("NewDestinationStation", station), ("NewDestinationSystem", system));

    private static CommanderGameState Commander(params JournalEvent[] events) =>
        JournalScript.Commander("F1", "Fixture", events);

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
        var install = new MemoryInstall();
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
        var install = new MemoryInstall();
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

        Assert.Null(offer.System);
    }

    private static JournalEvent Disembark(string system) =>
        Event("Disembark", ("SRV", false), ("StarSystem", system), ("Body", "Denver"), ("OnPlanet", true));

    private static JournalEvent Embark(string system, bool srv = false) =>
        Event("Embark", ("SRV", srv), ("StarSystem", system), ("Body", "Denver"), ("OnPlanet", true));

    [Fact]
    public void ARedirectInTheCurrentSystemMakesNoOffer()
    {
        var offer = new HandInOffer();
        var arrived = Event("FSDJump", ("StarSystem", "Sol"));
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), arrived, redirect);

        var said = Run(new MissionCallout { Offer = offer }, state, redirect);

        Assert.Equal("That's Courier 1 done. Hand-in moved to Jameson Memorial in Sol.", Assert.Single(said).Text);
        Assert.Null(offer.System);
    }

    [Fact]
    public void ARedirectOnFootWaitsUntilTheCommanderBoards()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var disembark = Disembark("Anlave");
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), disembark, redirect);

        var said = Run(callout, state, disembark, redirect);

        Assert.Equal("That's Courier 1 done. Hand-in moved to Jameson Memorial in Sol.", Assert.Single(said).Text);
        Assert.Null(offer.System);

        var embark = Embark("Anlave");
        state.Apply(embark);
        var boarded = Run(callout, state, embark);

        var plot = Assert.Single(boarded);
        Assert.Equal("Hand-in for Courier 1 is in Sol. Say plot it to set the course.", plot.Text);
        Assert.Equal($"{MissionCallout.RedirectedKey}.1.plot", plot.Key);
        Assert.Equal("Sol", offer.System);
        Assert.Empty(Run(callout, state, embark));
    }

    [Fact]
    public void ARedirectInTheSrvWaitsPastBoardingTheSrv()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var disembark = Disembark("Anlave");
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), disembark);
        Run(callout, state, disembark);

        var srv = Embark("Anlave", srv: true);
        state.Apply(srv);
        state.Apply(redirect);

        Assert.Single(Run(callout, state, srv, redirect));
        Assert.Null(offer.System);

        var ship = Embark("Anlave");
        state.Apply(ship);

        Assert.Single(Run(callout, state, ship));
        Assert.Equal("Sol", offer.System);
    }

    [Fact]
    public void BoardingInTheHandInSystemMakesNoOffer()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var disembark = Disembark("Anlave");
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), disembark, redirect);
        Run(callout, state, disembark, redirect);

        var embark = Embark("Sol");
        state.Apply(embark);

        Assert.Empty(Run(callout, state, embark));
        Assert.Null(offer.System);
    }

    [Fact]
    public void BoardingAfterTheMissionLeftTheBoardMakesNoOffer()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var disembark = Disembark("Anlave");
        var redirect = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), disembark, redirect);
        Run(callout, state, disembark, redirect);

        var abandoned = Event("MissionAbandoned", ("MissionID", 1L));
        var embark = Embark("Anlave");
        state.Apply(abandoned);
        state.Apply(embark);

        Assert.Empty(Run(callout, state, abandoned, embark));
        Assert.Null(offer.System);
    }

    [Fact]
    public void ARedirectAfterBoardingTakesOverTheOffer()
    {
        var offer = new HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var disembark = Disembark("Anlave");
        var first = Redirect(1, "Jameson Memorial", "Sol");
        var state = Commander(Accept(1), Accept(2), disembark, first);
        Run(callout, state, disembark, first);

        var embark = Embark("Anlave");
        var second = Redirect(2, "Ohm City", "Alpha Centauri");
        state.Apply(embark);
        state.Apply(second);

        var said = Run(callout, state, embark, second);

        Assert.Equal("Hand-in for Courier 1 is in Sol. Say plot it to set the course.", said[0].Text);
        Assert.Equal(
            "That's Courier 2 done. Hand-in moved to Ohm City in Alpha Centauri. Say plot it to set the course.",
            said[1].Text);
        Assert.Equal("Alpha Centauri", offer.System);
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
