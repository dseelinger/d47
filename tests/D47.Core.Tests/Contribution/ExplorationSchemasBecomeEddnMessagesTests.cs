using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class ExplorationSchemasBecomeEddnMessagesTests
{
    private const long Sol = 10477373803;

    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private const string RouteStamp = "2026-10-21T11:59:00Z";

    private static readonly string[] Preamble =
    [
        """{"timestamp":"2026-10-21T11:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r1"}""",
        """{"timestamp":"2026-10-21T11:00:01Z","event":"LoadGame","Horizons":true,"Odyssey":false,"gameversion":"4.1.0.100","build":"r1"}""",
        """{"timestamp":"2026-10-21T11:30:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[1,2,3]}""",
    ];

    private const string Codex =
        """{"timestamp":"2026-10-21T11:59:00Z","event":"CodexEntry","EntryID":1100101,"Name":"$Codex_Ent_Cactoid_01_Name;","Name_Localised":"Cactoida","SubCategory":"$Codex_SubCategory_Organic_Structures;","SubCategory_Localised":"Organic structures","Category":"$Codex_Category_Biology;","Category_Localised":"Biological and Geological","Region":"$Codex_RegionName_18;","Region_Localised":"Inner Orion Spur","System":"Sol","SystemAddress":10477373803,"VoucherAmount":2500,"IsNewEntry":true,"NewTraitsDiscovered":true,"Traits":["Hardy"]}""";

    private const string Settlement =
        """{"timestamp":"2026-10-21T11:59:00Z","event":"ApproachSettlement","Name":"Dockside","Name_Localised":"Dockside","MarketID":128000001,"StationGovernment":"$government_Democracy;","StationGovernment_Localised":"Democracy","StationAllegiance":"Federation","StationEconomies":[{"Name":"$economy_Industrial;","Name_Localised":"Industrial","Proportion":1.0}],"StationFaction":{"Name":"Sol Labour","FactionState":"Boom"},"StationServices":["dock"],"StationEconomy":"$economy_Industrial;","StationEconomy_Localised":"Industrial","SystemAddress":10477373803,"BodyID":5,"BodyName":"Sol 3","Latitude":10.5,"Longitude":-20.25}""";

    private const string Beacon =
        """{"timestamp":"2026-10-21T11:59:00Z","event":"NavBeaconScan","SystemAddress":10477373803,"NumBodies":9}""";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EddnComposer Composer(string? route = null, string? statusBody = null)
    {
        var composer = new EddnComposer(() => "anon", "1", () => null, readNavRoute: () => route, statusBodyName: () => statusBody);

        foreach (var line in Preamble)
        {
            composer.Compose(Event(line));
        }

        return composer;
    }

    private static string Route(string stamp, string stops) =>
        $$"""{"timestamp":"{{stamp}}","event":"NavRoute","Route":[{{stops}}]}""";

    private const string Stop =
        """{"StarSystem":"Alpha Centauri","SystemAddress":1,"StarPos":[3.03,-0.09,3.16],"StarClass":"G"}""";

    private const string RouteLine = """{"timestamp":"2026-10-21T11:59:00Z","event":"NavRoute"}""";

    [Theory]
    [InlineData(Codex, "codexentry", "System", "IsNewEntry")]
    [InlineData(Settlement, "approachsettlement", "StarSystem", "Name_Localised")]
    [InlineData(Beacon, "navbeaconscan", "StarSystem", "Progress")]
    public void AJournalLineGivesItsSchemasMessage(string line, string schema, string required, string absent)
    {
        var envelope = Assert.Single(Composer().ComposeAll(Event(line), T0));

        Assert.Equal($"https://eddn.edcd.io/schemas/{schema}/1", envelope.SchemaRef);
        Assert.NotNull(envelope.Message[required]);
        Assert.Equal(Sol, (long?)envelope.Message["SystemAddress"]);
        Assert.Equal(3, envelope.Message["StarPos"]!.AsArray().Count);
        Assert.True((bool?)envelope.Message["horizons"]);
        Assert.False((bool?)envelope.Message["odyssey"]);
        Assert.DoesNotContain("_Localised", envelope.Message.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"" + absent + "\"", envelope.Message.ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Codex)]
    [InlineData(Settlement)]
    [InlineData(Beacon)]
    public void ALineFromAnotherSystemGivesNothing(string line) =>
        Assert.Empty(Composer().ComposeAll(Event(line.Replace("10477373803", "42", StringComparison.Ordinal)), T0));

    [Fact]
    public void ACodexEntryKeepsTheKeysItsSchemaAllows()
    {
        var message = Assert.Single(Composer().ComposeAll(Event(Codex), T0)).Message;

        Assert.DoesNotContain("NewTraitsDiscovered", message.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(1100101, (long?)message["EntryID"]);
        Assert.Equal(2500, (long?)message["VoucherAmount"]);
        Assert.Null(message["BodyName"]);
        Assert.Null(message["BodyID"]);
    }

    [Fact]
    public void ACodexEntryOnABodyCarriesItsNameAndTheIdWhenTheJournalAgrees()
    {
        var composer = Composer(statusBody: "Sol 3");
        composer.Compose(Event("""{"timestamp":"2026-10-21T11:40:00Z","event":"ApproachBody","StarSystem":"Sol","SystemAddress":10477373803,"Body":"Sol 3","BodyName":"Sol 3","BodyID":5}"""));

        var message = Assert.Single(composer.ComposeAll(Event(Codex), T0)).Message;

        Assert.Equal("Sol 3", (string?)message["BodyName"]);
        Assert.Equal(5, (long?)message["BodyID"]);
    }

    [Fact]
    public void ACodexEntryOnAnotherBodyThanTheJournalNamedHasNoBodyId()
    {
        var composer = Composer(statusBody: "Sol 4");
        composer.Compose(Event("""{"timestamp":"2026-10-21T11:40:00Z","event":"ApproachBody","StarSystem":"Sol","SystemAddress":10477373803,"Body":"Sol 3","BodyName":"Sol 3","BodyID":5}"""));

        var message = Assert.Single(composer.ComposeAll(Event(Codex), T0)).Message;

        Assert.Equal("Sol 4", (string?)message["BodyName"]);
        Assert.Null(message["BodyID"]);
    }

    [Fact]
    public void ACodexEntryAfterLeavingABodyHasNoBodyId()
    {
        var composer = Composer(statusBody: "Sol 3");
        composer.Compose(Event("""{"timestamp":"2026-10-21T11:40:00Z","event":"ApproachBody","StarSystem":"Sol","SystemAddress":10477373803,"Body":"Sol 3","BodyName":"Sol 3","BodyID":5}"""));
        composer.Compose(Event("""{"timestamp":"2026-10-21T11:50:00Z","event":"LeaveBody","StarSystem":"Sol","SystemAddress":10477373803,"Body":"Sol 3","BodyID":5}"""));

        var message = Assert.Single(composer.ComposeAll(Event(Codex), T0)).Message;

        Assert.Null(message["BodyID"]);
    }

    [Fact]
    public void ASettlementKeepsOnlyTheFieldsItsSchemaAllows()
    {
        var line = Settlement.Replace("\"FactionState\":\"Boom\"", "\"FactionState\":\"Boom\",\"Happiness\":\"x\"", StringComparison.Ordinal);
        var message = Assert.Single(Composer().ComposeAll(Event(line), T0)).Message;

        Assert.DoesNotContain("Happiness", message.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal("Sol 3", (string?)message["BodyName"]);
        Assert.Equal(10.5, (double?)message["Latitude"]);
    }

    [Fact]
    public void ASettlementWithoutACoordinateGivesNothing()
    {
        var line = Settlement.Replace(",\"Latitude\":10.5", string.Empty, StringComparison.Ordinal);

        Assert.Empty(Composer().ComposeAll(Event(line), T0));
    }

    [Fact]
    public void ANavRouteFileWithTheEventsTimestampGivesTheRoute()
    {
        var envelope = Assert.Single(Composer(Route(RouteStamp, Stop + "," + Stop)).ComposeAll(Event(RouteLine), T0));

        Assert.Equal("https://eddn.edcd.io/schemas/navroute/1", envelope.SchemaRef);
        Assert.Equal(2, envelope.Message["Route"]!.AsArray().Count);
        Assert.Equal("G", (string?)envelope.Message["Route"]![0]!["StarClass"]);
        Assert.True((bool?)envelope.Message["horizons"]);
        Assert.False((bool?)envelope.Message["odyssey"]);
        Assert.DoesNotContain("_Localised", envelope.Message.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANavRouteFileWithAnotherTimestampGivesNothing() =>
        Assert.Empty(Composer(Route("2026-10-21T11:58:00Z", Stop)).ComposeAll(Event(RouteLine), T0));

    [Fact]
    public void AnEmptyNavRouteGivesNothing() =>
        Assert.Empty(Composer(Route(RouteStamp, string.Empty)).ComposeAll(Event(RouteLine), T0));

    [Fact]
    public void AnUnreadableNavRouteGivesNothing()
    {
        Assert.Empty(Composer().ComposeAll(Event(RouteLine), T0));
        Assert.Empty(Composer("{not json").ComposeAll(Event(RouteLine), T0));
    }

    [Fact]
    public void ARouteStopMissingAFieldGivesNothing() =>
        Assert.Empty(Composer(Route(RouteStamp, """{"StarSystem":"Alpha Centauri","SystemAddress":1,"StarPos":[3,0,3]}""")).ComposeAll(Event(RouteLine), T0));
}
