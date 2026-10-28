using D47.Core.Diagnostics.Donation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheEventsD47DidNotKnowAreNarratedTests
{
    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static string Say(string kind, string fields = "") =>
        JournalSentence.For(Parse($$"""{ "timestamp":"2026-02-10T09:05:00Z", "event":"{{kind}}"{{fields}} }"""));

    [Fact]
    public void ASwappedModuleIsNamed() =>
        Assert.Equal(
            "Moved the Xeno Multi-Limpet Controller to another slot",
            Say("ModuleSwap", ""","FromItem":"$int_multidronecontrol_xeno_size3_class4_name;","FromItem_Localised":"Xeno Multi-Limpet Controller","ToItem":"Null" """));

    [Fact]
    public void AMassStoreCountsItsModules() =>
        Assert.Equal("Stored 2 modules", Say("MassModuleStore", ""","Items":[{"Slot":"A"},{"Slot":"B"}]"""));

    [Fact]
    public void AHackedHoloscreenNamesThePower() =>
        Assert.Equal("Hacked a holoscreen for Nakato Kaine", Say("HoloscreenHacked", ""","PowerAfter":"Nakato Kaine" """));

    [Fact]
    public void ACrewmateTakesASeat() =>
        Assert.Equal("A crewmate took the Idle seat", Say("CrewMemberRoleChange", ""","Crew":"Zed Mate","Role":"Idle" """));

    [Fact]
    public void AnImpoundedShipIsNamed() =>
        Assert.Equal("Released the impounded Krait Phantom", Say("ClearImpound", ""","ShipType":"Krait_Light","ShipType_Localised":"Krait Phantom" """));

    [Fact]
    public void ASquadronInviteNamesTheSquadron() =>
        Assert.Equal("Invited to EXAMPLE SQUADRON", Say("InvitedToSquadron", ""","SquadronName":"EXAMPLE SQUADRON" """));

    [Fact]
    public void ThePassengerManifestAddsItsCounts() =>
        Assert.Equal(
            "Passenger manifest: 5 aboard",
            Say("Passengers", ""","Manifest":[{"Count":2},{"Count":3}]"""));

    [Theory]
    [InlineData("CrewMemberJoins")]
    [InlineData("CrewMemberQuits")]
    [InlineData("KickCrewMember")]
    [InlineData("EndCrewSession")]
    [InlineData("SelfDestruct")]
    [InlineData("VehicleSwitch")]
    [InlineData("RequestPowerMicroResources")]
    public void EveryOtherEventSaysMoreThanItsName(string kind) =>
        Assert.NotEqual(kind, Say(kind));

    [Fact]
    public void MarketIdIsNoise() => Assert.Contains("MarketID", (ISet<string>)new HashSet<string>(JournalSentence.Noise));

    [Theory]
    [InlineData("ModuleSwap")]
    [InlineData("MassModuleStore")]
    [InlineData("ClearImpound")]
    [InlineData("HoloscreenHacked")]
    [InlineData("SelfDestruct")]
    [InlineData("VehicleSwitch")]
    [InlineData("RequestPowerMicroResources")]
    [InlineData("InvitedToSquadron")]
    [InlineData("Passengers")]
    [InlineData("MarketID")]
    [InlineData("CrewMemberJoins")]
    [InlineData("CrewMemberQuits")]
    [InlineData("CrewMemberRoleChange")]
    [InlineData("KickCrewMember")]
    [InlineData("EndCrewSession")]
    public void EveryOneIsNarratedOnly(string kind) => Assert.Contains(kind, (IReadOnlySet<string>)HandledEvents.NarratedOnly);

    [Theory]
    [InlineData("""{"event":"CrewMemberJoins","Crew":"Zed Mate"}""", "Zed Mate")]
    [InlineData("""{"event":"CrewMemberQuits","Crew":"Zed Mate"}""", "Zed Mate")]
    [InlineData("""{"event":"CrewMemberRoleChange","Crew":"Zed Mate","Role":"Idle"}""", "Zed Mate")]
    [InlineData("""{"event":"KickCrewMember","Crew":"Zed Mate"}""", "Zed Mate")]
    [InlineData("""{"event":"InvitedToSquadron","SquadronName":"ZED SQUAD"}""", "ZED SQUAD")]
    public void TheScrubReplacesTheNameOnEachLine(string line, string name) =>
        Assert.DoesNotContain(name, JournalScrub.Line(line, new Pseudonyms()).Json!);
}
