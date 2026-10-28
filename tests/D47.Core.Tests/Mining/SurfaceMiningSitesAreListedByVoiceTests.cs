using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Mining;

public class SurfaceMiningSitesAreListedByVoiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private static string Line(string day, string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"" + day + "T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Refine(string day, string name) =>
        Line(day, "10:01:00", "MiningRefined", $"\"Type\":\"${name.ToLowerInvariant()}_name;\", \"Type_Localised\":\"{name}\"");

    private static IEnumerable<string> Run(string day, int body, string name, int tonnes, string? fix)
    {
        var at = $"\"StarSystem\":\"Arque\", \"SystemAddress\":99, \"Body\":\"Arque {body} c\", \"BodyID\":{body}";

        yield return Line(day, "09:00:00", "ApproachBody", at);

        if (fix is not null)
        {
            yield return Line(day, "09:30:00", "Touchdown", at + ", " + fix + ", \"PlayerControlled\":true");
        }

        yield return Line(day, "09:40:00", "LaunchSRV", "\"PlayerControlled\":true");

        for (var i = 0; i < tonnes; i++)
        {
            yield return Refine(day, name);
        }

        yield return Line(day, "10:30:00", "DockSRV");
    }

    private static SurfaceMiningSites Four()
    {
        var lines = Run("2025-03-01", 1, "Platinum", 40, "\"Latitude\":21.44, \"Longitude\":-63.8")
            .Concat(Run("2025-03-09", 1, "Platinum", 5, null))
            .Concat(Run("2025-04-02", 2, "Osmium", 30, null))
            .Concat(Run("2025-05-05", 3, "Copper", 20, "\"Latitude\":1, \"Longitude\":2"))
            .Concat(Run("2024-12-12", 4, "Platinum", 90, null));

        return lines
            .Select(line =>
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                return parsed!;
            })
            .OrderBy(e => e.Timestamp)
            .Aggregate(SurfaceMiningSites.None, (sites, e) => sites.Apply(e));
    }

    private static async Task<string> Ask(SurfaceMiningSites? sites, string? material = null, HistoryState state = HistoryState.Done)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        if (material is not null)
        {
            arguments["material"] = material;
        }

        var registry = CapabilityRegistry.Build(
            [MiningCapability.Create(null, () => "F1", () => state, null, () => sites, () => Now)]);

        var result = await registry.InvokeAsync(
            MiningCapability.SitesTool,
            new ToolArguments(arguments),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);

        return result.Content;
    }

    [Fact]
    public async Task WithNoMaterialThreeSitesAreSpokenLatestFirstAndTheRestCounted()
    {
        var text = await Ask(Four());

        Assert.StartsWith("Arque 3 c in Arque: 20 tonnes refined, mostly Copper over 1 run, last on 5 May,", text);
        Assert.True(text.IndexOf("Arque 3 c", StringComparison.Ordinal) < text.IndexOf("Arque 2 c", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Arque 2 c", StringComparison.Ordinal) < text.IndexOf("Arque 1 c", StringComparison.Ordinal));
        Assert.DoesNotContain("Arque 4 c", text);
        Assert.EndsWith("and 1 more.", text);
    }

    [Fact]
    public async Task WithAMaterialSitesAreOrderedByThatMaterialsTonnes()
    {
        var text = await Ask(Four(), "platinum");

        Assert.StartsWith("Arque 4 c in Arque: 90 tonnes of Platinum refined over 1 run, last on 12 December 2024.", text);
        Assert.Contains("Arque 1 c in Arque: 45 tonnes of Platinum refined over 2 runs, last on 9 March, near latitude 21.4, longitude -63.8.", text);
        Assert.DoesNotContain("more", text);
        Assert.DoesNotContain("Arque 2 c", text);
    }

    [Fact]
    public async Task ASiteWithNoPositionSaysNoLatitude()
    {
        var text = await Ask(Four(), "Osmium");

        Assert.DoesNotContain("latitude", text);
    }

    [Fact]
    public async Task AMaterialNoSiteRefinedGetsTheNoRecordSentence() =>
        Assert.Equal("You have no surface mining of Gold on record.", await Ask(Four(), "Gold"));

    [Fact]
    public async Task NoSitesSaysSo() =>
        Assert.Equal("You have no surface mining on record.", await Ask(SurfaceMiningSites.None));

    [Theory]
    [InlineData(HistoryState.Pending)]
    [InlineData(HistoryState.Running)]
    public async Task AWalkNotDoneGetsTheHistorySentence(HistoryState state) =>
        Assert.Contains("not finished reading the journal history", await Ask(Four(), null, state));

    [Theory]
    [InlineData(HistoryState.Failed)]
    [InlineData(HistoryState.Stopped)]
    public async Task AWalkThatDidNotFinishSaysSo(HistoryState state) =>
        Assert.Contains("did not finish", await Ask(Four(), null, state));
}
