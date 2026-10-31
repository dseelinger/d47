using D47.App.Panel;
using D47.App.Voice;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each Commander's comms selection is saved in view state and taken up again at launch and on a switch (#1996).</summary>
public sealed class TheCommsSelectionIsKeptAcrossRestartsTests
{
    private static readonly string[] Login =
    [
        """{"timestamp":"2026-10-31T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-31T09:00:01Z","event":"Loadout","Ship":"anaconda","Modules":[],"ShipName":"Sacred Wings","ShipIdent":"SW-01"}""",
    ];

    private const string Carrier =
        """{"timestamp":"2026-10-31T09:01:00Z","event":"CarrierStats","CarrierID":3700000001,"Callsign":"BNH-T2F","Name":"GREYBEARD DELTA"}""";

    private const string Hired =
        """{"timestamp":"2026-10-31T09:02:00Z","event":"CrewHire","Name":"Samira Voss","CrewID":1,"CombatRank":6}""";

    private const string Active =
        """{"timestamp":"2026-10-31T09:02:01Z","event":"CrewAssign","Name":"Samira Voss","CrewID":1,"Role":"Active"}""";

    private const string Target =
        """{"timestamp":"2026-10-31T09:03:00Z","event":"ShipTargeted","TargetLocked":true,"Ship":"cobramkiii","Ship_Localised":"Cobra Mk III","ScanStage":3,"PilotName":"$npc_name_decorate:#name=Mara Okafor;","PilotName_Localised":"Mara Okafor"}""";

    private const string SecondCommander =
        """{"timestamp":"2026-10-31T10:00:00Z","event":"Commander","FID":"F2","Name":"Kestrel"}""";

    private const string SecondCarrier =
        """{"timestamp":"2026-10-31T10:00:01Z","event":"CarrierStats","CarrierID":3700000002,"Callsign":"XQZ-41K","Name":"LONG WAKE"}""";

