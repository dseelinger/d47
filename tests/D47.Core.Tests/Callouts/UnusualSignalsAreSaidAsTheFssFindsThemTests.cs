using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Notable stellar phenomena and Guardian or Thargoid sites, said as they are found (#2762).</summary>
public class UnusualSignalsAreSaidAsTheFssFindsThemTests
{
    private const long Here = 1000300;

    private static readonly DateTimeOffset Start = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        private readonly CalloutEngine _engine = new(NullLogger<CalloutEngine>.Instance);
        private readonly GameStateStore _store = new();
        private int _seconds;

        public World()
        {
            _engine.Add(new UnusualSignalsCallout());

            foreach (var line in new[]
            {
                """{"timestamp":"2026-11-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                """{"timestamp":"2026-11-20T09:00:01Z","event":"LoadGame","FID":"F1","Commander":"Fixture","Credits":1}""",
                $$"""{"timestamp":"2026-11-20T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":{{Here}},"StarPos":[0,0,0]}""",
            })
            {
                _store.Apply(Parse(line));
            }
        }

        public List<string> Tick(bool priming, params string[] lines)
        {
            var events = lines.Select(Parse).ToList();

            _engine.Tick(new CalloutContext(Start.AddSeconds(_seconds++), priming, _store.Active, GameStatus.Unknown, NavRoute.None, events));

            return [.. _engine.Drain().Select(announcement => announcement.Text)];
        }
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Phenomenon(string name, long address = Here) =>
        $$"""{"timestamp":"2026-11-20T10:00:00Z","event":"FSSSignalDiscovered","SystemAddress":{{address}},"SignalName":"{{name}}","SignalName_Localised":"Notable stellar phenomena","SignalType":"Codex"}""";

    private static string Signal(string kind, string signalType, string signalName) =>
        $$"""{"timestamp":"2026-11-20T10:00:00Z","event":"FSSSignalDiscovered","SystemAddress":{{Here}},"SignalName":"{{signalName}}","SignalType":"{{signalType}}","kind":"{{kind}}"}""";

    private static string Site(string kind, string type, int count, int bodyId = 4, string name = "Fixture A 4") =>
        $$"""{"timestamp":"2026-11-20T10:00:00Z","event":"{{kind}}","BodyName":"{{name}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},"Signals":[{"Type":"{{type}}","Count":{{count}}}]}""";

    private const string GuardianType = "$SAA_SignalType_Guardian;";

    private const string ThargoidType = "$SAA_SignalType_Thargoid;";

    [Fact]
    public void TwoPhenomenaInOneSystemGiveOneLine()
    {
        var world = new World();

        Assert.Equal(
            ["Notable stellar phenomena in this system."],
            world.Tick(false, Phenomenon("$Fixed_Event_Life_Cloud;"), Phenomenon("$Fixed_Event_Life_Ring;")));
        Assert.Empty(world.Tick(false, Phenomenon("$Fixed_Event_Life_Ring;")));
    }

    [Fact]
    public void AnotherSystemSaysItsPhenomenaAgain()
    {
        var world = new World();

        Assert.Single(world.Tick(false, Phenomenon("$Fixed_Event_Life_Cloud;")));
        Assert.Single(world.Tick(false, Phenomenon("$Fixed_Event_Life_Cloud;", 2000400)));
    }

    [Fact]
    public void AGuardianCountOfTwoIsSaidOnceAcrossTheFssAndTheSurfaceScan()
    {
        var world = new World();

        Assert.Equal(["A 4: two Guardian signals."], world.Tick(false, Site("FSSBodySignals", GuardianType, 2)));
        Assert.Empty(world.Tick(false, Site("SAASignalsFound", GuardianType, 2)));
    }

    [Fact]
    public void ASurfaceScanFirstIsSaidAndTheFssAfterItIsNot()
    {
        var world = new World();

        Assert.Single(world.Tick(false, Site("SAASignalsFound", GuardianType, 3)));
        Assert.Empty(world.Tick(false, Site("FSSBodySignals", GuardianType, 3)));
    }

    [Fact]
    public void AThargoidCountOfOneDoesNotSayTheCount()
    {
        var world = new World();

        Assert.Equal(["B 1: Thargoid signals."], world.Tick(false, Site("FSSBodySignals", ThargoidType, 1, 7, "Fixture B 1")));
    }

    [Fact]
    public void CombatAndCarrierSignalsSayNothing()
    {
        var world = new World();

        Assert.Empty(world.Tick(false, Site("FSSBodySignals", "$SAA_SignalType_Human;", 2)));
        Assert.Empty(world.Tick(false, Signal("x", "Combat", "$Warzone_PointRace_Low;")));
        Assert.Empty(world.Tick(false, Signal("x", "FleetCarrier", "BNH-T2F")));
    }

    [Fact]
    public void PrimingSaysNothingAndIsNotSaidLater()
    {
        var world = new World();

        Assert.Empty(world.Tick(true, Phenomenon("$Fixed_Event_Life_Cloud;"), Site("FSSBodySignals", GuardianType, 2)));
        Assert.Empty(world.Tick(false, Phenomenon("$Fixed_Event_Life_Ring;"), Site("SAASignalsFound", GuardianType, 2)));
    }
}
