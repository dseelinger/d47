using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

public class TheNomadIsFlownOnTheShipBindingsTests
{
    private const string Folder = @"C:\d47-nomad";

    private const string Nomad = "\"event\":\"LaunchVessel\",\"VesselType\":\"lander01\",\"VesselType_Localised\":\"Nomad\",\"PlayerControlled\":true,\"ID\":7";

    private static JournalEvent Event(string body)
    {
        Assert.True(JournalEvent.TryParse("{\"timestamp\":\"2026-11-08T10:00:00Z\"," + body + "}", NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GameStatusReader Reader()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            Path.Combine(Folder, GameStatusReader.FileName),
            """{ "timestamp":"2026-11-08T10:00:00Z", "event":"Status", "Flags":67108870, "Flags2":0, "GuiFocus":0 }""");

        var reader = new GameStatusReader(Folder, files, NullLogger.Instance);
        Assert.True(reader.Poll());
        Assert.True(reader.Current.Has(StatusFlags.InSrv));
        return reader;
    }

    private static ControlContext After(GameStatusReader reader, string body, bool onFoot = false)
    {
        reader.Observe([Event(body)]);
        return ControlContexts.Of(onFoot ? reader.Current with { Flags2 = 1 } : reader.Current);
    }

    [Fact]
    public void TheNomadIsTheModeFromLaunchToDockingAndOnFootStaysOnFoot()
    {
        var reader = Reader();

        Assert.Equal(ControlContext.Nomad, After(reader, Nomad));
        Assert.Equal(ControlContext.Nomad, After(reader, "\"event\":\"Touchdown\",\"PlayerControlled\":true"));
        Assert.Equal(ControlContext.OnFoot, After(reader, "\"event\":\"Disembark\",\"SRV\":true,\"ID\":7", onFoot: true));
        Assert.Equal(ControlContext.Nomad, After(reader, "\"event\":\"Embark\",\"SRV\":true,\"ID\":7"));
        Assert.Equal(ControlContext.Nomad, After(reader, "\"event\":\"Liftoff\",\"PlayerControlled\":true"));

        reader.Observe([Event("\"event\":\"DockSRV\",\"SRVType\":\"lander01\"")]);
        Assert.False(reader.Current.InNomad);
    }

    [Fact]
    public void AnSrvLaunchedWithLaunchSrvIsStillAnSrv()
    {
        var reader = Reader();

        Assert.Equal(ControlContext.Srv, After(reader, "\"event\":\"LaunchSRV\",\"SRVType\":\"testbuggy\",\"PlayerControlled\":true"));
        Assert.Equal(ControlContext.OnFoot, After(reader, "\"event\":\"Disembark\",\"SRV\":true,\"ID\":7", onFoot: true));
        Assert.Equal(ControlContext.Srv, After(reader, "\"event\":\"Embark\",\"SRV\":true,\"ID\":7"));
    }

    [Fact]
    public void AnEmbarkOnAnotherVehicleEndsTheNomad()
    {
        var reader = Reader();
        After(reader, Nomad);

        Assert.Equal(ControlContext.Srv, After(reader, "\"event\":\"Embark\",\"SRV\":true,\"ID\":9"));
    }

    [Theory]
    [InlineData("LoadGame")]
    [InlineData("Died")]
    [InlineData("SRVDestroyed")]
    public void TheNomadEndsWhereTheSrvDoes(string kind)
    {
        var reader = Reader();
        After(reader, Nomad);

        Assert.Equal(ControlContext.Srv, After(reader, "\"event\":\"" + kind + "\""));
    }

    [Fact]
    public void LandingGearWorksInTheNomadAndChecksTheReport()
    {
        var gear = GameActions.Find("landing_gear")!;

        Assert.Equal("LandingGearToggle", gear.For(ControlContext.Nomad)?.EliteAction);
        Assert.Null(gear.For(ControlContext.Srv));

        var down = new GameStatus { Flags = StatusFlags.InSrv | StatusFlags.LandingGearDown, InNomad = true, ReadAt = DateTimeOffset.UnixEpoch };
        Assert.True(gear.AlreadyIn(DesiredState.On, down));
    }

    [Fact]
    public void EverySwitchButTheLandingGearPressesWithoutCheckingInTheNomad()
    {
        var status = new GameStatus { Flags = StatusFlags.InSrv, InNomad = true, ReadAt = DateTimeOffset.UnixEpoch };

        foreach (var action in GameActions.All.Where(a => a.Reports is not null && a.Id != "landing_gear"))
        {
            Assert.Null(action.AlreadyIn(DesiredState.On, status));
        }
    }

    [Fact]
    public void JumpAndExplorationActionsAreRefusedInTheNomad()
    {
        var refused = new[] { "HyperSuperCombination", "Supercruise", "Hyperspace", "TargetNextRouteSystem" };

        foreach (var variant in GameActions.All.SelectMany(action => action.Variants))
        {
            if (refused.Contains(variant.EliteAction) || variant.EliteAction.StartsWith("ExplorationFSS", StringComparison.Ordinal))
            {
                Assert.True(0 == (int)(variant.Contexts & ControlContext.Nomad), variant.EliteAction);
            }
        }

        Assert.Equal("in the Nomad", ControlContexts.Describe(ControlContext.Nomad));
    }

    [Fact]
    public void NoBuggyVariantCoversTheNomadAndShipOnesDo()
    {
        var variants = GameActions.All.SelectMany(action => action.Variants).ToArray();

        Assert.DoesNotContain(variants, v => v.EliteAction.Contains("Buggy", StringComparison.Ordinal) && (v.Contexts & ControlContext.Nomad) != 0);
        Assert.Contains(variants, v => v.EliteAction == "CycleFireGroupNext" && (v.Contexts & ControlContext.Nomad) != 0);
        Assert.Contains(variants, v => v.EliteAction == "IncreaseEnginesPower" && (v.Contexts & ControlContext.Nomad) != 0);
        Assert.Equal(0, (int)(ControlContext.AnyShip & ControlContext.Nomad));
    }
}