    private const string FirstCommanderAgain =
        """{"timestamp":"2026-10-31T11:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static ViewStateStore Store(MemoryFileSystem files) =>
        new(new AppPaths("/comms-selection-tests"), files, NullLogger<ViewStateStore>.Instance);

    private static GameStateStore Game(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in Login.Concat(lines))
        {
            Apply(store, line);
        }

        return store;
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    [Fact]
    public void TowerLeftSelectedComesBackAfterARestartOnceTheCarrierIsFound()
    {
        var files = new MemoryFileSystem();

        using (var first = new Host(Store(files)))
        {
            first.Feed(Game(Carrier));
            first.Channels.NextSubChannel();
            Assert.Equal(new CommsSelection(CommsChannel.Carrier, new(CommsChannels.TowerKey, "Tower")), first.Channels.Selected);
        }

        using var second = new Host(Store(files));
        var game = Game();

        second.Feed(game);
        Assert.Equal(new CommsSelection(CommsChannel.Carrier, null), second.Channels.Selected);

        Apply(game, Carrier);
        second.Feed(game);
        Assert.Equal(new CommsIndicatorState("CARRIER", "TOWER"), CommsIndicatorState.Of(second.Channels, game.Active?.Target));
    }

    [Fact]
    public void TwoCommandersEachKeepTheirOwnSelectionAcrossASwitch()
    {
        var files = new MemoryFileSystem();
        var game = Game(Carrier, Hired, Active);

        using (var host = new Host(Store(files)))
        {
            host.Feed(game);
            Assert.True(host.Channels.Select(CommsChannel.Crew));

            Apply(game, SecondCommander);
            Apply(game, SecondCarrier);
            host.Feed(game);
            Assert.Equal(new CommsSelection(CommsChannel.Carrier, new(CommsChannels.CaptainKey, "Captain")), host.Channels.Selected);
            host.Channels.NextSubChannel();

            Apply(game, FirstCommanderAgain);
            host.Feed(game);
            Assert.Equal(new CommsSelection(CommsChannel.Crew, new("1", "Samira Voss")), host.Channels.Selected);
        }

        using var restarted = new Host(Store(files));

        restarted.Feed(game);
        Assert.Equal(CommsChannel.Crew, restarted.Channels.Selected?.Channel);

        Apply(game, SecondCommander);
        restarted.Feed(game);
        Assert.Equal(new CommsSelection(CommsChannel.Carrier, new(CommsChannels.TowerKey, "Tower")), restarted.Channels.Selected);
    }

    [Fact]
    public void ACommanderWithNothingSavedStartsOnTheFirstAvailableChannel()
    {
        using var host = new Host(Store(new MemoryFileSystem()));

        host.Feed(Game(Hired, Active, Carrier));

        Assert.Equal(new CommsSelection(CommsChannel.Carrier, new(CommsChannels.CaptainKey, "Captain")), host.Channels.Selected);
    }

    [Fact]
    public void ABurstOfStepsIsWrittenOnceASecondAfterTheLast()
    {
        var files = new MemoryFileSystem();
        using var host = new Host(Store(files));
        var game = Game(Carrier, Hired, Active);

        host.Feed(game);
        host.Time.Advance(TimeSpan.FromSeconds(5));
        var writes = host.Writes;

        for (var step = 0; step < 3; step++)
        {
            host.Channels.NextChannel();
            host.Time.Advance(TimeSpan.FromMilliseconds(500));
        }

        Assert.Equal(writes, host.Writes);

        host.Time.Advance(TimeSpan.FromMilliseconds(500));

        Assert.Equal(writes + 1, host.Writes);
        Assert.Equal(host.Channels.Selected?.Channel, Store(files).Load().CommsByCommander["F1"].Channel);
    }

    [Fact]
    public void AChangeOnlyToWhatIsAvailableWritesNothing()
    {
        using var host = new Host(Store(new MemoryFileSystem()));
        var game = Game(Carrier);

        host.Feed(game);
        host.Time.Advance(TimeSpan.FromSeconds(5));
        var writes = host.Writes;

        Apply(game, Target);
        host.Feed(game);
        host.Time.Advance(TimeSpan.FromSeconds(5));

        Assert.Contains(CommsChannel.Target, host.Channels.Available);
        Assert.Equal(writes, host.Writes);
    }

    [Fact]
    public void AStepStillWaitingIsWrittenWhenTheHostStops()
    {
        var files = new MemoryFileSystem();

        using (var host = new Host(Store(files)))
        {
            host.Feed(Game(Carrier));
            host.Channels.NextSubChannel();
        }

        var saved = Store(files).Load().CommsByCommander["F1"];

        Assert.Contains("\"channel\": \"Carrier\"", files.ReadText(new AppPaths("/comms-selection-tests").ViewStateFile), StringComparison.Ordinal);
        Assert.Equal(CommsChannel.Carrier, saved.Channel);
        Assert.Equal(CommsChannels.TowerKey, saved.Parties[CommsChannel.Carrier]);
    }

    [Fact]
    public void AViewStateFileWithoutCommsLoads()
    {
        var files = new MemoryFileSystem();
        var paths = new AppPaths("/comms-selection-tests");
        files.WriteText(paths.ViewStateFile, """{"collapsedCards":["a"],"pinsByCommander":{}}""");

        var state = Store(files).Load();

        Assert.Equal(["a"], state.CollapsedCards);
        Assert.Empty(state.CommsByCommander);
    }

    /// <summary>The comms channels and their memory, fed as <c>AppHost.FeedCommsChannels</c> feeds them.</summary>
    private sealed class Host : IDisposable
    {
        private readonly CommsSelectionMemory _memory;

        public Host(ViewStateStore store) =>
            _memory = new CommsSelectionMemory(Channels, store, action => { Writes++; action(); }, Time);

        public CommsChannels Channels { get; } = new();

        public ManualClock Time { get; } = new();

        public int Writes { get; private set; }

        public void Feed(GameStateStore game)
        {
            _memory.Follow(game.Active?.Identity.FrontierId);
            Channels.Update(CommsFeed.Inputs(game.Active, [], null, []));
        }

        public void Dispose() => _memory.Dispose();
    }

    /// <summary>A clock that moves only when <see cref="Advance"/> is called, firing due timers on the caller's thread.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly List<Scheduled> _timers = [];
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Scheduled(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            var target = _now + by;

            while (_timers.Where(timer => timer.Due <= target).OrderBy(timer => timer.Due).FirstOrDefault() is { } next)
            {
                _now = next.Due!.Value;
                next.Due = null;
                next.Fire();
            }

            _now = target;
        }

        private sealed class Scheduled(ManualClock owner, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset? Due { get; set; }

            public void Fire() => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;

                if (!owner._timers.Contains(this))
                {
                    owner._timers.Add(this);
                }

                return true;
            }

            public void Dispose() => owner._timers.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
