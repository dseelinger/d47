using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A player-controlled LaunchVessel, the Nomad leaving a Mk II Vessel Hangar, is read as LaunchSRV.</summary>
public class ANomadLaunchIsAnSrvLaunchTests
{
    private static JournalEvent Event(string time, string body)
    {
        Assert.True(JournalEvent.TryParse("{\"timestamp\":\"" + time + "\"," + body + "}", NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CombatRecord Fold(params JournalEvent[] events) =>
        events.Aggregate(CombatRecord.None, (record, journalEvent) => record.Apply(journalEvent));

    private static JournalEvent Eagle(string time) =>
        Event(time, "\"event\":\"Bounty\",\"Target\":\"eagle\",\"TotalReward\":1000,\"VictimFaction\":\"X\",\"Rewards\":[{\"Faction\":\"A\",\"Reward\":1000}]");

    private static JournalEvent Launch(string time, bool playerControlled) =>
        Event(time, "\"event\":\"LaunchVessel\",\"VesselType\":\"lander01\",\"VesselType_Localised\":\"Nomad\",\"PlayerControlled\":" + (playerControlled ? "true" : "false"));

    [Fact]
    public void AKillInTheNomadIsAnSrvKillUntilTheCommanderDocksIt()
    {
        var record = Fold(
            Launch("2026-10-26T10:00:00Z", true),
            Event("2026-10-26T10:00:10Z", "\"event\":\"Disembark\",\"SRV\":true"),
            Event("2026-10-26T10:00:20Z", "\"event\":\"Embark\",\"SRV\":true"),
            Eagle("2026-10-26T10:01:00Z"),
            Event("2026-10-26T10:02:00Z", "\"event\":\"DockSRV\",\"SRVType\":\"lander01\""),
            Eagle("2026-10-26T10:03:00Z"));

        Assert.Equal([CombatVehicle.Srv, CombatVehicle.Ship], record.Kills.Select(kill => kill.Vehicle));
    }

    [Fact]
    public void ALaunchVesselNobodyFliesChangesNoVehicle()
    {
        var record = Fold(Launch("2026-10-26T10:00:00Z", false), Eagle("2026-10-26T10:01:00Z"));

        Assert.Equal(CombatVehicle.Ship, Assert.Single(record.Kills).Vehicle);
    }

    [Fact]
    public void ALaunchVesselIsNarratedByItsLocalisedName()
    {
        Assert.Equal("Launched the Nomad", JournalSentence.For(Launch("2026-10-26T10:00:00Z", true)));
        Assert.Equal("Launched the Nomad", JournalSentence.For(Launch("2026-10-26T10:00:00Z", false)));
    }

    [Fact]
    public void OnlyAPlayerControlledLaunchVesselLaunchesAnSrv()
    {
        Assert.True(Launch("2026-10-26T10:00:00Z", true).LaunchesSrv);
        Assert.False(Launch("2026-10-26T10:00:00Z", false).LaunchesSrv);
    }
}
