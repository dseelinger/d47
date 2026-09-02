using D47.App.Logging;
using D47.Core;
using D47.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A log-level row moves the code it names, driven through the real logging configuration
/// (bugs.md 1).
/// <para>
/// The sibling test in Core asserts that every prefix names a namespace that exists. This one
/// asserts the half that a list of strings cannot: that Serilog, configured the way d47
/// configures it, actually routes an event to the switch the settings row is holding — including
/// where two subsystems both claim a logger and the more specific one has to win.
/// </para>
/// </summary>
public class LogLevelRoutingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-log-routing",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Captures whatever survives the level switches, so the assertion is about routing.</summary>
    private sealed class Captured : ILogEventSink
    {
        public List<string> Sources { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            var source = logEvent.Properties.TryGetValue("SourceContext", out var value)
                ? value.ToString().Trim('"')
                : "(none)";

            lock (Sources)
            {
                Sources.Add(source);
            }
        }
    }

    /// <summary>
    /// The real thing: <see cref="LoggingSetup.Create"/> builds the overrides, and this only adds
    /// somewhere to see what came through. A test that rebuilt the configuration itself would be
    /// asserting about a copy of the arrangement rather than the arrangement.
    /// </summary>
    private (Serilog.ILogger Logger, SerilogVerbosityControl Verbosity, Captured Sink) Configured()
    {
        var verbosity = new SerilogVerbosityControl();
        var sink = new Captured();

        Directory.CreateDirectory(_root);

        var logger = LoggingSetup.Create(new AppPaths(_root), verbosity);

        // The sink cannot be added after the fact, so the captured view is a second logger over
        // the same switches — which is what is actually under test.
        var watched = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(verbosity.Default)
            .Enrich.FromLogContext();

        foreach (var (subsystem, prefixes) in Subsystems.SourcePrefixes)
        {
            var level = verbosity.SwitchFor(subsystem);

            foreach (var prefix in prefixes)
            {
                watched = watched.MinimumLevel.Override(prefix, level);
            }
        }

        return (watched.WriteTo.Sink(sink).CreateLogger(), verbosity, sink);
    }

    /// <summary>
    /// Turning a subsystem down silences all of it. Before the fix this silenced nothing at all
    /// for Voice, Input and LLM: their prefixes named namespaces that did not exist, so the
    /// override was never applied and every line came through at the default.
    /// </summary>
    [Theory]
    [InlineData(Subsystems.Voice, "D47.App.Voice.VoicePipeline")]
    [InlineData(Subsystems.Voice, "D47.Core.Audio.SpeechPipeline")]
    [InlineData(Subsystems.Voice, "D47.Core.Listening.ListenGate")]
    [InlineData(Subsystems.Input, "D47.App.Input.ScancodeInjector")]
    [InlineData(Subsystems.Input, "D47.Core.Input.BindsReader")]
    [InlineData(Subsystems.Llm, "D47.Llm.AnthropicLlmProvider")]
    [InlineData(Subsystems.Vr, "D47.Core.Vr.VrLifecycle")]
    [InlineData(Subsystems.Journal, "D47.Core.Journal.JournalReader")]
    public void TurningASubsystemDownSilencesIt(string subsystem, string source)
    {
        var (logger, verbosity, sink) = Configured();

        logger.ForContext(Constants.SourceContextPropertyName, source).Information("before");
        Assert.Contains(source, sink.Sources);

        sink.Sources.Clear();
        verbosity.Set(subsystem, LogLevel.Error);

        logger.ForContext(Constants.SourceContextPropertyName, source).Information("after");
        Assert.DoesNotContain(source, sink.Sources);

        // And it is the level rather than the logger that stopped: an error still gets through.
        logger.ForContext(Constants.SourceContextPropertyName, source).Error("still loud");
        Assert.Contains(source, sink.Sources);
    }

    /// <summary>
    /// <c>D47.App</c> claims every app logger and <c>D47.App.Voice</c> claims the speech
    /// pipeline, so both match. The design relies on Serilog applying the most specific
    /// override — if the broad one won instead, the Voice row would control nothing again by a
    /// different route, and just as quietly.
    /// </summary>
    [Fact]
    public void TheNarrowerSubsystemOwnsALoggerBothClaim()
    {
        const string source = "D47.App.Voice.VoicePipeline";
        var (logger, verbosity, sink) = Configured();

        // App silent, Voice loud. If App won, nothing would arrive.
        verbosity.Set(Subsystems.App, LogLevel.Error);
        verbosity.Set(Subsystems.Voice, LogLevel.Debug);

        logger.ForContext(Constants.SourceContextPropertyName, source).Debug("the loop said something");
        Assert.Contains(source, sink.Sources);

        // And the other way round: Voice silent, App loud.
        sink.Sources.Clear();
        verbosity.Set(Subsystems.App, LogLevel.Debug);
        verbosity.Set(Subsystems.Voice, LogLevel.Error);

        logger.ForContext(Constants.SourceContextPropertyName, source).Debug("and again");
        Assert.DoesNotContain(source, sink.Sources);

        // The app's own loggers were following the App row throughout, which is what makes the
        // two rows independent rather than one shadowing the other.
        logger.ForContext(Constants.SourceContextPropertyName, "D47.App.AppHost").Debug("the app said something");
        Assert.Contains("D47.App.AppHost", sink.Sources);
    }
}
