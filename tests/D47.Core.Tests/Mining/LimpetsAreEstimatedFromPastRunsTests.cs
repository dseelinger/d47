using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Mining;

/// <summary>The history walk folds finished mining runs, and estimate_limpets answers from them (#610).</summary>
public class LimpetsAreEstimatedFromPastRunsTests
{
    private const string Fid = "F1";

    private static string Line(string day, string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"" + day + "T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static IEnumerable<string> Repeat(int times, string line) => Enumerable.Repeat(line, times);

    private static string Refine(string day, string material) =>
        Line(day, "11:00:00", "MiningRefined", $"\"Type\":\"${material.ToLowerInvariant()}_name;\", \"Type_Localised\":\"{material}\"");

    /// <summary>A run of the shape the 2025-07-10 journal holds: 108 t from 19 collectors and 33 prospectors.</summary>
    private static List<string> TheJulyRun(string day = "2025-07-10")
    {
        var lines = new List<string> { Line(day, "06:23:44", "LoadGame", $"\"FID\":\"{Fid}\", \"Commander\":\"Jameson\"") };
        lines.AddRange(Repeat(33, Line(day, "10:49:30", "LaunchDrone", "\"Type\":\"Prospector\"")));
        lines.AddRange(Repeat(19, Line(day, "10:49:40", "LaunchDrone", "\"Type\":\"Collection\"")));
        lines.AddRange(Repeat(90, Refine(day, "Platinum")));
        lines.AddRange(Repeat(12, Refine(day, "Samarium")));
        lines.AddRange(Repeat(6, Refine(day, "Praseodymium")));
        lines.Add(Line(day, "12:14:28", "Docked", "\"StationName\":\"Jameson Memorial\""));
        return lines;
    }

    private static HistoryBackfill Walk(MemoryInstall install)
    {
        var history = new HistoryBackfill { Directory = install.Root, FileSystem = install.Files, Loggers = NullLoggerFactory.Instance };
        history.Run(TestContext.Current.CancellationToken);
        return history;
    }

    private static async Task<ToolResult> Estimate(
        HistoryState state, IReadOnlyList<MiningRun>? runs, string tonnes, string? material = null)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["tonnes"] = tonnes };

        if (material is not null)
        {
            arguments["material"] = material;
        }

        var registry = CapabilityRegistry.Build(
            [MiningCapability.Create(null, () => Fid, () => state, fid => fid == Fid ? runs : null)]);

        return await registry.InvokeAsync(
            MiningCapability.EstimateTool,
            new ToolArguments(arguments),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);
    }

    [Fact]
    public void TheWalkFoldsTheJulyRun()
    {
        var install = new MemoryInstall();
        install.Files.WriteLines(Path.Combine(install.Root, "Journal.2025-07-10T062344.01.log"), TheJulyRun());

        var runs = Walk(install).MiningRuns![Fid];

        var run = Assert.Single(runs);
        Assert.Equal(108, run.TonnesRefined);
        Assert.Equal(19, run.CollectorsLaunched);
        Assert.Equal(33, run.ProspectorsLaunched);
    }

    [Fact]
    public void ARunStillOpenAtTheLastJournalIsNotCounted()
    {
        var install = new MemoryInstall();
        var lines = TheJulyRun();
        lines.RemoveAt(lines.Count - 1);
        install.Files.WriteLines(Path.Combine(install.Root, "Journal.2025-07-10T062344.01.log"), lines);

        Assert.False(Walk(install).MiningRuns!.ContainsKey(Fid));
    }

    [Fact]
    public async Task TenTonnesOverTheJulyRunIsTwoCollectorsAndFourProspectorsFromAllRuns()
    {
        var install = new MemoryInstall();
        install.Files.WriteLines(Path.Combine(install.Root, "Journal.2025-07-10T062344.01.log"), TheJulyRun());
        var history = Walk(install);

        var result = await Estimate(history.State, history.MiningRuns![Fid], "10");

        Assert.False(result.IsError);
        Assert.Equal(
            "About 2 collectors and 4 prospectors for 10 tonnes, from your 1 past run of any material: "
            + "a collector every 5.7 tonnes, a prospector every 3.3 tonnes.",
            result.Content);
    }

    [Fact]
    public async Task AMaterialMostRefinedInThreeRunsUsesItsOwnRatio()
    {
        var install = new MemoryInstall();

        foreach (var day in new[] { "2025-07-10", "2025-07-11", "2025-07-12" })
        {
            install.Files.WriteLines(Path.Combine(install.Root, $"Journal.{day}T062344.01.log"), TheJulyRun(day));
        }

        var history = Walk(install);

        var result = await Estimate(history.State, history.MiningRuns![Fid], "10", "platinum");

        // 270 t of Platinum from 57 collectors and 99 prospectors.
        Assert.Equal(
            "About 3 collectors and 4 prospectors for 10 tonnes of Platinum, from your 3 past Platinum runs: "
            + "a collector every 4.7 tonnes, a prospector every 2.7 tonnes.",
            result.Content);
    }

    [Fact]
    public async Task AMaterialWithFewerThanThreeRunsSaysItUsedAllRuns()
    {
        var install = new MemoryInstall();
        install.Files.WriteLines(Path.Combine(install.Root, "Journal.2025-07-10T062344.01.log"), TheJulyRun());
        var history = Walk(install);

        var result = await Estimate(history.State, history.MiningRuns![Fid], "10", "Osmium");

        Assert.StartsWith("Fewer than 3 of your runs were mostly Osmium, so this is across all of them.", result.Content);
        Assert.Contains("of any material", result.Content);
    }

    [Fact]
    public async Task NoPastRunsGivesNoNumber()
    {
        var result = await Estimate(HistoryState.Done, null, "10");

        Assert.Equal("I have no mining runs to estimate from.", result.Content);
        Assert.DoesNotContain(result.Content, char.IsDigit);
    }

    [Theory]
    [InlineData(HistoryState.Pending)]
    [InlineData(HistoryState.Running)]
    public async Task BeforeTheWalkFinishesItGivesNoNumber(HistoryState state)
    {
        var install = new MemoryInstall();
        install.Files.WriteLines(Path.Combine(install.Root, "Journal.2025-07-10T062344.01.log"), TheJulyRun());
        var runs = Walk(install).MiningRuns![Fid];

        var result = await Estimate(state, runs, "10");

        Assert.Equal("I have not finished reading the journal history yet, so I cannot estimate limpets.", result.Content);
        Assert.DoesNotContain(result.Content, char.IsDigit);
    }
}
