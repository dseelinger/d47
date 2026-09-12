using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>The step observer and the marks that go with it.</summary>
public class TheKeysAreWatchedStepByStepTests
{
    /// <summary>An observer that keeps what it was told, in order.</summary>
    private sealed class Watcher : IInputStepObserver
    {
        public List<Trace> Traces { get; } = [];

        public IInputTrace Open(string caller)
        {
            var trace = new Trace(caller);
            Traces.Add(trace);

            return trace;
        }

        public sealed class Trace(string caller) : IInputTrace
        {
            public string Caller => caller;

            public List<InputStepReport> Steps { get; } = [];

            public List<(string Name, string Value)> Declared { get; } = [];

            public (string Verdict, string Reason)? Ended { get; private set; }

            public int Closings { get; private set; }

            /// <summary>Every line's kind, in the order it was written, for what came last.</summary>
            public List<string> Written { get; } = [];

            public void Stepped(InputStepReport report)
            {
                Steps.Add(report);
                Written.Add("step");
            }

            public void Declare(string name, string value)
            {
                Declared.Add((name, value));
                Written.Add($"declare {name}");
            }

            public void Verdict(string verdict, string reason)
            {
                Ended = (verdict, reason);
                Written.Add("verdict");
            }

            public void Dispose() => Closings++;

            public string? Value(string name) =>
                Declared.Where(entry => entry.Name == name).Select(entry => entry.Value).LastOrDefault();
        }
    }

    /// <summary>A route file that says what it is, so the plot's declared evidence is a real string.</summary>
    private sealed class DescribingWatch(bool? answer, string description) : IPlotWatch
    {
        public Task<PlotConfirmation> ConfirmAsync(string system, CancellationToken cancellationToken) =>
            Task.FromResult(new PlotConfirmation(answer, null, null));

        public string Describe() => description;
    }

    /// <summary>A route check that is interrupted, which is how a Commander cancels a turn mid-macro.</summary>
    private sealed class CancelledWatch : IPlotWatch
    {
        public Task<PlotConfirmation> ConfirmAsync(string system, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }

    private static EliteBinds Binds(params (string Action, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(entry => new EliteBinding(entry.Action, "Primary", "Keyboard", entry.Key))],
    };

    private static EliteBinds MapBinds() => Binds(
        ("GalaxyMapOpen", "Key_M"),
        ("UI_Up", "Key_W"),
        ("UI_Select", "Key_Space"),
        ("UI_Down", "Key_S"),
        ("CamTranslateRight", "Key_R"),
        ("CamTranslateLeft", "Key_L"));

    private static GameStatus Flying => new()
    {
        Flags = StatusFlags.InMainShip,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static ActionSurface Actions(RecordingGameInput input, EliteBinds binds, GameStatus? status = null) => new()
    {
        Binds = () => binds,
        Status = () => status ?? Flying,
        Input = input,
        Enabled = () => true,
    };

    private static NavigationSurface Navigation(
        RecordingGameInput input,
        IPlotWatch watch,
        ILogger? log = null,
        Func<bool, CancellationToken, Task<bool?>>? awaitMap = null) => new()
    {
        Clipboard = new RecordingClipboard(),
        Actions = Actions(input, MapBinds()),
        AutoPlotEnabled = () => true,
        WatchRoute = () => watch,
        AwaitGalaxyMap = awaitMap ?? ((open, _) => Task.FromResult<bool?>(true)),
        Log = log ?? NullLogger.Instance,
    };

    private static async Task<ToolResult> Plot(
        RecordingGameInput input,
        IPlotWatch watch,
        ILogger? log = null,
        Func<bool, CancellationToken, Task<bool?>>? awaitMap = null)
    {
        var registry = CapabilityRegistry.Build(
            [NavigationCapability.Create(Navigation(input, watch, log, awaitMap))]);

        return await registry.InvokeAsync(
            "plot_course",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = "Colonia" }),
            TestContext.Current.CancellationToken);
    }

    private static (RecordingGameInput Input, Watcher Watching) Watched()
    {
        var watching = new Watcher();

        return (new RecordingGameInput { Observer = watching }, watching);
    }

