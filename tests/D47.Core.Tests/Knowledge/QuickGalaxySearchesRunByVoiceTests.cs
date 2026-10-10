using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The fixed quick searches: phrases, baked arguments and the tools that run them (#812).</summary>
public class QuickGalaxySearchesRunByVoiceTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public int Searches { get; private set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Searches++;

            return Task.FromResult(new GalaxySearchResult("Sol", 0, []));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Searches++;

            return Task.FromResult(new StationSearchResult("Sol", 0, []));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static CapabilityRegistry Build(MemoryInstall install, FakeGalaxy galaxy, bool lookupsOn)
    {
        var settings = TestSurface.For(install).Settings;

        if (lookupsOn)
        {
            settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        }

        return CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "Sol", settings)]);
    }

    [Theory]
    [InlineData("interstellar factors search", "search_stations", "services", "Interstellar Factors")]
    [InlineData("nearest black market", "search_stations", "services", "Black Market")]
    [InlineData("run the anarchy outbreak search", "search_systems", "government", "Anarchy")]
    [InlineData("find the nearest raw material trader", "search_stations", "material_trader", "Raw")]
    public void ASpokenQuickSearchReachesItsToolWithItsArguments(string said, string tool, string key, string value)
    {
        var install = new MemoryInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry, QuickSearches.Phrases);

        var match = router.MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(GalaxyCapability.Id, match.CapabilityId);
        Assert.Equal(tool, match.ToolName);
        Assert.Equal(value, match.Arguments.Values[key]);
    }

    [Fact]
    public void AnarchyOutbreakSetsTheGovernmentAndTheState()
    {
        var search = Assert.Single(QuickSearches.All, candidate => candidate.Name == "anarchy outbreak");

        Assert.Equal(["government", "state"], search.Arguments.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Outbreak", search.Arguments["state"]);
    }

    [Fact]
    public void EveryQuickSearchHasFourPhrasesAndNoNear()
    {
        Assert.Equal(QuickSearches.All.Count * 4, QuickSearches.Phrases().Count());
        Assert.All(QuickSearches.All, search => Assert.False(search.Arguments.ContainsKey("near")));
    }

    [Fact]
    public async Task EveryQuickSearchPassesItsToolsValidation()
    {
        var install = new MemoryInstall();
        var galaxy = new FakeGalaxy();
        var registry = Build(install, galaxy, lookupsOn: true);

        foreach (var search in QuickSearches.All)
        {
            var before = galaxy.Searches;

            var result = await registry.InvokeAsync(
                search.Tool,
                new ToolArguments(search.Arguments),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsError, $"{search.Name}: {result.Content}");
            Assert.Equal(before + 1, galaxy.Searches);
        }
    }

    [Fact]
    public void NoQuickSearchPhraseIsAlreadyInThePhraseBook()
    {
        var install = new MemoryInstall();

        var taken = PhraseBook.From(TestSurface.For(install).Registry, [])
            .Entries
            .Select(entry => entry.Phrase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(QuickSearches.Phrases().Select(command => command.Phrase), taken.Contains);
    }

    [Fact]
    public async Task ASearchWithLookupsOffAnswersWithTheSwitchedOffSentence()
    {
        var install = new MemoryInstall();
        var galaxy = new FakeGalaxy();
        var registry = Build(install, galaxy, lookupsOn: false);
        var search = QuickSearches.All[0];

        var result = await registry.InvokeAsync(
            search.Tool,
            new ToolArguments(search.Arguments),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(
            "Galaxy search is switched off, so I can't look that up. The Commander can turn it on in settings.",
            result.Content);
        Assert.Equal(0, galaxy.Searches);
    }
}
