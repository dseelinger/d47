using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class ScannerResultsBecomeEddnMessagesTests
{
    private const long Sol = 10477373803;

    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Preamble =
    [
        """{"timestamp":"2026-10-21T11:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r1"}""",
        """{"timestamp":"2026-10-21T11:00:01Z","event":"LoadGame","Horizons":true,"Odyssey":true,"gameversion":"4.1.0.100","build":"r1"}""",
        """{"timestamp":"2026-10-21T11:30:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[1,2,3]}""",
    ];

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EddnComposer Composer()
    {
        var composer = new EddnComposer(() => "anon", "1", () => null);

        foreach (var line in Preamble)
        {
            composer.Compose(Event(line));
        }

        return composer;
    }

    private static string Signal(long address, string name, string extra = "") =>
        $$"""{"timestamp":"2026-10-21T11:59:00Z","event":"FSSSignalDiscovered","SystemAddress":{{address}},"SignalName":"{{name}}","SignalName_Localised":"x"{{extra}}}""";

    [Theory]
    [InlineData("""{"timestamp":"2026-10-21T11:59:00Z","event":"FSSDiscoveryScan","Progress":0.5,"BodyCount":9,"NonBodyCount":3,"SystemName":"Sol","SystemAddress":10477373803}""", "fssdiscoveryscan", "SystemName", "Progress")]
    [InlineData("""{"timestamp":"2026-10-21T11:59:00Z","event":"FSSBodySignals","BodyName":"Sol 3","BodyID":3,"SystemAddress":10477373803,"Signals":[{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":2}]}""", "fssbodysignals", "StarSystem", "_Localised")]
    [InlineData("""{"timestamp":"2026-10-21T11:59:00Z","event":"FSSAllBodiesFound","SystemName":"Sol","SystemAddress":10477373803,"Count":9}""", "fssallbodiesfound", "SystemName", "_Localised")]
    [InlineData("""{"timestamp":"2026-10-21T11:59:00Z","event":"ScanBaryCentre","StarSystem":"Sol","SystemAddress":10477373803,"BodyID":1,"SemiMajorAxis":1.5,"Eccentricity":0.1,"OrbitalInclination":0,"Periapsis":0,"OrbitalPeriod":5,"AscendingNode":0,"MeanAnomaly":0}""", "scanbarycentre", "StarSystem", "_Localised")]
    public void ASingleScannerEventGivesItsSchemasMessage(string line, string schema, string required, string absent)
    {
        var envelope = Assert.Single(Composer().ComposeAll(Event(line), T0));

        Assert.Equal($"https://eddn.edcd.io/schemas/{schema}/1", envelope.SchemaRef);
        Assert.NotNull(envelope.Message[required]);
        Assert.Equal(Sol, (long?)envelope.Message["SystemAddress"]);
        Assert.Equal(3, envelope.Message["StarPos"]!.AsArray().Count);
        Assert.True((bool?)envelope.Message["horizons"]);
        Assert.True((bool?)envelope.Message["odyssey"]);
        Assert.DoesNotContain("_Localised", envelope.Message.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain(absent == "_Localised" ? "_Localised" : "\"" + absent + "\"", envelope.Message.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AScannerEventFromAnotherSystemGivesNothing()
    {
        var line = """{"timestamp":"2026-10-21T11:59:00Z","event":"FSSAllBodiesFound","SystemName":"Sol","SystemAddress":999,"Count":9}""";

        Assert.Empty(Composer().ComposeAll(Event(line), T0));
    }

    [Fact]
    public void ARunOfSignalsIsOneMessageSentWhenAnotherEventEndsIt()
    {
        var composer = Composer();

        Assert.Empty(composer.ComposeAll(Event(Signal(Sol, "A")), T0));
        Assert.Empty(composer.ComposeAll(Event(Signal(Sol, "B", ",\"TimeRemaining\":300,\"USSType\":\"$USS_Type_Salvage;\"")), T0));
        Assert.Empty(composer.ComposeAll(Event(Signal(Sol, "Mission", ",\"USSType\":\"$USS_Type_MissionTarget;\"")), T0));

        var messages = composer.ComposeAll(Event("""{"timestamp":"2026-10-21T11:59:05Z","event":"Music","MusicTrack":"x"}"""), T0);
        var envelope = Assert.Single(messages);
        var signals = envelope.Message["signals"]!.AsArray();

        Assert.Equal("https://eddn.edcd.io/schemas/fsssignaldiscovered/1", envelope.SchemaRef);
        Assert.Equal(2, signals.Count);
        Assert.Equal("Sol", (string?)envelope.Message["StarSystem"]);
        Assert.Equal("2026-10-21T11:59:00Z", (string?)envelope.Message["timestamp"]);

        var text = envelope.Message.ToJsonString();

        foreach (var absent in new[] { "TimeRemaining", "Mission", "_Localised", "\"event\":\"FSSSignalDiscovered\",\"SignalName\"" })
        {
            Assert.DoesNotContain(absent, text, StringComparison.Ordinal);
        }

        Assert.Null(signals[0]!["SystemAddress"]);
    }

    [Fact]
    public void ARunStaysOpenAcrossAnEmptyTickAndClosesTenSecondsAfterItsLastSignal()
    {
        var composer = Composer();

        composer.ComposeAll(Event(Signal(Sol, "A")), T0);

        Assert.Null(composer.Flush(T0.AddSeconds(9)));

        composer.ComposeAll(Event(Signal(Sol, "B")), T0.AddSeconds(9));

        Assert.Null(composer.Flush(T0.AddSeconds(18)));

        var envelope = composer.Flush(T0.AddSeconds(19));

        Assert.Equal(2, envelope!.Message["signals"]!.AsArray().Count);
        Assert.Null(composer.Flush(T0.AddSeconds(40)));
    }

    [Fact]
    public void SignalsFoundBeforeTheJumpAreSentForTheSystemTheyWereFoundIn()
    {
        var composer = Composer();
        const long other = 555;

        composer.ComposeAll(Event(Signal(other, "Arriving")), T0);

        var messages = composer.ComposeAll(
            Event("""{"timestamp":"2026-10-21T11:59:10Z","event":"FSDJump","StarSystem":"Other","SystemAddress":555,"StarPos":[9,9,9]}"""),
            T0);

        var run = messages[0];

        Assert.Equal("https://eddn.edcd.io/schemas/fsssignaldiscovered/1", run.SchemaRef);
        Assert.Equal(other, (long?)run.Message["SystemAddress"]);
        Assert.Equal("Other", (string?)run.Message["StarSystem"]);
    }

    [Fact]
    public void SignalsFoundBeforeALeavingJumpAreNotSentForTheNewSystem()
    {
        var composer = Composer();

        composer.ComposeAll(Event(Signal(Sol, "Leaving")), T0);

        var messages = composer.ComposeAll(
            Event("""{"timestamp":"2026-10-21T11:59:10Z","event":"FSDJump","StarSystem":"Other","SystemAddress":555,"StarPos":[9,9,9]}"""),
            T0);

        Assert.Equal(Sol, (long?)messages[0].Message["SystemAddress"]);
        Assert.Equal("Sol", (string?)messages[0].Message["StarSystem"]);
    }

    [Fact]
    public void ARunWhoseSignalsMatchNoLocationGivesNothing()
    {
        var composer = Composer();

        composer.ComposeAll(Event(Signal(777, "Stray")), T0);

        Assert.Null(composer.Flush(T0.AddSeconds(11)));
    }
}
