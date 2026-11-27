using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>What is still worth doing in the system, said when the FSS closes (#2760).</summary>
public class ClosingTheFssSaysWhatIsLeftTests
{
    private const long Here = 1000300;

    private static readonly DateTimeOffset Start = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Open = GameStatus.Unknown with { GuiFocus = GuiFocus.FssMode };

    private static readonly GameStatus Closed = GameStatus.Unknown;

    private sealed class Said(IEnumerable<Announcement> announcements) : ICallout
    {
        private readonly List<Announcement> _announcements = [.. announcements];

        public string Id => "said";

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            var now = _announcements.ToList();
            _announcements.Clear();
            return now;
        }
    }

    private sealed class World
    {
        private readonly CartographyLedger _ledger = new(null, new MemoryFileSystem(), NullLogger.Instance);
        private readonly SignalSightings _signals = new(new MemoryFileSystem(), NullLogger.Instance);
        private readonly GameStateStore _store = new();

        public World(params string[] lines)
        {
            _ledger.FoldHistory([], TestContext.Current.CancellationToken);

            Add(
                """{"timestamp":"2026-11-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                """{"timestamp":"2026-11-20T09:00:01Z","event":"LoadGame","FID":"F1","Commander":"Fixture","Credits":1}""",
                $$"""{"timestamp":"2026-11-20T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":{{Here}},"StarPos":[0,0,0]}""");
            Add(lines);

            Engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
            Engine.Add(new FssCloseCallout(_ledger, _signals));
        }

        public CalloutEngine Engine { get; }

        public void Add(params string[] lines)
        {
            var events = lines.Select(Parse).ToList();

            foreach (var journalEvent in events)
            {
                _store.Apply(journalEvent);
            }

            _ledger.Apply(events);
            _signals.Apply(events);
            _store.Active!.CodexLedger = _store.Active.CodexLedger with { IsWhole = true };
        }

        public List<string> Tick(GameStatus status, int seconds, bool priming = false)
        {
            Engine.Tick(new CalloutContext(Start.AddSeconds(seconds), priming, _store.Active, status, NavRoute.None, []));

            return [.. Engine.Drain().Select(announcement => announcement.Text)];
        }
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Guardian(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Guardian;","Type_Localised":"Guardian","Count":2}]}
        """;

    private static string Undiscovered(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Rocky body","Landable":false,"WasDiscovered":false,"WasMapped":true}
        """;

    [Fact]
    public void ClosingTheFssSaysWhatIsLeftOnce()
    {
        var world = new World(Guardian(10));

        Assert.Empty(world.Tick(Closed, 0, priming: true));
        Assert.Empty(world.Tick(Open, 1));

        Assert.Equal(["Worth doing here: Guardian signals on 10."], world.Tick(Closed, 2));
        Assert.Empty(world.Tick(Closed, 3));
    }

    [Fact]
    public void AnFssOpenAtStartupSaysNothingWhenItCloses()
    {
        var world = new World(Guardian(10));

        Assert.Empty(world.Tick(Open, 0, priming: true));
        Assert.Empty(world.Tick(Closed, 1));
    }

    [Fact]
    public void NothingLeftSaysNothing()
    {
        var world = new World();

        Assert.Empty(world.Tick(Open, 0));
        Assert.Empty(world.Tick(Closed, 1));
    }

    [Fact]
    public void FiveItemsNameThreeAndCountTheRest()
    {
        var world = new World(Guardian(10), Guardian(11), Guardian(12), Guardian(13), Guardian(14));

        world.Tick(Open, 0);

        Assert.Equal(
            ["Worth doing here: Guardian signals on 10; Guardian signals on 11; Guardian signals on 12; and two more."],
            world.Tick(Closed, 1));
    }

    [Fact]
    public void UndiscoveredBodiesAreCounted()
    {
        var world = new World(Guardian(10), Undiscovered(1), Undiscovered(2));

        world.Tick(Open, 0);

        Assert.Equal(["Worth doing here: Guardian signals on 10. Two first discoveries."], world.Tick(Closed, 1));
    }

    [Fact]
    public void ABodyJustSaidByItsOwnLineIsCountedButNotNamedAgain()
    {
        var world = new World(Guardian(10), Guardian(11));

        world.Engine.Add(new Said([new Announcement($"body-value.{Here}.10", "Fixture 10: a rocky world.")]));

        world.Tick(Open, 0);

        Assert.Equal(["Worth doing here: Guardian signals on 11; and one more."], world.Tick(Closed, 30));
    }

    [Fact]
    public void ABodySaidMoreThanAMinuteAgoIsNamedAgain()
    {
        var world = new World(Guardian(10), Guardian(11));

        world.Engine.Add(new Said([new Announcement($"geology.{Here}.10", "Fixture 10 could have geysers.")]));

        world.Tick(Open, 0);

        Assert.Equal(
            ["Worth doing here: Guardian signals on 10; Guardian signals on 11."],
            world.Tick(Closed, 61));
    }

    [Fact]
    public void ReopeningAndClosingWithNothingNewSaysNothing()
    {
        var world = new World(Guardian(10));

        world.Tick(Open, 0);
        Assert.Single(world.Tick(Closed, 1));

        world.Tick(Open, 2);
        Assert.Empty(world.Tick(Closed, 3));
    }

    [Fact]
    public void ReopeningAndClosingAfterANewScanSaysItAgain()
    {
        var world = new World(Guardian(10));

        world.Tick(Open, 0);
        Assert.Single(world.Tick(Closed, 1));

        world.Tick(Open, 2);
        world.Add(Guardian(11));

        Assert.Equal(
            ["Worth doing here: Guardian signals on 10; Guardian signals on 11."],
            world.Tick(Closed, 3));
    }

    [Fact]
    public void TheSwitchTurnsItOff()
    {
        var world = new World(Guardian(10));

        world.Engine.SetEnabled("fss-close", false);
        world.Tick(Open, 0);

        Assert.Empty(world.Tick(Closed, 1));
    }
}
