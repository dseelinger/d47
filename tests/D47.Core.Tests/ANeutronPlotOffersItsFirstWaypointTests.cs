using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

public class ANeutronPlotOffersItsFirstWaypointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private sealed class Routes(string firstWaypoint) : StubRouteService
    {
        public override Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(new PlottedRoute(
                "Sol",
                "Colonia",
                22_000,
                2,
                [new RouteWaypoint(firstWaypoint, 1, 700, IsNeutron: true), new RouteWaypoint("Colonia", 1, 0, IsNeutron: false)]));
    }

    private static async Task<string> PlotAsync(NeutronOffer offer, string firstWaypoint = "Waypoint 0")
    {
        var install = new MemoryInstall();
        var settings = TestSurface.For(install).Settings;

        var plans = new RoutePlanBook(
            Path.Combine(install.Root, "data", "route-plans.json"), install.Files, NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            RouteCapability.Create(
                new Routes(firstWaypoint), null, () => null, settings, plans, () => Now, neutronOffer: offer),
        ]);

        var result = await registry.InvokeAsync(
            "plot_route",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["from"] = "Sol", ["to"] = "Colonia", ["jump_range"] = "60" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);

        return result.Content;
    }

    private static List<Announcement> Run(RouteCallout callout, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(Now, false, null, GameStatus.Unknown, NavRoute.None, events))];

    private static JournalEvent Event(string kind) => JournalScript.EventOf(kind, "2026-11-02T12:00:00Z");

    [Fact]
    public async Task APlotOpensTheOfferWithTheFirstWaypointAndEndsWithIt()
    {
        var offer = new NeutronOffer();

        var said = await PlotAsync(offer);

        Assert.Equal("Waypoint 0", offer.System);
        Assert.EndsWith("Say 'plot the first waypoint' to set it in the galaxy map.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondPlotReplacesTheOffer()
    {
        var offer = new NeutronOffer();

        await PlotAsync(offer, "Waypoint 0");
        await PlotAsync(offer, "Waypoint 9");

        Assert.Equal("Waypoint 9", offer.System);
    }

    [Theory]
    [InlineData("NavRouteClear")]
    [InlineData("NavRoute")]
    public void TheGamesOwnRouteWithdrawsTheOffer(string kind)
    {
        var offer = new NeutronOffer();
        offer.Open("Waypoint 0");

        Run(new RouteCallout { Offer = offer }, Event(kind));

        Assert.False(offer.IsStanding);
    }

    [Fact]
    public void AnUnrelatedEventLeavesTheOfferStanding()
    {
        var offer = new NeutronOffer();
        offer.Open("Waypoint 0");

        Run(new RouteCallout { Offer = offer }, Event("Music"));

        Assert.True(offer.IsStanding);
    }

    [Fact]
    public void WithNoOfferStandingThePhrasesAreAbsent()
    {
        Assert.Empty(new NeutronOffer().Phrases());

        var surface = TestSurface.For(new MemoryInstall());
        var router = new KeywordRouter(surface.Registry, new NeutronOffer().Phrases);

        foreach (var phrase in NeutronOffer.EveryPhrase)
        {
            Assert.NotEqual("plot_course", router.MatchToolCommand(phrase)?.ToolName);
        }
    }

    [Fact]
    public async Task AfterAPlotThePhraseReachesPlotCourseWithTheWaypointAndNoModelTurn()
    {
        var offer = new NeutronOffer();
        await PlotAsync(offer, "Waypoint 0");

        var surface = TestSurface.For(new MemoryInstall());
        var router = new KeywordRouter(surface.Registry, offer.Phrases);

        foreach (var phrase in NeutronOffer.EveryPhrase)
        {
            var match = router.MatchToolCommand(phrase);

            Assert.NotNull(match);
            Assert.Equal(NavigationCapability.Id, match.CapabilityId);
            Assert.Equal("plot_course", match.ToolName);
            Assert.True(match.Arguments.TryGetString("system", out var system));
            Assert.Equal("Waypoint 0", system);
        }
    }
}
