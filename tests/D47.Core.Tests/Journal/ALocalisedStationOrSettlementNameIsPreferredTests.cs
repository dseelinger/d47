using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A station or settlement whose name is a token is stored and spoken by its localised name (#1785).</summary>
public class ALocalisedStationOrSettlementNameIsPreferredTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed), json);
        return parsed!;
    }

    private static JournalLocation Located(string json) =>
        new JournalLocation(null, null, false, null).Apply(Event(json));

    [Theory]
    [InlineData("$Operations_Runner_Name:#index=1;", "Frontline Runner")]
    [InlineData("$EXT_PANEL_ColonisationShip:#index=1;", "System Colonisation Ship")]
    public void ADockedTokenNameBecomesItsLocalisedName(string token, string localised)
    {
        var location = Located(
            $$"""{"timestamp":"2026-10-27T10:00:00Z","event":"Docked","StationName":"{{token}}","StationName_Localised":"{{localised}}","StationType":"Megaship","StarSystem":"Sol"}""");

        Assert.Equal(localised, location.StationName);
    }

    [Fact]
    public void ALocationAtATokenStationHoldsTheLocalisedName()
    {
        var location = Located(
            """{"timestamp":"2026-10-27T10:00:00Z","event":"Location","Docked":true,"StationName":"$Operations_Runner_Name:#index=1;","StationName_Localised":"Frontline Runner","StarSystem":"Sol"}""");

        Assert.Equal("Frontline Runner", location.StationName);
    }

    [Fact]
    public void TheColonisationShipPrefixStillYieldsTheName() =>
        Assert.Equal("Alpha Site", Located(
            """{"timestamp":"2026-10-27T10:00:00Z","event":"Docked","StationName":"$EXT_PANEL_ColonisationShip; Alpha Site","StarSystem":"Sol"}""").StationName);

    [Fact]
    public void AnOrdinaryStationKeepsItsName() =>
        Assert.Equal("Jameson Memorial", Located(
            """{"timestamp":"2026-10-27T10:00:00Z","event":"Docked","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra"}""").StationName);

    [Fact]
    public void TheDamageCalloutSpeaksTheLocalisedName()
    {
        var docked = Event(
            """{"timestamp":"2026-10-27T10:00:00Z","event":"Docked","StationName":"$Operations_Runner_Name:#index=1;","StationName_Localised":"Frontline Runner","StationState":"UnderAttack","StarSystem":"Sol"}""");

        var said = new StationDamageCallout().Examine(new CalloutContext(
            docked.Timestamp, false, null, GameStatus.Unknown, NavRoute.None, [docked])).Single();

        Assert.Equal("Frontline Runner is under attack.", said.Text);
    }

    [Fact]
    public void ASceneIsNamedByTheLocalisedSettlement()
    {
        var tracker = new SceneTracker();
        tracker.Fold(Event(
            """{"timestamp":"2026-10-27T10:00:00Z","event":"ApproachSettlement","Name":"$Ancient_Small_005:#index=1;","Name_Localised":"Guardian Structure","BodyName":"Body 1"}"""));
        tracker.Fold(Event(
            """{"timestamp":"2026-10-27T10:01:00Z","event":"Disembark","SRV":false,"Taxi":false,"Body":"Body 1","OnStation":false,"OnPlanet":true}"""));

        Assert.Equal("Guardian Structure", tracker.Snapshot.Last?.Settlement);
    }
}
