using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A spoken faction name is corrected against the factions this Commander's journals name (#486).</summary>
public class AFactionIsReadAsTheJournalSpellsItTests
{
    private sealed class Recording : IGalaxyService
    {
        public GalaxyQuery? LastQuery { get; private set; }

        public GalaxySearchResult Result { get; set; } =
            new("Sol", 1, [new SystemSummary { Name = "Eurybia", Distance = 12.5 }]);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static readonly string[] Met = ["Eurybia Blue Mafia", "Eurybia Purple Posse", "Mother Gaia"];

    private static async Task<(ToolResult Result, Recording Galaxy)> Search(
        string filter,
        string value,
        GalaxySearchResult? answer = null)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var galaxy = new Recording();

        if (answer is not null)
        {
            galaxy.Result = answer;
        }

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy, () => "Sol", settings, factions: () => Met)]);

        var result = await registry.InvokeAsync(
            "search_systems",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { [filter] = value }),
            TestContext.Current.CancellationToken);

        return (result, galaxy);
    }

    private static string Sent(Recording galaxy) => Assert.Single(Assert.Single(galaxy.LastQuery!.Criteria).Choices);

    [Theory]
    [InlineData("faction", "eurybia blue mafia")]
    [InlineData("controlling_faction", "blue mafia")]
    public async Task AMetFactionIsSentInTheJournalsSpellingAndSaysSo(string filter, string said)
    {
        var (result, galaxy) = await Search(filter, said);

        Assert.Equal("Eurybia Blue Mafia", Sent(galaxy));
        Assert.StartsWith("Read as Eurybia Blue Mafia.", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExactNameIsNotAnnouncedAsACorrection()
    {
        var (result, galaxy) = await Search("faction", "Eurybia Blue Mafia");

        Assert.Equal("Eurybia Blue Mafia", Sent(galaxy));
        Assert.DoesNotContain("Read as", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnmetFactionIsSentAsGivenAndAnEmptyResultSaysItMayBeMisspelled()
    {
        var (result, galaxy) = await Search("faction", "Eurybia Blu Mafai", new GalaxySearchResult("Sol", 0, []));

        Assert.Equal("Eurybia Blu Mafai", Sent(galaxy));
        Assert.Contains("may be misspelled", result.Content, StringComparison.Ordinal);
        Assert.Contains("Did you mean Eurybia Blue Mafia", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("not present", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUnmetFactionThatFindsSystemsIsNotQuestioned()
    {
        var (result, _) = await Search("faction", "Sirius Corporation");

        Assert.DoesNotContain("misspelled", result.Content, StringComparison.Ordinal);
    }

    private static readonly GalaxySearchResult Controlled = new("Eurybia", 1,
    [
        new SystemSummary
        {
            Name = "LPM 173",
            Distance = 9.4,
            ControllingFaction = "Eurybia Blue Mafia",
            Factions = [new FactionPresence("Booty Bay Butchers", 0.202557), new FactionPresence("Eurybia Blue Mafia", 0.67355)],
            ReportedAt = new DateTimeOffset(2026, 9, 25, 23, 59, 57, TimeSpan.Zero),
        },
    ]);

    [Fact]
    public async Task AFactionSearchNamesTheControllerTheInfluenceAndTheReportDate()
    {
        var (result, _) = await Search("faction", "Eurybia Blue Mafia", Controlled);

        Assert.Contains(
            "controlled by Eurybia Blue Mafia; Eurybia Blue Mafia at 67.4% influence; reported 2026-09-25",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASearchWithoutAFactionLeavesFactionsOut()
    {
        var (result, _) = await Search("allegiance", "Independent", Controlled);

        Assert.DoesNotContain("controlled by", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("influence", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("reported", result.Content, StringComparison.Ordinal);
    }
}
