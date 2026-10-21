using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class NewGalNetHeadlinesAreSaidTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    private static GalNetArticle[] Articles(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new GalNetArticle($"id{i}", Titles[i], ["Body."], Now.AddHours(-i), "3311"))
            .ToArray();

    private static readonly string[] Titles =
    [
        "Faulcon DeLacy Announces Anaconda Mk II", "Aegis Unveils Relic Scanner", "Salvation Returns to Pleiades",
        "Fourth Headline", "Fifth Headline",
    ];

    private static string[] Said(IReadOnlyList<GalNetArticle> arrived, bool priming = false) =>
        new GalNetCallout { Arrived = () => arrived }
            .Examine(new CalloutContext(Now, priming, null, GameStatus.Unknown, NavRoute.None, []))
            .Select(a => a.Text)
            .ToArray();

    [Fact]
    public void OneArrivalIsSaidAsItsHeadline() =>
        Assert.Equal(["GalNet: Faulcon DeLacy Announces Anaconda Mk II."], Said(Articles(1)));

    [Fact]
    public void ThreeArrivalsAreSaidNewestFirst() =>
        Assert.Equal(
            ["New on GalNet: Faulcon DeLacy Announces Anaconda Mk II; Aegis Unveils Relic Scanner; and Salvation Returns to Pleiades."],
            Said(Articles(3)));

    [Fact]
    public void TwoArrivalsAreSaidTogether() =>
        Assert.Equal(
            ["New on GalNet: Faulcon DeLacy Announces Anaconda Mk II; and Aegis Unveils Relic Scanner."],
            Said(Articles(2)));

    [Fact]
    public void FiveArrivalsAreACountAndThreeHeadlines() =>
        Assert.Equal(
            ["Five new GalNet articles. The latest: Faulcon DeLacy Announces Anaconda Mk II; Aegis Unveils Relic Scanner; and Salvation Returns to Pleiades."],
            Said(Articles(5)));

    [Fact]
    public void NoArrivalsAndPrimingSayNothing()
    {
        Assert.Empty(Said([]));
        Assert.Empty(Said(Articles(2), priming: true));
    }

    [Fact]
    public void ArrivalsWhileTheCalloutIsOffAreNotSavedForLater()
    {
        var callout = new GalNetCallout();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(callout);
        IReadOnlyList<GalNetArticle> arrived = Articles(1);
        callout.Arrived = () => arrived;
        var context = new CalloutContext(Now, false, null, GameStatus.Unknown, NavRoute.None, []);

        engine.SetEnabled("galnet", false);
        engine.Tick(context);
        Assert.Empty(engine.Drain());

        arrived = [];
        engine.SetEnabled("galnet", true);
        engine.Tick(context);
        Assert.Empty(engine.Drain());
    }

    [Fact]
    public void TheRowIsOnByDefault() => Assert.True(new CalloutSettings().GalNet);
}
