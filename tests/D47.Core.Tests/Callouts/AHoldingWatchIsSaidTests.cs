using D47.Core.Callouts;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AHoldingWatchIsSaidTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static CalloutContext Context(bool priming = false) => new(Now, priming, null, D47.Core.Journal.GameStatus.Unknown, D47.Core.Journal.NavRoute.None, []);

    [Fact]
    public void EachReadyLineIsSaidUnderItsWatchsKey()
    {
        var first = new WatchNotice(Guid.NewGuid(), "Soontill Relics at Cheranovsky City, Ngurii: 240 in stock.");
        var second = new WatchNotice(Guid.NewGuid(), "Gold at Abraham Lincoln, Sol: 12,000 in stock.");
        var callout = new WatchCallout { Ready = () => [first, second] };

        var said = callout.Examine(Context()).ToList();

        Assert.Equal([$"watch.{first.WatchId}", $"watch.{second.WatchId}"], said.Select(line => line.Key));
        Assert.Equal([first.Text, second.Text], said.Select(line => line.Text));
        Assert.All(said, line => Assert.Equal(TimeSpan.Zero, line.Cooldown));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var callout = new WatchCallout { Ready = () => [new WatchNotice(Guid.NewGuid(), "Gold: 1 in stock.")] };

        Assert.Empty(callout.Examine(Context(priming: true)));
    }

    [Fact]
    public void TheEngineQueuesTheLine()
    {
        var notice = new WatchNotice(Guid.NewGuid(), "Gold: 1 in stock.");
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new WatchCallout { Ready = () => [notice] });

        engine.Tick(Context());

        Assert.Equal(notice.Text, Assert.Single(engine.Drain()).Text);
    }
}