    /// <summary>
    /// One trace for the whole attempt, carrying every step the injector sent with the instant it went
    /// and the foreground verdict at that instant.
    /// </summary>
    [Fact]
    public async Task APlotIsTracedStepByStep()
    {
        var (input, watching) = Watched();

        await Plot(input, new DescribingWatch(true, "written 2026-09-07T01:00:00Z, ends at Colonia"));

        var trace = Assert.Single(watching.Traces);

        Assert.Equal("galaxy-map-plot", trace.Caller);

        // Every key that was sent is a line, across all three sends, in the order they went.
        Assert.Equal(input.Steps.Count, trace.Steps.Count);
        Assert.Equal(
            [.. input.Steps.Select(step => step.ToString())],
            [.. trace.Steps.Select(step => step.Step.ToString())]);

        Assert.All(trace.Steps, step => Assert.True(step.Foreground));
        Assert.All(trace.Steps, step => Assert.NotEqual(default, step.At));
    }

    /// <summary>
    /// The caller's own evidence, which the injector cannot know: the system, and the route file read
    /// at the three moments that make the difference between "no route was written" and "a route was
    /// written and read too late".
    /// </summary>
    [Fact]
    public async Task ThePlotDeclaresTheRouteFileAsItsEvidence()
    {
        var (input, watching) = Watched();

        await Plot(input, new DescribingWatch(true, "written 2026-09-07T01:00:00Z, ends at Colonia"));

        var trace = Assert.Single(watching.Traces);

        Assert.Equal("Colonia", trace.Value("system"));
        Assert.Equal("written 2026-09-07T01:00:00Z, ends at Colonia", trace.Value("route at open"));
        Assert.Equal("written 2026-09-07T01:00:00Z, ends at Colonia", trace.Value("route after the hold"));
        Assert.Equal("written 2026-09-07T01:00:00Z, ends at Colonia", trace.Value("route at the verdict"));

        Assert.Equal("plotted", trace.Ended?.Verdict);
        Assert.Equal(1, trace.Closings);
    }

    /// <summary>The five moments between "map open" and "select held", marked and in order.</summary>
    [Fact]
    public async Task TheFiveStepsWorthSeeingAreMarked()
    {
        var (input, watching) = Watched();

        await Plot(input, new DescribingWatch(true, string.Empty));

        var trace = Assert.Single(watching.Traces);

        Assert.Equal(
            ["after-the-walk", "after-the-paste", "after-the-camera", "after-the-nudges", "after-the-hold"],
            [.. trace.Steps.Where(step => step.Step.Capture).Select(step => step.Step.Mark ?? string.Empty)]);
    }

    /// <summary>A turn the Commander cancels mid-macro.</summary>
    [Fact]
    public async Task ACancelledPlotStillNamesTheStepItReached()
    {
        var (input, watching) = Watched();

        try
        {
            await Plot(input, new CancelledWatch());
        }
        catch (OperationCanceledException)
        {
        // The subject is what the trace was left holding, not how the turn ended.
        }

        var trace = Assert.Single(watching.Traces);

        Assert.Equal("the search sequence was sent", trace.Value("exit"));
        Assert.Null(trace.Ended);
        Assert.Equal(1, trace.Closings);
    }

    /// <summary>A turn cut short while the map is closing, after the route has already been checked.</summary>
    [Fact]
    public async Task APlotCutShortWhileTheMapClosesKeepsTheVerdictItAlreadyHad()
    {
        var (input, watching) = Watched();

        try
        {
            await Plot(
                input,
                new DescribingWatch(false, string.Empty),
                awaitMap: (open, _) => open
                    ? Task.FromResult<bool?>(true)
                    : throw new OperationCanceledException());
        }
        catch (OperationCanceledException)
        {
        // The subject is what the trace was left holding, not how the turn ended.
        }

        var trace = Assert.Single(watching.Traces);

        Assert.Equal("the route was checked", trace.Value("exit"));
        Assert.Equal("no route", trace.Ended?.Verdict);
        Assert.Equal("verdict", trace.Written[^1]);
    }

    /// <summary>The happy path names its exit step too, so the line means the same thing on every road.</summary>
    [Fact]
    public async Task AFinishedPlotNamesItsExitStep()
    {
        var (input, watching) = Watched();

        await Plot(input, new DescribingWatch(true, string.Empty));

        Assert.Equal("the map was closed", Assert.Single(watching.Traces).Value("exit"));
    }

