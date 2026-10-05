using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Mining;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A ring mapped with the DSS says its hotspots, the mining target first (#608).</summary>
public class AMappedRingSaysItsHotspotsTests
{
    private const string Ring = """{ "timestamp":"2025-07-10T10:46:16Z", "event":"SAASignalsFound", "BodyName":"Omicron Capricorni B B 1 A Ring", "SystemAddress":220354045116, "BodyID":48, "Signals":[ { "Type":"Serendibite", "Count":3 }, { "Type":"Rhodplumsite", "Count":1 }, { "Type":"Monazite", "Count":6 }, { "Type":"Platinum", "Count":4 }, { "Type":"Painite", "Count":2 } ], "Genuses":[  ] }""";

    private const string Planet = """{ "timestamp":"2025-07-10T10:47:00Z", "event":"SAASignalsFound", "BodyName":"Omicron Capricorni B B 1", "SystemAddress":220354045116, "BodyID":47, "Signals":[ { "Type":"$SAA_SignalType_Geological;", "Type_Localised":"Geological", "Count":2 } ], "Genuses":[  ] }""";

    private static CalloutContext Context(bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(line =>
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                return parsed!;
            })]);

    private static RingHotspotsCallout Targeting(string? material) =>
        new() { Target = () => material is null ? null : new MiningTarget(material, null) };

    [Fact]
    public void ARingSaysItsThreeBiggestAndCountsTheRest() =>
        Assert.Equal(
            "A Ring: 6 Monazite, 4 Platinum, 3 Serendibite, and 2 more.",
            Assert.Single(new RingHotspotsCallout().Examine(Context(false, Ring))).Text);

    [Fact]
    public void TheTargetLeadsWhenTheRingHasIt() =>
        Assert.Equal(
            "A Ring: 4 Platinum. Also 6 Monazite, 3 Serendibite, and 2 more.",
            Assert.Single(Targeting("Platinum").Examine(Context(false, Ring))).Text);

    [Fact]
    public void ARingWithoutTheTargetSaysSo() =>
        Assert.Equal(
            "A Ring: no Void Opal. 6 Monazite, 4 Platinum, 3 Serendibite, and 2 more.",
            Assert.Single(Targeting("Void Opal").Examine(Context(false, Ring))).Text);

    [Fact]
    public void APlanetSaysNothing() =>
        Assert.Empty(new RingHotspotsCallout().Examine(Context(false, Planet)));

    [Fact]
    public void TheSameRingMappedTwiceIsSaidOnce()
    {
        var callout = new RingHotspotsCallout();

        Assert.Single(callout.Examine(Context(false, Ring)));
        Assert.Empty(callout.Examine(Context(false, Ring)));
    }

    [Fact]
    public void PrimingSaysNothing() =>
        Assert.Empty(new RingHotspotsCallout().Examine(Context(true, Ring)));

    [Fact]
    public void TheMappingCalloutStaysSilentOnTheRingLine() =>
        Assert.Empty(new MappingCallout().Examine(Context(false, Ring)));

    [Fact]
    public void TheRowIsOnByDefaultAndSwitchesTheCalloutOff()
    {
        var settings = new D47.Core.Configuration.D47Settings();

        Assert.True(settings.Callouts.RingHotspots);
        Assert.Equal("ring-hotspots", new RingHotspotsCallout().Id);
    }
}
