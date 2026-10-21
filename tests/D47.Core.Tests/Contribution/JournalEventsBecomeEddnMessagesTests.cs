using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class JournalEventsBecomeEddnMessagesTests
{
    private static readonly string[] Preamble =
    [
        """{"timestamp":"2026-10-20T09:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r334473/r0 "}""",
        """{"timestamp":"2026-10-20T09:00:01Z","event":"LoadGame","Horizons":true,"Odyssey":false,"gameversion":"4.1.0.100","build":"r334473/r0 "}""",
    ];

    private const string Jump =
        """{"timestamp":"2026-10-20T10:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0],"Wanted":true,"BoostUsed":1,"FuelLevel":10.5,"FuelUsed":1.2,"JumpDist":8.3,"Body_Localised":"x","Factions":[{"Name":"A","Name_Localised":"A","MyReputation":50,"HappiestSystem":"S","HomeSystem":"S","SquadronFaction":true,"Allegiance":"Federation"}]}""";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EddnComposer Composer(bool horizons = true)
    {
        var composer = new EddnComposer(() => "anon", "1.2.3", () => null);

        foreach (var line in horizons ? Preamble : Preamble[..1])
        {
            composer.Compose(Event(line));
        }

        return composer;
    }

    private static EddnEnvelope? Compose(EddnComposer composer, string line) => composer.Compose(Event(line));

    private static string Text(EddnEnvelope? envelope) => envelope!.Message.ToJsonString();

    [Fact]
    public void AnFsdJumpLosesItsPersonalFieldsAndFactionEntries()
    {
        var envelope = Compose(Composer(), Jump);

        Assert.NotNull(envelope);
        Assert.Equal("https://eddn.edcd.io/schemas/journal/1", envelope.SchemaRef);

        var text = Text(envelope);

        foreach (var removed in new[] { "Wanted", "BoostUsed", "FuelLevel", "FuelUsed", "JumpDist", "_Localised", "MyReputation", "HappiestSystem", "HomeSystem", "SquadronFaction" })
        {
            Assert.DoesNotContain(removed, text, StringComparison.Ordinal);
        }

        Assert.Equal("Sol", (string?)envelope.Message["StarSystem"]);
        Assert.Equal(10477373803, (long?)envelope.Message["SystemAddress"]);
        Assert.Equal("Federation", (string?)envelope.Message["Factions"]![0]!["Allegiance"]);
        Assert.True((bool?)envelope.Message["horizons"]);
        Assert.False((bool?)envelope.Message["odyssey"]);
    }

    [Fact]
    public void ALocationLosesWantedAndPlanetPosition()
    {
        var envelope = Compose(
            Composer(),
            """{"timestamp":"2026-10-20T10:00:00Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0],"Wanted":false,"Latitude":1.5,"Longitude":2.5,"StationName_Localised":"x","Factions":[{"Name":"A","MyReputation":1,"HomeSystem":"S"}]}""");

        var text = Text(envelope);

        foreach (var removed in new[] { "Wanted", "Latitude", "Longitude", "_Localised", "MyReputation", "HomeSystem" })
        {
            Assert.DoesNotContain(removed, text, StringComparison.Ordinal);
        }

        Assert.Equal("Sol", (string?)envelope!.Message["StarSystem"]);
    }

    [Fact]
    public void ACarrierJumpLosesTheFieldsOfAJumpAndALocation()
    {
        var envelope = Compose(
            Composer(),
            """{"timestamp":"2026-10-20T10:00:00Z","event":"CarrierJump","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0],"Wanted":false,"Latitude":1.5,"StationName_Localised":"x","Factions":[{"Name":"A","SquadronFaction":true}]}""");

        var text = Text(envelope);

        foreach (var removed in new[] { "Wanted", "Latitude", "_Localised", "SquadronFaction" })
        {
            Assert.DoesNotContain(removed, text, StringComparison.Ordinal);
        }

        Assert.Equal("Sol", (string?)envelope!.Message["StarSystem"]);
    }

    [Fact]
    public void ADockedLosesWantedFinesAndBreachAndGainsThePosition()
    {
        var composer = Composer();
        Compose(composer, Jump);

        var envelope = Compose(
            composer,
            """{"timestamp":"2026-10-20T10:05:00Z","event":"Docked","StationName":"Abraham Lincoln","StationType":"Orbis","StarSystem":"Sol","SystemAddress":10477373803,"MarketID":128000001,"Wanted":true,"ActiveFine":true,"CockpitBreach":true,"StationServices_Localised":["x"],"StationGovernment_Localised":"y"}""");

        var text = Text(envelope);

        foreach (var removed in new[] { "Wanted", "ActiveFine", "CockpitBreach", "_Localised" })
        {
            Assert.DoesNotContain(removed, text, StringComparison.Ordinal);
        }

        Assert.Equal("Abraham Lincoln", (string?)envelope!.Message["StationName"]);
        Assert.Equal(3, envelope.Message["StarPos"]!.AsArray().Count);
    }

    [Fact]
    public void AScanInTheLastJumpsSystemGainsStarPos()
    {
        var composer = Composer();
        Compose(composer, Jump);

        var envelope = Compose(
            composer,
            """{"timestamp":"2026-10-20T10:06:00Z","event":"Scan","BodyName":"Sol A","BodyID":1,"SystemAddress":10477373803,"PlanetClass_Localised":"x"}""");

        Assert.NotNull(envelope);
        Assert.Equal(3, envelope.Message["StarPos"]!.AsArray().Count);
        Assert.Equal("Sol", (string?)envelope.Message["StarSystem"]);
        Assert.DoesNotContain("_Localised", Text(envelope), StringComparison.Ordinal);
    }

    [Fact]
    public void AScanInAnotherSystemBuildsNothing()
    {
        var composer = Composer();
        Compose(composer, Jump);

        Assert.Null(Compose(composer, """{"timestamp":"2026-10-20T10:06:00Z","event":"Scan","BodyName":"Alpha","BodyID":1,"SystemAddress":99}"""));
        Assert.Null(Compose(composer, """{"timestamp":"2026-10-20T10:06:00Z","event":"Scan","BodyName":"Alpha","BodyID":1}"""));
    }

    [Fact]
    public void ASaaSignalsFoundGainsTheSystemNameAndPosition()
    {
        var composer = Composer();
        Compose(composer, Jump);

        var envelope = Compose(
            composer,
            """{"timestamp":"2026-10-20T10:07:00Z","event":"SAASignalsFound","BodyName":"Sol A 1","SystemAddress":10477373803,"BodyID":3,"Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2}]}""");

        Assert.Equal("Sol", (string?)envelope!.Message["StarSystem"]);
        Assert.Equal(3, envelope.Message["StarPos"]!.AsArray().Count);
        Assert.DoesNotContain("_Localised", Text(envelope), StringComparison.Ordinal);
        Assert.Null(Compose(composer, """{"timestamp":"2026-10-20T10:07:00Z","event":"SAASignalsFound","BodyName":"B","SystemAddress":99,"BodyID":3}"""));
    }

    [Fact]
    public void AScanBeforeAnyLocationBuildsNothing()
    {
        Assert.Null(Compose(Composer(), """{"timestamp":"2026-10-20T10:06:00Z","event":"Scan","BodyName":"Alpha","BodyID":1,"SystemAddress":10477373803}"""));
    }

    [Fact]
    public void AnUnknownHorizonsAndOdysseyAreLeftOut()
    {
        var envelope = Compose(Composer(horizons: false), Jump);

        Assert.NotNull(envelope);
        Assert.False(envelope.Message.ContainsKey("horizons"));
        Assert.False(envelope.Message.ContainsKey("odyssey"));
    }

    [Fact]
    public void AJumpWithoutAPositionBuildsNothing()
    {
        Assert.Null(Compose(Composer(), """{"timestamp":"2026-10-20T10:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803}"""));
    }
}