    /// <summary>The verdict is the last thing the trace is told, on a path that reaches one.</summary>
    [Fact]
    public async Task TheVerdictIsTheLastThingWritten()
    {
        var (input, watching) = Watched();

        await Plot(input, new DescribingWatch(true, string.Empty));

        var trace = Assert.Single(watching.Traces);

        Assert.Equal("verdict", trace.Written[^1]);
        Assert.Equal("declare exit", trace.Written[^2]);
    }

    /// <summary>The exit line in d47's own log, which is the half of this that works on an ordinary run: the trace is off unless it was asked for.</summary>
    [Theory]
    [InlineData(true, "the map was closed")]
    [InlineData(false, "the search sequence was sent")]
    public async Task ThePlotLogsItsExitStepWithoutATrace(bool finished, string expected)
    {
        var log = new RecordingLogger();
        var input = new RecordingGameInput();

        try
        {
            await Plot(input, finished ? new DescribingWatch(true, string.Empty) : new CancelledWatch(), log);
        }
        catch (OperationCanceledException)
        {
        // The subject is the line that was logged, not how the turn ended.
        }

        Assert.Contains(
            log.Entries,
            entry => entry.Message == $"Galaxy-map plot for Colonia left off at {expected}");
    }

    /// <summary>
    /// The generality, which is the whole reason the hook is on the injector rather than on the plot:
    /// the launch walk is traced with no code in the launch path at all.
    /// </summary>
    [Fact]
    public async Task ALaunchIsTracedWithNoCodeOfItsOwn()
    {
        var (input, watching) = Watched();

        var docked = new GameStatus
        {
            Flags = StatusFlags.InMainShip | StatusFlags.Docked,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        var binds = Binds(
            ("FocusLeftPanel", "Key_1"),
            ("UI_Back", "Key_B"),
            ("UI_Down", "Key_S"),
            ("UI_Select", "Key_Z"));

        var outcome = await Launch.RunAsync(
            Actions(input, binds, docked),
            (_, _) => Task.FromResult<bool?>(true),
            _ => Task.FromResult<bool?>(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Launched, outcome.Ending);

        // One trace per sequence, each named for nothing in particular and each ending in the injector's own
        // verdict, because a caller that declares nothing needs no name.
        Assert.NotEmpty(watching.Traces);
        Assert.All(watching.Traces, trace => Assert.Equal("unnamed", trace.Caller));
        Assert.All(watching.Traces, trace => Assert.Equal("Sent", trace.Ended?.Verdict));
        Assert.Equal(
            input.Steps.Count,
            watching.Traces.Sum(trace => trace.Steps.Count));
    }

    /// <summary>
    /// With nothing watching, nothing is asked for: the trace door answers null and a step carries no
    /// mark.
    /// </summary>
    [Fact]
    public async Task WithNoObserverNothingIsOpenedAndNothingIsMarked()
    {
        var input = new RecordingGameInput();

        Assert.Null(input.Trace("galaxy-map-plot"));

        await input.SendAsync(
            InputSequence.Tap(new EliteBinding("UI_Select", "Primary", "Keyboard", "Key_Z")),
            TestContext.Current.CancellationToken);

        Assert.All(input.Steps, step => Assert.False(step.Capture));
    }

    /// <summary>
    /// Marking a step changes nothing about what is sent — same kind, same code, same wait, same
    /// printed form.
    /// </summary>
    [Fact]
    public void AMarkedStepIsSentAsThePlainOneIs()
    {
        var plain = InputStep.Wait(TimeSpan.FromMilliseconds(150));
        var marked = plain.Watched("after-the-walk");

        Assert.False(plain.Capture);
        Assert.Null(plain.Mark);

        Assert.True(marked.Capture);
        Assert.Equal("after-the-walk", marked.Mark);

        Assert.Equal(plain.Kind, marked.Kind);
        Assert.Equal(plain.Code, marked.Code);
        Assert.Equal(plain.Delay, marked.Delay);
        Assert.Equal(plain.ToString(), marked.ToString());
    }

    /// <summary>A run's last step is where a hold's still belongs, and only the last one is marked.</summary>
    [Fact]
    public void OnlyTheLastStepOfAWatchedRunIsMarked()
    {
        var run = InputSequence.WatchedAfter(
            InputSequence.Hold(new EliteBinding("UI_Select", "Primary", "Keyboard", "Key_Z"), TimeSpan.FromSeconds(1)),
            "after-the-hold");

        Assert.Equal("after-the-hold", run[^1].Mark);
        Assert.All(run.Take(run.Count - 1), step => Assert.False(step.Capture));
    }
}
