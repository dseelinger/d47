using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class GalNetWaitsForTheOpeningLinesTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    private static readonly GalNetArticle Article = new("id0", "Aegis Unveils Relic Scanner", ["Body."], Start, "3311");

    private sealed class Rig
    {
        public CalloutEngine Engine { get; } = new(NullLogger<CalloutEngine>.Instance);

        public ContinuityCallout Continuity { get; } = new();

        public RecapCallout Recap { get; } = new() { Prepare = null };

        public GalNetCallout GalNet { get; } = new();

        public IReadOnlyList<GalNetArticle> Arrived { get; set; } = [];

        public Rig()
        {
            Engine.Add(Continuity).Add(Recap).Add(GalNet);
            GalNet.Arrived = () => Arrived;
            GalNet.OpeningDone = () =>
                (!Engine.IsEnabled("continuity") || Continuity.Opened) && (!Engine.IsEnabled("recap") || Recap.Done);
            Recap.Supply("Last session you docked in Sol.");
        }

        public string[] TickAt(int seconds, bool arriving = false)
        {
            Arrived = arriving ? [Article] : [];
            Engine.Tick(new CalloutContext(Start.AddSeconds(seconds), false, null, GameStatus.Unknown, NavRoute.None, []));
            return Engine.Drain().Select(a => a.Key).ToArray();
        }
    }

    [Fact]
    public void HeadlinesBeforeTheGreetingAreSaidAfterTheRecapOnItsTick()
    {
        var rig = new Rig();

        Assert.Empty(rig.TickAt(0, arriving: true));
        Assert.Empty(rig.TickAt(7));
        Assert.Equal([ContinuityCallout.Key], rig.TickAt(9));
        Assert.Empty(rig.TickAt(11));
        Assert.Equal([RecapCallout.Key, GalNetCallout.Key], rig.TickAt(12));
    }

    [Fact]
    public void HeadlinesAreSaidWhenTheRecapIsSkipped()
    {
        var rig = new Rig();
        rig.Recap.Supply(null);

        rig.TickAt(0, arriving: true);

        Assert.Equal([ContinuityCallout.Key], rig.TickAt(9));
        Assert.Equal([GalNetCallout.Key], rig.TickAt(12));
    }

    [Fact]
    public void WithTheRecapOffHeadlinesFollowTheGreeting()
    {
        var rig = new Rig();
        rig.Engine.SetEnabled("recap", false);

        rig.TickAt(0, arriving: true);

        Assert.Equal([ContinuityCallout.Key, GalNetCallout.Key], rig.TickAt(9));
    }

    [Fact]
    public void WithTheGreetingAndRecapOffHeadlinesAreNotHeld()
    {
        var rig = new Rig();
        rig.Engine.SetEnabled("recap", false);
        rig.Engine.SetEnabled("continuity", false);

        Assert.Equal([GalNetCallout.Key], rig.TickAt(0, arriving: true));
    }

    [Fact]
    public void ALoginByAnotherCommanderDoesNotHoldHeadlinesAgain()
    {
        var rig = new Rig();
        rig.TickAt(0);
        rig.TickAt(9);
        rig.TickAt(12);

        rig.Continuity.Rearm();

        Assert.Equal([GalNetCallout.Key], rig.TickAt(13, arriving: true));
    }
}
