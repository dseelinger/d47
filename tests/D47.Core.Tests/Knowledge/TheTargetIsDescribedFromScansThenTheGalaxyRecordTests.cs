using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary><c>get_target</c> describes the navigation panel's selection, the Commander's own scans first (#2286).</summary>
public class TheTargetIsDescribedFromScansThenTheGalaxyRecordTests
{
    private const long Fixture = 633608311522;

    private const int PlanetId = 12;

    private const string Planet = "Fixture 3 a";

    private static readonly BodyProfile RecordedPlanet = new()
    {
        BodyId = PlanetId,
        Name = Planet,
        Type = "Planet",
        SubType = "Rocky body",
        Gravity = 0.9,
        IsLandable = true,
        Atmosphere = "No atmosphere",
        DistanceToArrival = 1234,
        Signals = new Dictionary<string, int> { ["$SAA_SignalType_Geological;"] = 2 },
    };

    private static readonly StationProfile Base = new()
    {
        Name = "Fixture Base",
        Kind = StationKind.Settlement,
        Type = "Settlement",
        Body = Planet,
        ControllingFaction = "Fixture Party",
        LargestPad = PadSize.Medium,
    };

    private static readonly StationProfile Port = new()
    {
        Name = "Fixture Port",
        Kind = StationKind.Starport,
        Type = "Coriolis Starport",
        Body = "Fixture 3",
        ControllingFaction = "Fixture Party",
        Government = "Democracy",
        PrimaryEconomy = "Industrial",
        LargestPad = PadSize.Large,
        Services = ["Market", "Outfitting"],
    };

    private static readonly StationProfile Carrier = new()
    {
        Name = "XFX-07Q",
        Kind = StationKind.FleetCarrier,
        Type = "Drake-Class Carrier",
        LargestPad = PadSize.Large,
    };

    private static StarSystemProfile Record(params BodyProfile[] bodies) => new()
    {
        Name = "Fixture",
        SystemAddress = Fixture,
        Government = "Democracy",
        Stations = [Base, Port, Carrier],
        Bodies = bodies,
    };

    private static JournalEvent Parse(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(json);

    private static BodyScans Scanned() => BodyScans.Empty.Apply(Parse(
        $$"""
        {"timestamp":"2026-11-10T10:00:00Z","event":"Scan","BodyName":"{{Planet}}","BodyID":{{PlanetId}},"SystemAddress":{{Fixture}},"PlanetClass":"High metal content body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"","SurfaceGravity":5.512481,"SurfaceTemperature":180.4,"SurfacePressure":1520.3,"Landable":true,"Radius":2045000.0,"TerraformState":"","Materials":[{"Name":"iron","Percent":21.3},{"Name":"nickel","Percent":16.1}]}
        """));

    private static BodySignals Signals() => BodySignals.Empty.Apply(Parse(
        $$"""
        {"timestamp":"2026-11-10T10:01:00Z","event":"FSSBodySignals","BodyName":"{{Planet}}","BodyID":{{PlanetId}},"SystemAddress":{{Fixture}},"Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":3}]}
        """));

    private static string Describe(StatusDestination? destination, BodyScans scans, BodySignals signals, StarSystemProfile? record) =>
        TargetAnswer.Describe(NavTarget.Resolve(destination, scans, signals, record));

    private static string Part(string answer, string heading)
    {
        var start = answer.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, answer);
        var end = answer.IndexOf(". From ", start + heading.Length, StringComparison.Ordinal);
        return end < 0 ? answer[start..] : answer[start..end];
    }

    [Fact]
    public void AScannedPlanetIsNamedWithItsGravityLandingAtmosphereAndSignals()
    {
        var answer = Describe(new StatusDestination(Fixture, PlanetId, Planet), Scanned(), Signals(), null);
        var scanned = Part(answer, "From your own scans:");

        Assert.StartsWith("Your target is Fixture 3 a.", answer, StringComparison.Ordinal);
        Assert.Contains("gravity 0.56 g", scanned, StringComparison.Ordinal);
        Assert.Contains("landable", scanned, StringComparison.Ordinal);
        Assert.Contains("thin carbon dioxide atmosphere", scanned, StringComparison.Ordinal);
        Assert.Contains("signals: 3 Biological", scanned, StringComparison.Ordinal);
        Assert.Contains("surface pressure 0.02 atm", scanned, StringComparison.Ordinal);
        Assert.Contains("radius 2,045 km", scanned, StringComparison.Ordinal);
        Assert.Contains("materials: Iron 21.3%, Nickel 16.1%", scanned, StringComparison.Ordinal);
    }

    [Fact]
    public void AScanGravityInMetresPerSecondSquaredIsGivenInG() =>
        Assert.Equal("0.56 g", TargetAnswer.GravityFromScan(5.512481));

    [Fact]
    public void AnUnscannedPlanetIsAnsweredFromTheGalaxyRecordAndSaysSo()
    {
        var answer = Describe(new StatusDestination(Fixture, PlanetId, Planet), BodyScans.Empty, BodySignals.Empty, Record(RecordedPlanet with { Gravity = 0.56 }));
        var recorded = Part(answer, "From the galaxy record:");

        Assert.DoesNotContain("From your own scans", answer, StringComparison.Ordinal);
        Assert.Contains("gravity 0.56 g", recorded, StringComparison.Ordinal);
        Assert.Contains("Rocky body", recorded, StringComparison.Ordinal);
        Assert.Contains("1,234 Ls from arrival", recorded, StringComparison.Ordinal);
        Assert.Contains("signals: 2 Geological", recorded, StringComparison.Ordinal);
        Assert.Contains("on it: Fixture Base (Settlement)", recorded, StringComparison.Ordinal);
    }

    [Fact]
    public void AScannedFieldOutranksTheGalaxyRecord()
    {
        var answer = Describe(new StatusDestination(Fixture, PlanetId, Planet), Scanned(), Signals(), Record(RecordedPlanet));
        var scanned = Part(answer, "From your own scans:");
        var recorded = Part(answer, "From the galaxy record:");

        Assert.Contains("gravity 0.56 g", scanned, StringComparison.Ordinal);
        Assert.Contains("High metal content body", scanned, StringComparison.Ordinal);
        Assert.Contains("signals: 3 Biological", scanned, StringComparison.Ordinal);
        Assert.DoesNotContain("0.90 g", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Rocky body", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Geological", answer, StringComparison.Ordinal);
        Assert.Contains("1,234 Ls from arrival", recorded, StringComparison.Ordinal);
    }

    [Fact]
    public void AStationIsMatchedByNameWithItsTypeBodyAndFaction()
    {
        var answer = Describe(new StatusDestination(Fixture, 40, "fixture port"), BodyScans.Empty, BodySignals.Empty, Record(RecordedPlanet));

        Assert.Equal(
            "Your target is Fixture Port. From the galaxy record: Coriolis Starport; on Fixture 3; controlled by "
            + "Fixture Party; Democracy government; Industrial economy; largest pad Large; services: Market, Outfitting.",
            answer);
    }

    [Fact]
    public void ACarrierSelectedUnderItsNameIsMatchedByItsCallsign() =>
        Assert.StartsWith(
            "Your target is XFX-07Q. From the galaxy record: Drake-Class Carrier;",
            Describe(new StatusDestination(Fixture, 42, "FIXTURE TENDER XFX-07Q"), BodyScans.Empty, BodySignals.Empty, Record()),
            StringComparison.Ordinal);

    [Fact]
    public void ANameWithNoRecordIsSaidWithNoRecord() =>
        Assert.Equal(
            "Fixture Hold is targeted, but there is no record of it.",
            Describe(new StatusDestination(Fixture, 41, "Fixture Hold"), BodyScans.Empty, BodySignals.Empty, Record(RecordedPlanet)));

    [Fact]
    public void ADollarNameIsNeverSpoken()
    {
        var answer = Describe(new StatusDestination(Fixture, 41, "$EXT_PANEL_ColonisationShip;"), BodyScans.Empty, BodySignals.Empty, Record());

        Assert.Equal(TargetAnswer.NothingTargeted, answer);
    }

    [Fact]
    public async Task NothingSelectedIsNothingTargeted()
    {
        var result = await CallAsync(new GameStatus(), new FakeSystems());

        Assert.Equal("Nothing is targeted.", result.Content);
    }

    [Fact]
    public async Task BodyZeroIsTheSystemsDescription()
    {
        var systems = new FakeSystems();
        systems.Profiles[Fixture] = Record(RecordedPlanet);

        var target = await CallAsync(new GameStatus { Destination = new StatusDestination(Fixture, 0, "Fixture") }, systems);
        var described = await Tool(systems, new GameStatus(), "describe_system")
            .Handler(new ToolArguments(new Dictionary<string, string> { ["system"] = "Fixture" }), TestContext.Current.CancellationToken);

        Assert.False(target.IsError);
        Assert.Equal(described.Content, target.Content);
        Assert.StartsWith("Fixture: Democracy.", target.Content, StringComparison.Ordinal);
    }

    private static Task<ToolResult> CallAsync(GameStatus status, FakeSystems systems) =>
        Tool(systems, status, GalaxyCapability.TargetTool)
            .Handler(new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

    private static ToolDefinition Tool(FakeSystems systems, GameStatus status, string name)
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        return GalaxyCapability.Create(null, () => "Sol", settings, systems: systems, gameStatus: () => status)
            .Tools.Single(tool => tool.Name == name);
    }

    private sealed class FakeSystems : IStarSystemService
    {
        public Dictionary<long, StarSystemProfile> Profiles { get; } = [];

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult(Profiles.GetValueOrDefault(systemAddress));

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                [.. Profiles.Values
                    .Where(profile => string.Equals(profile.Name, typed, StringComparison.OrdinalIgnoreCase))
                    .Select(profile => new SystemNameMatch(profile.Name, profile.SystemAddress, new StarPosition(0, 0, 0)))]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
