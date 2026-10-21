using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The remark on a notable passing ship (#1444).</summary>
public class ACoreRemarksOnNotableShipsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException(json);

    private static JournalEvent Hail(string message, string channel = "npc") =>
        Event($$"""{ "timestamp":"2026-10-21T12:00:00Z", "event":"ReceiveText", "From":"$ShipName_Example;", "From_Localised":"Some Ship", "Message":"{{message}}", "Message_Localised":"Hello there", "Channel":"{{channel}}" }""");

    private static JournalEvent LoadGame() =>
        Event("""{ "timestamp":"2026-10-21T12:00:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Doug" }""");

    private static CalloutContext Context(bool priming, params JournalEvent[] events) =>
        new(Noon, priming, null, GameStatus.Unknown, NavRoute.None, events);

    private static List<Announcement> Say(NotableShipCallout callout, bool priming, params JournalEvent[] events) =>
        [.. callout.Examine(Context(priming, events))];

    [Theory]
    [InlineData("$ConvoyWedding_Patrol02;", "wedding convoy")]
    [InlineData("$ConvoyFuneral_Patrol03;", "funeral procession")]
    [InlineData("$ConvoyNomads_Patrol01;", "nomad convoy")]
    [InlineData("$RefugeeFlotillaWar_Patrol01;", "refugee flotilla fleeing a war")]
    [InlineData("$PassengerLiner_Patrol01;", "tour liner")]
    [InlineData("$Protester_Patrol01;", "protester")]
    [InlineData("$Propagandist_Patrol01;", "propagandist")]
    [InlineData("$PoliceAcademy_Patrol01;", "security academy training flight")]
    public void EachFamilyIsRemarkedOnceAndNamedByItsKind(string message, string kind)
    {
        var callout = new NotableShipCallout();

        var said = Assert.Single(Say(callout, false, Hail(message)));

        Assert.StartsWith(NotableShipCallout.KeyPrefix, said.Key);
        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
        Assert.Equal(kind, NotableShipCallout.KindOf(said.Text));
        Assert.Empty(Say(callout, false, Hail(message)));
    }

    [Fact]
    public void ALoadGameStartsTheKindsAgain()
    {
        var callout = new NotableShipCallout();
        _ = Say(callout, false, Hail("$ConvoyWedding_Patrol02;"));

        Assert.Single(Say(callout, false, LoadGame(), Hail("$ConvoyWedding_Patrol02;")));
    }

    [Fact]
    public void ABacklogIsNeverRemarkedOn() =>
        Assert.Empty(Say(new NotableShipCallout(), true, Hail("$ConvoyWedding_Patrol02;")));

    [Fact]
    public void APlayerChannelNeverMatches() =>
        Assert.Empty(Say(new NotableShipCallout(), false, Hail("$ConvoyWedding_Patrol02;", "local")));

    [Theory]
    [InlineData("$CruiseLiner_Patrol01;")]
    [InlineData("$Police_Scan01;")]
    public void RoutineTrafficIsNotRemarkedOn(string message) =>
        Assert.Empty(Say(new NotableShipCallout(), false, Hail(message)));

    [Fact]
    public void TheAnnouncementCarriesNothingFromTheMessage()
    {
        var said = Assert.Single(Say(new NotableShipCallout(), false, Hail("$ConvoyWedding_Patrol02;")));

        Assert.Null(said.CommsChannel);
        Assert.Null(said.Transcript);
        Assert.DoesNotContain("Some Ship", said.Text);
        Assert.DoesNotContain("Hello there", said.Text);
    }

    [Fact]
    public void TheBriefIsOfferedWithPersonalityOnAndWithheldWithItOff()
    {
        var said = Assert.Single(Say(new NotableShipCallout(), false, Hail("$ConvoyFuneral_Patrol03;")));

        var brief = FlavourBriefs.For(said, true, "Doug");

        Assert.NotNull(brief);
        Assert.Contains(said.Text, brief.Instruction);
        Assert.Null(FlavourBriefs.For(said, false, "Doug"));
    }

    [Fact]
    public void TheToggleIsOnByDefault() =>
        Assert.True(new D47.Core.Configuration.CalloutSettings().NotableShips);

    [Fact]
    public void TheToggleHasARow() =>
        Assert.Equal("callouts.notableShips", CalloutCapability.NotableShipsKey);
}
