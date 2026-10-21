using D47.Core.CaptainsLog;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.CaptainsLog;

/// <summary>"Export my log entries" writes the Commander's entries to a Markdown file, oldest first, and never over an earlier export.</summary>
public class ExportCaptainsLogEntriesAsTextTests
{
    private const string Commander = "F100";

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private static readonly string Data = Path.Combine(@"C:\d47-test", "data");

    private static string Expected(string name) => Path.Combine(Data, "captains-log", Commander, name);

    private static (CaptainsLogStore Store, MemoryFileSystem Files, CapabilityRegistry Registry) Bench(bool entries = true)
    {
        var files = new MemoryFileSystem();
        var store = new CaptainsLogStore(Path.Combine(Data, "captains-log.json"), files, NullLogger<CaptainsLogStore>.Instance);

        if (entries)
        {
            store.Add(Commander, new CaptainsLogEntry("b", "Second, with everything known", new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero))
            {
                System = "Sol",
                Body = "Earth",
                Station = "Abraham Lincoln",
                ShipType = "Cobra Mk III",
                ShipName = "Kestrel",
                Latitude = 12.5,
                Longitude = -45.25,
            });
            store.Add(Commander, new CaptainsLogEntry("c", "Third, system only", new DateTimeOffset(2026, 10, 21, 7, 0, 0, TimeSpan.Zero)) { System = "Colonia" });
            store.Add(Commander, new CaptainsLogEntry("a", "First, nothing known", new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero)));
        }

        var registry = CapabilityRegistry.Build([CaptainsLogCapability.Create(store, () => Commander, () => null, null, () => Now)]);

        return (Store: store, Files: files, Registry: registry);
    }

    private static Task<ToolResult> ExportAsync(CapabilityRegistry registry) =>
        registry.InvokeAsync(CaptainsLogCapability.ExportTool, ToolArguments.Empty, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task ThreeEntriesExportAsThreeHeadingsOldestFirstWithOnlyTheKnownFields()
    {
        var (_, files, registry) = Bench();

        var answer = await ExportAsync(registry);
        var text = files.ReadText(Expected("captains-log-2026-10-21.md"))!;

        Assert.False(answer.IsError);
        Assert.Contains("three log entries", answer.Spoken, StringComparison.Ordinal);
        Assert.Contains(Expected("captains-log-2026-10-21.md"), answer.Spoken, StringComparison.Ordinal);

        var headings = text.Split('\n').Where(line => line.StartsWith("## ", StringComparison.Ordinal)).Select(line => line.TrimEnd()).ToList();

        Assert.Equal(["## 14 October 3312, 08:00", "## 20 October 3312, 08:00", "## 21 October 3312, 07:00"], headings);
        Assert.True(text.IndexOf("First", StringComparison.Ordinal) < text.IndexOf("Second", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Second", StringComparison.Ordinal) < text.IndexOf("Third", StringComparison.Ordinal));
        Assert.Contains("*System: Sol; Body: Earth; Station: Abraham Lincoln; Ship: Cobra Mk III \"Kestrel\"; Position: 12.5000, -45.2500*", text, StringComparison.Ordinal);
        Assert.Contains("*System: Colonia*", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split('\n').Select(line => line.TrimEnd()).Count(line => line.StartsWith('*') && line.EndsWith('*')));
    }

    [Fact]
    public async Task ASecondExportTheSameDayIsWrittenBesideTheFirstNotOverIt()
    {
        var (_, files, registry) = Bench();

        await ExportAsync(registry);
        var second = await ExportAsync(registry);

        Assert.Contains("captains-log-2026-10-21-2.md", second.Spoken, StringComparison.Ordinal);
        Assert.NotNull(files.ReadText(Expected("captains-log-2026-10-21.md")));
        Assert.NotNull(files.ReadText(Expected("captains-log-2026-10-21-2.md")));
    }

    [Fact]
    public async Task WithNoEntriesNothingIsWrittenAndTheAnswerSaysSo()
    {
        var (_, files, registry) = Bench(entries: false);

        var answer = await ExportAsync(registry);

        Assert.Equal("You have no log entries to export.", answer.Spoken);
        Assert.Null(files.ReadText(Expected("captains-log-2026-10-21.md")));
    }

    [Fact]
    public async Task TheModelIsRefusedTheExport()
    {
        var (_, files, registry) = Bench();

        var refused = await registry.InvokeAsync(
            CaptainsLogCapability.ExportTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Null(files.ReadText(Expected("captains-log-2026-10-21.md")));
    }

    [Theory]
    [InlineData("export my log entries")]
    [InlineData("Export all my log entries.")]
    [InlineData("export the captain's log entries")]
    public void TheExportPhrasesAreRead(string input) =>
        Assert.IsType<CaptainsLogReading.ExportAll>(CaptainsLogPhrase.Read(input));
}
