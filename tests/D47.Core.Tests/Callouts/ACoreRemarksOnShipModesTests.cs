using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The remark on a ship mode held for a while (#1445).</summary>
public class ACoreRemarksOnShipModesTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private const StatusFlags Flying = StatusFlags.InMainShip;

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException(json);

    private static JournalEvent At(DateTimeOffset when, string kind) =>
        Event($$"""{ "timestamp":"{{when:yyyy-MM-ddTHH:mm:ssZ}}", "event":"{{kind}}" }""");

    private static IEnumerable<Announcement> Tick(
        ShipModeCallout callout, int seconds, StatusFlags flags, bool priming = false, params JournalEvent[] events) =>
        callout.Examine(new CalloutContext(
            Noon.AddSeconds(seconds), priming, null, new GameStatus { Flags = flags }, NavRoute.None, events));

    private static List<Announcement> Run(ShipModeCallout callout, int seconds, StatusFlags flags, params JournalEvent[] events) =>
        [.. Tick(callout, seconds, flags, false, events)];

    [Theory]
    [InlineData(StatusFlags.SilentRunning, 10, "shipmode.silent")]
    [InlineData(StatusFlags.FlightAssistOff, 30, "shipmode.faoff")]
    [InlineData(StatusFlags.HardpointsDeployed, 60, "shipmode.hardpoints")]
    [InlineData(StatusFlags.CargoScoopDeployed, 60, "shipmode.scoop")]
    [InlineData(StatusFlags.LandingGearDown, 60, "shipmode.gear")]
    [InlineData(StatusFlags.FsdMassLocked | StatusFlags.InDanger, 10, "shipmode.masslocked")]
    public void EachModeIsRemarkedOnceAfterItsHoldTime(StatusFlags mode, int hold, string key)
    {
        var callout = new ShipModeCallout();
        var flags = Flying | mode;

        Assert.Empty(Run(callout, 0, flags));
        Assert.Empty(Run(callout, hold - 1, flags));

        var said = Assert.Single(Run(callout, hold, flags));
        Assert.Equal(key, said.Key);
        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
        Assert.Empty(Run(callout, hold + 1, flags));
        Assert.Empty(Run(callout, hold + 3600, flags));
    }

    [Fact]
    public void AFlagThatClearsBeforeItsHoldTimeStartsTheClockAgain()
    {
        var callout = new ShipModeCallout();
        var silent = Flying | StatusFlags.SilentRunning;

        Assert.Empty(Run(callout, 0, silent));
        Assert.Empty(Run(callout, 8, silent));
        Assert.Empty(Run(callout, 9, Flying));
        Assert.Empty(Run(callout, 10, silent));
        Assert.Empty(Run(callout, 19, silent));
        Assert.Single(Run(callout, 20, silent));
    }

    [Theory]
    [InlineData(StatusFlags.Docked)]
    [InlineData(StatusFlags.Landed)]
    [InlineData(StatusFlags.Supercruise)]
    public void NothingIsSaidDockedLandedOrInSupercruise(StatusFlags where)
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.SilentRunning | where;

        Assert.Empty(Run(callout, 0, flags));
        Assert.Empty(Run(callout, 60, flags));
    }

    [Fact]
    public void NothingIsSaidOutsideTheMainShip()
    {
        var callout = new ShipModeCallout();
        var flags = StatusFlags.InFighter | StatusFlags.SilentRunning;

        Assert.Empty(Run(callout, 0, flags));
        Assert.Empty(Run(callout, 60, flags));
    }

    [Fact]
    public void HardpointsInDangerAreNotRemarkedOn()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.HardpointsDeployed | StatusFlags.InDanger;

        Assert.Empty(Run(callout, 0, flags));
        Assert.Empty(Run(callout, 120, flags));
    }

    [Theory]
    [InlineData(StatusFlags.HardpointsDeployed, "ProspectedAsteroid")]
    [InlineData(StatusFlags.CargoScoopDeployed, "ProspectedAsteroid")]
    [InlineData(StatusFlags.CargoScoopDeployed, "MiningRefined")]
    [InlineData(StatusFlags.CargoScoopDeployed, "CollectCargo")]
    public void MiningInTheLastTenMinutesKeepsItQuiet(StatusFlags mode, string recent)
    {
        var callout = new ShipModeCallout();
        var flags = Flying | mode;

        Assert.Empty(Run(callout, 0, flags, At(Noon.AddSeconds(-30), recent)));
        Assert.Empty(Run(callout, 60, flags));
        Assert.Empty(Run(callout, 500, flags));
    }

    [Fact]
    public void HardpointsAreRemarkedOnceTheMiningIsTenMinutesOld()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.HardpointsDeployed;

        Assert.Empty(Run(callout, 0, flags, At(Noon, "AsteroidCracked")));
        Assert.Empty(Run(callout, 599, flags));
        Assert.Single(Run(callout, 600, flags));
    }

    [Fact]
    public void ACollectedCargoDoesNotQuietHardpoints()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.HardpointsDeployed;

        Assert.Empty(Run(callout, 0, flags, At(Noon, "CollectCargo")));
        Assert.Single(Run(callout, 60, flags));
    }

    [Fact]
    public void GearDownWithMassLockIsNotRemarkedOn()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.LandingGearDown | StatusFlags.FsdMassLocked;

        Assert.Empty(Run(callout, 0, flags));
        Assert.Empty(Run(callout, 120, flags));
    }

    [Fact]
    public void TwoModesHeldTogetherGiveOneRemarkAndTheSecondWaitsOutTheSpacing()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.SilentRunning | StatusFlags.FlightAssistOff;

        Assert.Empty(Run(callout, 0, flags));
        Assert.Equal("shipmode.silent", Assert.Single(Run(callout, 30, flags)).Key);
        Assert.Empty(Run(callout, 31, flags));
        Assert.Empty(Run(callout, 329, flags));
        Assert.Equal("shipmode.faoff", Assert.Single(Run(callout, 330, flags)).Key);
    }

    [Fact]
    public void ALoadGameStartsTheModesAgain()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.SilentRunning;
        _ = Run(callout, 0, flags);
        Assert.Single(Run(callout, 10, flags));

        Assert.Empty(Run(callout, 20, flags, At(Noon.AddSeconds(20), "LoadGame")));
        Assert.Single(Run(callout, 30, flags));
    }

    [Fact]
    public void ABacklogIsNeverRemarkedOn()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.SilentRunning;

        Assert.Empty(Tick(callout, 0, flags, true));
        Assert.Empty(Tick(callout, 60, flags, true));
        Assert.Empty(Run(callout, 61, flags));
    }

    [Fact]
    public void TheAnnouncementCarriesNoCommsOrTranscript()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.SilentRunning;
        _ = Run(callout, 0, flags);

        var said = Assert.Single(Run(callout, 10, flags));

        Assert.Null(said.CommsChannel);
        Assert.Null(said.Transcript);
    }

    [Fact]
    public void TheBriefIsOfferedWithPersonalityOnAndWithheldWithItOff()
    {
        var callout = new ShipModeCallout();
        var flags = Flying | StatusFlags.SilentRunning;
        _ = Run(callout, 0, flags);
        var said = Assert.Single(Run(callout, 10, flags));

        var brief = FlavourBriefs.For(said, true, "Doug");

        Assert.NotNull(brief);
        Assert.Contains(said.Text, brief.Instruction);
        Assert.Contains("silent running", brief.Instruction);
        Assert.Null(FlavourBriefs.For(said, false, "Doug"));
    }

    [Fact]
    public void TheToggleIsOnByDefault() =>
        Assert.True(new D47.Core.Configuration.CalloutSettings().ShipModes);

    [Fact]
    public void TheToggleHasARow() =>
        Assert.Equal("callouts.shipModes", CalloutCapability.ShipModesKey);
}
