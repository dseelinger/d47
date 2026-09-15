using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The hand-picked farming sites (#243): the two tiers <c>find_material</c> gains, and
/// <c>get_material_farming_route</c>.
/// </summary>
public class MaterialFarmingSitesTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CapabilityRegistry Build(string? location = null, string? loadout = null)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        if (location is not null)
        {
            gameState.Apply(Event(location));
        }

        if (loadout is not null)
        {
            gameState.Apply(Event(loadout));
        }

        return CapabilityRegistry.Build([EngineeringCapability.Create(() => gameState.Active)]);
    }

    private static async Task<string> Ask(CapabilityRegistry registry, string tool, params (string Name, string Value)[] values)
    {
        var result = await registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        return result.Content;
    }

    private static string Loadout(double maxJumpRange) =>
        $$"""
        {"timestamp":"3311-01-01T00:02:00Z","event":"Loadout","Ship":"DiamondBackXL","ShipID":1,
         "ShipName":"Fixture","ShipIdent":"FX-01","HullValue":1000,"ModulesValue":2000,
         "MaxJumpRange":{{maxJumpRange.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
         "Modules":[]}
        """;

    // ---- find_material: no trade group -------------------------------------------------------

    [Fact]
    public async Task SensorFragmentNamesTheThargoidShipAndItsRelog()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Sensor Fragment"));

        Assert.Contains("HIP 17403 A 4 a", answer, StringComparison.Ordinal);
        Assert.Contains("-7.3362, -6.277", answer, StringComparison.Ordinal);
        Assert.Contains("a relog respawns it", answer, StringComparison.Ordinal);
    }

    // ---- find_material: raw -------------------------------------------------------------------

    [Fact]
    public async Task TelluriumNamesTheShardsSiteAndMentionsShardsAndBrainTreesGenerally()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Tellurium"));

        Assert.Contains("crystalline shards or brain trees", answer, StringComparison.Ordinal);
        Assert.Contains("HIP 36601 C 3 b", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGrade1RawNamesTheG4SiteAndTheOneForTwentySevenTrade()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Carbon"));

        Assert.Contains("Outotz LS-K d8-3 B 5 a", answer, StringComparison.Ordinal);
        Assert.Contains("trade 1 for 27 into Carbon", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeleniumCarriesNoJumpRangeWarning()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Selenium"));

        Assert.Contains("HR 3230 3 a a", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Diamondback Explorer", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PoloniumCarriesTheConfirmedRangeAndPrerequisites()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Polonium"));

        Assert.Contains("48.62", answer, StringComparison.Ordinal);
        Assert.Contains("45.74", answer, StringComparison.Ordinal);
        Assert.Contains("Diamondback Explorer", answer, StringComparison.Ordinal);
        Assert.Contains("FSD engineering", answer, StringComparison.Ordinal);
        Assert.Contains("FSD Injection", answer, StringComparison.Ordinal);
        Assert.Contains("Guardian FSD Booster", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShipAtOrAboveTheConfirmedRangeIsSaidToHaveMadeTheTrip()
    {
        var registry = Build(loadout: Loadout(48.62));

        var answer = await Ask(registry, "find_material", ("material", "Polonium"));

        Assert.Contains("has made this trip", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AShipBelowTheConfirmedRangeGetsItsOwnFigureAndNeverToldItCannotMakeIt()
    {
        var registry = Build(loadout: Loadout(30.0));

        var answer = await Ask(registry, "find_material", ("material", "Polonium"));

        Assert.Contains("30", answer, StringComparison.Ordinal);
        Assert.Contains("45.74", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot", answer, StringComparison.OrdinalIgnoreCase);
    }

    // ---- find_material: manufactured ----------------------------------------------------------

    [Fact]
    public async Task BiotechConductorsSaysNoEmissionCarriesItAndGivesTheSixForOneTrade()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Biotech Conductors"));

        Assert.Contains("no High Grade Emission carries", answer, StringComparison.Ordinal);
        Assert.Contains("trade 6 ×", answer, StringComparison.Ordinal);
        Assert.Contains("for 1 ×", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImperialShieldingStillPointsAtAHighGradeEmissionSearch()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Imperial Shielding"));

        Assert.Contains("a High Grade Emission of the right allegiance and state", answer, StringComparison.Ordinal);
    }

    // ---- find_material: encoded ----------------------------------------------------------------

    [Fact]
    public async Task ClassifiedScanFragmentNamesHip12099AndTheSixForOneTrade()
    {
        var registry = Build();

        var answer = await Ask(registry, "find_material", ("material", "Classified Scan Fragment"));

        Assert.Contains("HIP 12099 1 b", answer, StringComparison.Ordinal);
        Assert.Contains("Adaptive Encryptors Capture", answer, StringComparison.Ordinal);
        Assert.Contains("trade 6 ×", answer, StringComparison.Ordinal);
        Assert.Contains("for 1 ×", answer, StringComparison.Ordinal);
    }

    // ---- find_material: no galaxy service ------------------------------------------------------

    [Fact]
    public async Task TheSiteTiersAppearWithNoGalaxyService()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        var registry = CapabilityRegistry.Build([EngineeringCapability.Create(() => gameState.Active)]);

        var answer = await Ask(registry, "find_material", ("material", "Tellurium"));

        Assert.Contains("HIP 36601 C 3 b", answer, StringComparison.Ordinal);
    }

    // ---- get_material_farming_route ------------------------------------------------------------

    [Fact]
    public async Task TheRouteListsNearestFirstFromTheCommandersPosition()
    {
        // HIP 12099's StarPos is much closer to this point than HIP 17403's.
        var registry = Build(
            location: """{"timestamp":"3311-01-01T00:00:30Z","event":"Location","StarSystem":"Near HIP 12099","StarPos":[-100.0,-95.0,-165.0]}""");

        var answer = await Ask(registry, "get_material_farming_route");

        var hip12099 = answer.IndexOf("HIP 12099", StringComparison.Ordinal);
        var hr3230 = answer.IndexOf("HR 3230", StringComparison.Ordinal);

        Assert.True(hip12099 > 0 && hip12099 < hr3230, answer);
    }

    [Fact]
    public async Task TheRouteFallsBackToTheTablesOwnOrderWithNoPosition()
    {
        var registry = Build();

        var answer = await Ask(registry, "get_material_farming_route");

        var hip12099 = answer.IndexOf("HIP 12099", StringComparison.Ordinal);
        var hip17403 = answer.IndexOf("HIP 17403", StringComparison.Ordinal);

        Assert.True(hip12099 > 0 && hip12099 < hip17403, answer);
    }

    [Fact]
    public async Task TheRouteTypeNarrowsItToOneCategory()
    {
        var registry = Build();

        var answer = await Ask(registry, "get_material_farming_route", ("type", "Raw"));

        Assert.DoesNotContain("HIP 12099", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("HIP 17403", answer, StringComparison.Ordinal);
        Assert.Contains("HIP 36601", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRouteStepsAtTheDistantSystemsCarryTheJumpRangeAdvice()
    {
        var registry = Build();

        var answer = await Ask(registry, "get_material_farming_route", ("type", "Raw"));

        Assert.Contains("48.62", answer, StringComparison.Ordinal);
        Assert.Contains("Diamondback Explorer", answer, StringComparison.Ordinal);
    }

    // ---- coverage: every raw group has a real, top-graded fastest site --------------------------

    [Theory]
    [InlineData("raw-1")]
    [InlineData("raw-2")]
    [InlineData("raw-3")]
    [InlineData("raw-4")]
    [InlineData("raw-5")]
    [InlineData("raw-6")]
    [InlineData("raw-7")]
    public void EveryRawGroupHasAFastestSiteAtItsOwnTopGrade(string group)
    {
        var site = FarmingSites.FastestFor(group);

        Assert.NotNull(site);

        var material = MaterialCatalogue.Find(site!.MaterialSymbol);

        Assert.NotNull(material);
        Assert.Equal(group, material!.Line);

        var topGrade = MaterialCatalogue.All
            .Where(entry => entry.Line == group)
            .Max(entry => entry.Grade);

        Assert.Equal(topGrade, material.Grade);
    }

    [Fact]
    public void NoFastestSiteIsAnAlternate()
    {
        Assert.All(FarmingSites.All, site => Assert.True(!site.IsAlternate || site.TopsGroup is null));
    }
}
