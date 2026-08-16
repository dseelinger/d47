using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Finding somewhere to colonise (list.md Phase 18, "Find somewhere worth colonising").
/// <para>
/// Two things are being defended here and they pull in opposite directions. The index cannot
/// filter on the one criterion Frontier's rule turns on, so the deciding happens over the
/// response and has to be right; and the answer must never let a Commander believe they have been
/// told a system is free, so the sentence saying otherwise is asserted rather than assumed.
/// </para>
/// </summary>
public class ColonisationCandidateTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public ColonisationQuery? LastScan { get; private set; }

        public BodyQuery? LastBodyQuery { get; private set; }

        public ColonisationScan Scan { get; set; } = new("Sol", 3, []);

        public BodySearchResult Bodies { get; set; } = new("Sol", 0, []);

        public Exception? ScanThrows { get; set; }

        public Exception? BodiesThrow { get; set; }

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken)
        {
            LastScan = query;

            return ScanThrows is not null
                ? Task.FromException<ColonisationScan>(ScanThrows)
                : Task.FromResult(Scan);
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            LastBodyQuery = query;

            return BodiesThrow is not null
                ? Task.FromException<BodySearchResult>(BodiesThrow)
                : Task.FromResult(Bodies);
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Sol", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(0);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("Sol", 0, []));
    }

    /// <summary>
    /// A system the index knows something about. Planets default to a plausible handful, because a
    /// system with none is a distinct case with its own meaning — see <see cref="Unsurveyed"/>.
    /// </summary>
    private static ColonisationSystem System(
        string name,
        double distance,
        long population = 0,
        int bodies = 10,
        bool beingColonised = false,
        bool colonised = false,
        int terraformable = 0,
        params (string Subtype, int Count)[] planets) => new()
        {
            Name = name,
            Distance = distance,
            Population = population,
            BodyCount = bodies,
            BeingColonised = beingColonised,
            Colonised = colonised,
            Terraformable = terraformable,
            Planets = planets.Length > 0 ? planets : [("Rocky body", 4)],
            NearestBody = 120,
            FurthestBody = 4_400,
        };

    /// <summary>A star and nothing else, which is what nobody having looked reads as.</summary>
    private static ColonisationSystem Unsurveyed(string name, double distance) => new()
    {
        Name = name,
        Distance = distance,
        BodyCount = 1,
    };

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy) Build(
        TempInstall install,
        bool enabled = true)
    {
        var galaxy = new FakeGalaxy();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, enabled ? "true" : "false", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
            [ColonisationCapability.Create(() => null, galaxy, settings)]);

        return (registry, galaxy);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    private static Task<ToolResult> Find(CapabilityRegistry registry, params (string Name, string Value)[] values) =>
        registry.InvokeAsync("find_colonisation_candidates", Args(values), TestContext.Current.CancellationToken);

    /// <summary>
    /// The load-bearing one. Everything else here is about being useful; this is about not being
    /// believed for something that cannot be known.
    /// </summary>
    [Fact]
    public async Task NoAnswerEverSuggestsASystemIsFreeToClaim()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan("Sol", 40, [System("Candidate", 6.1)]);

        var found = await Find(registry, ("near", "Sol"));
        var none = await Find(registry, ("near", "Sol"), ("body_type", "Earth-like world"));

        foreach (var answer in new[] { found, none })
        {
            Assert.Contains("None of this says a system is free", answer.Content, StringComparison.Ordinal);
            Assert.Contains("System Colonisation Contact", answer.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("unclaimed", answer.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("available", answer.Content, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The claim rule, applied here because the index will not apply it: its population filter is
    /// silently dropped, so a populated system that reaches this code has to be dropped by this
    /// code or it is reported as somewhere to colonise.
    /// </summary>
    [Fact]
    public async Task APopulatedSystemIsNeverACandidateHoweverWellItMatches()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan(
            "Sol",
            2,
            [System("Busy", 1.0, population: 4_000_000), System("Empty", 9.0)]);

        var result = await Find(registry, ("near", "Sol"));

        Assert.DoesNotContain("Busy", result.Content, StringComparison.Ordinal);
        Assert.Contains("Empty", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A system nobody lives in but somebody is already building in. The flag is one-way — absent
    /// on 79 of 84 unpopulated systems measured — so true is worth acting on and false says
    /// nothing, which is why it excludes and never endorses.
    /// </summary>
    [Fact]
    public async Task ASystemSomebodyIsAlreadyBuildingInIsLeftOutAndCounted()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan(
            "Sol",
            3,
            [System("Taken", 2.0, beingColonised: true), System("Free", 8.0)]);

        var result = await Find(registry, ("near", "Sol"));

        Assert.DoesNotContain("Taken", result.Content, StringComparison.Ordinal);
        Assert.Contains("already begun building", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A star and no planets is what an unsurveyed system looks like in a crowd-fed index — a fifth
    /// of the neighbourhood on a real frontier reference. Recommending one would report nobody
    /// having looked as a system with nothing in it, and dropping it silently would hide the very
    /// systems a Commander out there might want to go and honk.
    /// </summary>
    [Fact]
    public async Task ASystemNobodyHasSurveyedIsCountedRatherThanRecommendedOrHidden()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan(
            "Sol",
            2,
            [Unsurveyed("Unknown", 1.0), System("Known", 5.0)]);

        var result = await Find(registry, ("near", "Sol"));

        Assert.DoesNotContain("Unknown", result.Content, StringComparison.Ordinal);
        Assert.Contains("Known", result.Content, StringComparison.Ordinal);
        Assert.Contains("nobody has surveyed", result.Content, StringComparison.Ordinal);
        Assert.Contains("not the same as empty", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheObjectiveNarrowsOnWhatTheSystemActuallyHolds()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan(
            "Sol",
            2,
            [
                System("Rocks", 1.0, planets: [("Rocky body", 8)]),
                System("Garden", 7.0, terraformable: 1, planets: [("Water world", 1), ("Rocky body", 3)]),
            ]);

        var wanted = await Find(registry, ("near", "Sol"), ("body_type", "water world"));
        var terraformable = await Find(registry, ("near", "Sol"), ("terraformable", "true"));

        Assert.Contains("Garden", wanted.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Rocks", wanted.Content, StringComparison.Ordinal);
        Assert.Contains("Garden", terraformable.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Rocks", terraformable.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABodyTypeTheCatalogueDoesNotKnowIsRefusedBeforeAnythingIsAsked()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await Find(registry, ("near", "Sol"), ("body_type", "Ringworld"));

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastScan);
    }

    /// <summary>
    /// Landability and rings are the two things the scan cannot see — the embedded body records
    /// carry neither — so asking for them has to reach the body search, by name and never by a
    /// zero-width distance range.
    /// </summary>
    [Fact]
    public async Task LandableAndRingsAreDecidedByTheSecondCallAndAskedForByName()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan("Sol", 2, [System("Flat", 1.0), System("Rocky", 4.0)]);

        galaxy.Bodies = new BodySearchResult(
            "Sol",
            3,
            [
                new BodySummary { Name = "Flat 1", SystemName = "Flat", IsLandable = false },
                new BodySummary { Name = "Rocky 1", SystemName = "Rocky", IsLandable = true },
                new BodySummary
                {
                    Name = "Rocky 2",
                    SystemName = "Rocky",
                    IsLandable = true,
                    ReserveLevel = "Pristine",
                    Rings = [new RingSummary("Rocky 2 A Ring", "Metallic")],
                },
            ]);

        var result = await Find(registry, ("near", "Sol"), ("landable", "2"), ("rings", "true"));

        Assert.Contains("Rocky", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Flat", result.Content, StringComparison.Ordinal);
        Assert.Contains("Metallic", result.Content, StringComparison.Ordinal);

        Assert.NotNull(galaxy.LastBodyQuery);
        Assert.Equal(["Flat", "Rocky"], galaxy.LastBodyQuery!.SystemNames);
    }

    /// <summary>
    /// The scan already answered. Losing the second call costs two figures, and throwing the whole
    /// answer away for them would discard work that succeeded.
    /// </summary>
    [Fact]
    public async Task LosingTheSecondCallDegradesTheAnswerRatherThanRefusingIt()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan("Sol", 2, [System("Somewhere", 3.0)]);
        galaxy.BodiesThrow = new GalaxyUnavailableException("The body search took too long to answer.");

        var result = await Find(registry, ("near", "Sol"));

        Assert.False(result.IsError);
        Assert.Contains("Somewhere", result.Content, StringComparison.Ordinal);
        Assert.Contains("no landable or ring figures", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableIndexIsASentenceRatherThanAnException()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.ScanThrows = new GalaxyUnavailableException("The galaxy search took too long to answer.");

        var result = await Find(registry, ("near", "Sol"));

        Assert.True(result.IsError);
        Assert.Contains("took too long", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchingFromAnUnpopulatedSystemSaysSoBecauseThereIsNoContactThere()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan(
            "HIP 22460",
            2,
            [System("HIP 22460", 0.0), System("HIP 22711", 13.5)]);

        var result = await Find(registry, ("near", "HIP 22460"));

        Assert.Contains("no System Colonisation Contact there", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRangeIsCappedAtWhatAClaimActuallyReaches()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        await Find(registry, ("near", "Sol"), ("max_distance", "80"));

        Assert.Equal(ColonisationQuery.ClaimRange, galaxy.LastScan!.MaxDistance);
    }

    [Fact]
    public async Task TheGalaxySwitchGovernsThisTheSameAsEveryOtherQuestionThatLeavesTheMachine()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install, enabled: false);

        var result = await Find(registry, ("near", "Sol"));

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastScan);
    }

    /// <summary>
    /// The naive rule produces "18 Rocky bodys" and "4 Gas giant with water-based lifes", which is
    /// the kind of wrongness a Commander hears rather than reads.
    /// </summary>
    [Theory]
    [InlineData("Rocky body", 18, "18 Rocky bodies")]
    [InlineData("High metal content world", 4, "4 High metal content worlds")]
    [InlineData("Class I gas giant", 2, "2 Class I gas giants")]
    [InlineData("Gas giant with water-based life", 3, "3 Gas giants with water-based life")]
    [InlineData("Earth-like world", 1, "1 Earth-like world")]
    public async Task BodyKindsArePluralisedByTheirHeadNoun(string subtype, int count, string expected)
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan("Sol", 1, [System("Somewhere", 3.0, planets: [(subtype, count)])]);

        var result = await Find(registry, ("near", "Sol"));

        Assert.Contains(expected, result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryNeighbourBeingPopulatedIsAnAnswerRatherThanAnEmptyList()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan("Deciat", 22, [System("Deciat", 0.0, population: 3_000)]);

        var result = await Find(registry, ("near", "Deciat"));

        Assert.Contains("already populated", result.Content, StringComparison.Ordinal);
        Assert.Contains("None of this says a system is free", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Having nothing to show is three different situations, and telling a Commander to loosen an
    /// objective they never set is advice about a filter that does not exist.
    /// </summary>
    [Fact]
    public async Task NothingToShowIsExplainedByWhatActuallyEmptiedTheList()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Scan = new ColonisationScan("Sol", 9, [Unsurveyed("Nobody Went", 2.0)]);

        var noObjective = await Find(registry, ("near", "Sol"));

        Assert.DoesNotContain("wider objective", noObjective.Content, StringComparison.Ordinal);
        Assert.Contains("nobody has surveyed", noObjective.Content, StringComparison.Ordinal);

        galaxy.Scan = new ColonisationScan("Sol", 9, [System("Rocks", 2.0, planets: [("Rocky body", 5)])]);

        var withObjective = await Find(registry, ("near", "Sol"), ("body_type", "Earth-like world"));

        Assert.Contains("wider objective", withObjective.Content, StringComparison.Ordinal);
    }
}
