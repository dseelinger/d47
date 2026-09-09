using System.Text.Json;
using D47.App.Diagnostics;
using D47.App.Input;
using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The input trace, driven through the real injector with the capture stubbed.</summary>
public class TheInputTraceSeesWhatTheLogCannotTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-input-traces", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        // The switch is process-wide state, so it is put back.
        InputTraceWriter.ReadCommandLine([]);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class FakeElite : IEliteWindow
    {
        public bool IsRunning => true;

        public bool IsForeground => true;

        public (int X, int Y, int Width, int Height)? Bounds => null;

        public FocusResult Raise() =>
            throw new InvalidOperationException("the injector must never raise the game itself");
    }

    /// <summary>A capture that writes a byte, or refuses with a sentence.</summary>
    private sealed class StubCapture(string? refusal = null) : IWindowCapture
    {
        public List<string> Asked { get; } = [];

        public string? Capture(string path)
        {
            Asked.Add(Path.GetFileName(path));

            if (refusal is not null)
            {
                return refusal;
            }

            File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);
            return null;
        }
    }

    private InputTraceWriter Writer(
        IWindowCapture? capture,
        GuiFocus focus = GuiFocus.GalaxyMap,
        string? music = "GalaxyMap",
        Func<DateTimeOffset>? now = null) =>
        InputTraceWriter.Regardless(
            _folder,
            now ?? (() => Noon),
            () => new GameStatus { Flags = StatusFlags.InMainShip, GuiFocus = focus, ReadAt = Noon },
            () => music,
            capture,
            NullLogger.Instance);

    private static ScancodeInjector Injector(InputTraceWriter writer) =>
        new(new FakeElite(), NullLogger<ScancodeInjector>.Instance, null, writer) { DryRun = true };

    /// <summary>The steps the plot's own macro marks, built the way the plot builds them.</summary>
    private static IReadOnlyList<InputStep> Marked() =>
    [
        new InputStep(InputStepKind.KeyDown, 0x57),
        new InputStep(InputStepKind.KeyUp, 0x57),
        InputStep.Wait(TimeSpan.FromMilliseconds(1)).Watched("after-the-walk"),
        InputStep.Wait(TimeSpan.FromMilliseconds(1)).Watched("after-the-hold"),
    ];

    /// <summary>Every line of a trace, in the order it was written.</summary>
    private IReadOnlyList<JsonElement> Lines()
    {
        var folder = Assert.Single(Directory.GetDirectories(_folder));

        return
        [
            .. File.ReadAllLines(Path.Combine(folder, "trace.jsonl"))
                .Where(line => line.Length > 0)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    private static string? Text(JsonElement line, string field) =>
        line.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<JsonElement> Of(IEnumerable<JsonElement> lines, string kind) =>
        lines.Where(line => Text(line, "kind") == kind);

    /// <summary>
    /// The record a reader needs beside a still: when the key went, whether Elite held the foreground
    /// at that instant, the map's focus either side of the step, and the journal's own music track — a
    /// second clock on the map opening, written by something other than Status.json.
    /// </summary>
    [Fact]
    public async Task EveryStepIsALineWithItsInstantForegroundFocusAndMusic()
    {
        var writer = Writer(new StubCapture());
        var injector = Injector(writer);

        var trace = injector.Trace("galaxy-map-plot");

        Assert.NotNull(trace);

        trace.Declare("system", "Colonia");
        await injector.SendAsync(Marked(), trace, TestContext.Current.CancellationToken);
        trace.Verdict("plotted", "the route watch answered True for Colonia");
        trace.Dispose();

        writer.Dispose();
        injector.Dispose();

        var lines = Lines();
        var steps = Of(lines, "step").ToList();

        Assert.Equal("galaxy-map-plot", Text(Of(lines, "open").Single(), "caller"));
        Assert.Equal(Marked().Count, steps.Count);

        Assert.All(steps, step =>
        {
            Assert.NotNull(Text(step, "at"));
            Assert.True(step.GetProperty("foreground").GetBoolean());
            Assert.Equal("GalaxyMap", Text(step, "guiFocusBefore"));
            Assert.Equal("GalaxyMap", Text(step, "guiFocusAfter"));
            Assert.Equal("GalaxyMap", Text(step, "musicBefore"));
            Assert.Equal("GalaxyMap", Text(step, "musicAfter"));
        });

        // The caller's own evidence, and the verdict as the last line of the file.
        Assert.Equal("Colonia", Text(Of(lines, "declare").Single(line => Text(line, "name") == "system"), "value"));
        Assert.Equal("plotted", Text(lines[^1], "verdict"));
    }

    /// <summary>A still at every marked step, named for the mark, and none at the steps between.</summary>
    [Fact]
    public async Task StillsAreWrittenAtTheMarkedStepsAndNowhereElse()
    {
        var capture = new StubCapture();
        var writer = Writer(capture);
        var injector = Injector(writer);

        await injector.SendAsync(Marked(), TestContext.Current.CancellationToken);

        writer.Dispose();
        injector.Dispose();

        var folder = Assert.Single(Directory.GetDirectories(_folder));
        var stills = Directory.GetFiles(folder, "*.png").Select(Path.GetFileName).ToList();

        Assert.Equal(2, stills.Count);
        Assert.Contains(stills, name => name!.EndsWith("after-the-walk.png", StringComparison.Ordinal));
        Assert.Contains(stills, name => name!.EndsWith("after-the-hold.png", StringComparison.Ordinal));

        var marked = Of(Lines(), "step").Where(line => Text(line, "mark") is not null).ToList();

        Assert.Equal(2, marked.Count);
        Assert.All(marked, line => Assert.NotNull(Text(line, "still")));
        Assert.All(marked, line => Assert.Null(Text(line, "stillError")));
    }

 /// <summary>A second sequence on the same trace does not write over the first one's stills.</summary>
    [Fact]
    public async Task ASecondSequenceKeepsTheFirstOnesStills()
    {
        var capture = new StubCapture();
        var writer = Writer(capture);
        var injector = Injector(writer);

        // One trace, two sequences: the shape of a plot that found no route and was driven again.
        var trace = injector.Trace("galaxy-map-plot");

        Assert.NotNull(trace);

        await injector.SendAsync(Marked(), trace, TestContext.Current.CancellationToken);
        await injector.SendAsync(Marked(), trace, TestContext.Current.CancellationToken);

        trace.Dispose();
        writer.Dispose();
        injector.Dispose();

        var folder = Assert.Single(Directory.GetDirectories(_folder));
        var stills = Directory.GetFiles(folder, "*.png").Select(Path.GetFileName).ToList();

        Assert.Equal(4, stills.Count);
        Assert.Equal(4, stills.Distinct(StringComparer.Ordinal).Count());

        // And they still sort into the order they were taken, which is what a reader reads them in.
        var named = Of(Lines(), "step")
            .Select(line => Text(line, "still"))
            .OfType<string>()
            .ToList();

        Assert.Equal([.. stills.OfType<string>().Order(StringComparer.Ordinal)], named);
    }

    /// <summary>
    /// A still is grabbed behind the sequence, on the writer's thread, so the line says when the
    /// picture was taken as well as when the step went.
    /// </summary>
    [Fact]
    public async Task AStillSaysWhenItWasTakenAndHowLongItTook()
    {
        var ticks = 0;
        var writer = Writer(new StubCapture(), now: () => Noon.AddSeconds(ticks++));
        var injector = Injector(writer);

        await injector.SendAsync(Marked(), TestContext.Current.CancellationToken);

        writer.Dispose();
        injector.Dispose();

        var marked = Of(Lines(), "step").Where(line => Text(line, "mark") is not null).ToList();

        Assert.Equal(2, marked.Count);
        Assert.All(marked, line =>
        {
            var at = DateTimeOffset.Parse(Text(line, "at")!, null);
            var stillAt = DateTimeOffset.Parse(Text(line, "stillAt")!, null);

            // The step's own instant is the injector's; the still's is the writer's, a second per reading of
            // it, so the two are never the same moment by construction and the capture always spans at least
            // one tick.
            Assert.NotEqual(at, stillAt);
            Assert.True(line.GetProperty("stillMs").GetInt32() >= 1000);
        });
    }

    /// <summary>A capture that cannot be taken is a line saying so, and never a failed sequence.</summary>
    [Fact]
    public async Task ACaptureThatFailsIsALineRatherThanAFailedSequence()
    {
        var writer = Writer(new StubCapture("Windows Graphics Capture is not available on this machine"));
        var injector = Injector(writer);

        var result = await injector.SendAsync(Marked(), TestContext.Current.CancellationToken);

        writer.Dispose();
        injector.Dispose();

        Assert.True(result.Sent);

        var folder = Assert.Single(Directory.GetDirectories(_folder));

        Assert.Empty(Directory.GetFiles(folder, "*.png"));

        var marked = Of(Lines(), "step").Where(line => Text(line, "mark") is not null).ToList();

        Assert.Equal(2, marked.Count);
        Assert.All(marked, line => Assert.Null(Text(line, "still")));
        Assert.All(
            marked,
            line => Assert.Equal("Windows Graphics Capture is not available on this machine", Text(line, "stillError")));
    }

    /// <summary>
    /// A sequence that never reached a verdict — a cancelled turn, or a fault — still leaves a trace,
    /// and the trace says the last step it got to rather than simply stopping.
    /// </summary>
    [Fact]
    public async Task ACancelledSequenceLeavesATraceNamingTheStepItReached()
    {
        var writer = Writer(new StubCapture());
        var injector = Injector(writer);

        var trace = injector.Trace("galaxy-map-plot");

        Assert.NotNull(trace);

        await injector.SendAsync(Marked(), trace, TestContext.Current.CancellationToken);
        trace.Declare("exit", "the search sequence was sent");
        trace.Dispose();

        writer.Dispose();
        injector.Dispose();

        var lines = Lines();

        Assert.Equal("the search sequence was sent", Text(Of(lines, "declare").Last(), "value"));
        Assert.Equal("unfinished", Text(lines[^1], "verdict"));
    }

    /// <summary>
    /// The generality: a caller that hands in no trace of its own is traced anyway, one folder per
    /// sequence, ending in the injector's own verdict.
    /// </summary>
    [Fact]
    public async Task ASequenceWithNoTraceOfItsOwnIsStillWrittenDown()
    {
        var writer = Writer(new StubCapture());
        var injector = Injector(writer);

        await injector.SendAsync(Marked(), TestContext.Current.CancellationToken);

        writer.Dispose();
        injector.Dispose();

        var lines = Lines();

        Assert.Equal("unnamed", Text(Of(lines, "open").Single(), "caller"));
        Assert.Equal("Sent", Text(lines[^1], "verdict"));
    }

    /// <summary>Both roads on, and neither remembered.</summary>
    [Fact]
    public void TheSwitchTurnsTracingOnForOneRun()
    {
        InputTraceWriter.ReadCommandLine(["--trace-input"]);

        Assert.True(InputTraceWriter.Enabled);

        InputTraceWriter.ReadCommandLine([]);

        Assert.False(InputTraceWriter.Enabled);
    }

    /// <summary>
    /// Per-run means per-run: with the switch on, nothing about it reaches <c>settings.json</c> and no
    /// row exists for it anywhere on the settings surface.
    /// </summary>
    [Fact]
    public void TheFlagIsNotASettingAndHasNoRow()
    {
        InputTraceWriter.ReadCommandLine(["--trace-input"]);

        var (settings, _, paths, registry, _) = TestSurface.CreateFull();

        // A real write, so the file on disk is the whole of what the app would leave behind.
        settings.Replace(
            "a test",
            current => current with { Actions = current.Actions with { AutoPlot = !current.Actions.AutoPlot } });

        var stored = File.ReadAllText(paths.SettingsFile);

        Assert.DoesNotContain("trace", stored, StringComparison.OrdinalIgnoreCase);

        var rows = registry.All.SelectMany(capability => capability.Descriptor.Settings).ToList();

        Assert.NotEmpty(rows);
        Assert.DoesNotContain(rows, row => row.Key.Contains("trace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rows, row => row.Label.Contains("input trace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rows, row => row.Help.Contains(InputTraceWriter.Flag, StringComparison.Ordinal));
    }

    /// <summary>
    /// Traces are written under <c>data\flight\</c>, asserted through the real composition path.
    /// </summary>
    [Fact]
    public void TracesAreWrittenUnderFlight()
    {
        InputTraceWriter.ReadCommandLine(["--trace-input"]);

        var paths = new D47.Core.AppPaths(_folder);
        paths.EnsureCreated();

        // The real composition path, so what is asserted is where a run actually writes rather than a second
        // copy of the same string.
        using var writer = InputTraceWriter.Create(
            paths,
            () => Noon,
            () => GameStatus.Unknown,
            () => null,
            () => null,
            NullLogger.Instance);

        Assert.NotNull(writer);
        Assert.Equal(
            Path.Combine(paths.Data, "flight", InputTraceWriter.FolderName),
            writer.Folder);
    }
}
