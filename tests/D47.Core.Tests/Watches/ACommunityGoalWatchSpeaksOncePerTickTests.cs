using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class ACommunityGoalWatchSpeaksOncePerTickTests
{
    private static readonly DateTimeOffset Tick = new(2026, 11, 3, 6, 40, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now = Tick.AddHours(2);

    private sealed class Rig
    {
        public required WatchRunner Runner { get; init; }

        public required WatchStore Store { get; init; }

        public IReadOnlyList<GalNetArticle> Arrived { get; set; } = [];

        public DateTimeOffset? LastTick { get; set; } = Tick;

        public bool GalNet { get; set; } = true;

        public bool BgsTick { get; set; } = true;

        public IReadOnlyList<WatchNotice> Poll(DateTimeOffset at, params GalNetArticle[] arrived)
        {
            Arrived = arrived;
            Runner.Poll(at);
            return Runner.Ready;
        }
    }

    private static Rig Build()
    {
        var store = new WatchStore(@"C:\d47\data\watches.json", new MemoryFileSystem(), NullLogger<WatchStore>.Instance);
        Assert.Null(store.Add(new Watch(Guid.NewGuid(), "New Community Goal", WatchKind.NewCommunityGoal)));
        Rig? rig = null;

        var runner = new WatchRunner(
            store,
            new StubTradePlanService(),
            () => rig!.LastTick,
            () => true,
            () => rig!.BgsTick,
            () => rig!.Arrived,
            () => rig!.GalNet,
            NullLogger<WatchRunner>.Instance);

        return rig = new Rig { Runner = runner, Store = store };
    }

    private static GalNetArticle Launch(string title) => new(
        title, title, ["Opening.", "Pilots are invited to sign up for the initiative."], Now, "3310");

    [Fact]
    public void AnArticleThatIsNotALaunchSaysNothing()
    {
        var rig = Build();

        Assert.Empty(rig.Poll(Now, new GalNetArticle("a", "Lady Zi Breaks Silence", ["Nothing to do."], Now, "3310")));
    }

    [Fact]
    public void OneLaunchIsSaidAsAGuessWithItsHeadline()
    {
        var rig = Build();

        var notice = Assert.Single(rig.Poll(Now, Launch("Wreaken Calls for Surface Mining Support Tests")));

        Assert.Equal("GalNet may have a new Community Goal: Wreaken Calls for Surface Mining Support Tests.", notice.Text);
    }

    [Fact]
    public void TwoLaunchesInOnePollAreOneLine()
    {
        var rig = Build();

        var notice = Assert.Single(rig.Poll(Now, Launch("Wreaken Calls for Tests"), Launch("Colonia Calls for Assistance")));

        Assert.Equal("GalNet may have new Community Goals: Wreaken Calls for Tests; and Colonia Calls for Assistance.", notice.Text);
    }

    [Fact]
    public void ALaunchAfterTheWatchFiredThisTickIsSaidAtTheNextTick()
    {
        var rig = Build();
        Assert.Single(rig.Poll(Now, Launch("Wreaken Calls for Tests")));

        Assert.Empty(rig.Poll(Now.AddHours(1), Launch("Colonia Calls for Assistance")));
        Assert.Empty(rig.Poll(Now.AddHours(2)));

        rig.LastTick = Tick.AddDays(1);
        var notice = Assert.Single(rig.Poll(Now.AddDays(1)));

        Assert.Equal("GalNet may have a new Community Goal: Colonia Calls for Assistance.", notice.Text);
    }

    [Fact]
    public void NothingHappensWhileGalNetNewsIsOff()
    {
        var rig = Build();
        rig.GalNet = false;

        Assert.Empty(rig.Poll(Now, Launch("Wreaken Calls for Tests")));

        rig.GalNet = true;
        Assert.Empty(rig.Poll(Now.AddHours(1)));
    }

    [Fact]
    public void NothingHappensWhileBgsTickTimeIsOff()
    {
        var rig = Build();
        rig.BgsTick = false;

        Assert.Empty(rig.Poll(Now, Launch("Wreaken Calls for Tests")));
        Assert.Null(rig.Store.Watches.Single().FiredForTick);
    }
}
