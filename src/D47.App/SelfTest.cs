using D47.App.Logging;
using D47.Audio;
using D47.Core;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace D47.App;

/// <summary>
/// <c>d47.exe --selftest</c>: prove the build that will actually ship can transcribe.
/// <para>
/// This exists because of a bug class nothing else can catch. Every automated test runs against
/// build output, where Whisper's natives sit in <c>bin\...\runtimes\win-x64\</c> and resolve
/// fine; <c>dotnet publish</c> lays files out differently, and a broken layout ships silently —
/// which is exactly how a d47 whose transcriber had never once worked was released for weeks.
/// The release workflow runs this against the published exe and refuses to release on a
/// non-zero exit.
/// </para>
/// <para>
/// The exit code is the whole contract: d47 is a windowed build, so stdout only goes somewhere
/// when the caller redirects it. Pass = the real <see cref="WhisperTranscriber"/> loads a real
/// model from <c>data\models\</c> and pushes a second of silence through it without anything
/// logging an error. The model is user data, not bundle content, so the caller has to put one
/// on disk first — a missing model is a failure here, not a skip, because a gate that can be
/// skipped by forgetting a step is not a gate.
/// </para>
/// <para>
/// Phase 21 added a third thing to the same gate, and it is not a native at all: the Windows SDK
/// projection behind <c>Windows.Gaming.Input</c>. It reaches a published build as a managed
/// assembly inside the single-file bundle plus a COM activation, which is a third layout the
/// automated tests cannot tell from <c>bin\</c> — and a projection that fails to activate takes
/// out every HOTAS switch silently, because the reader is written to treat "no controllers" as
/// the normal state. A machine with nothing plugged in still passes; a projection that did not
/// load does not.
/// </para>
/// <para>
/// Phase 13 added a second native to the same gate, for the same reason and against the same
/// bug: <see cref="EchoCanceller"/> loads <c>webrtc-apm</c>, which reaches a published build
/// through the single-file bundle's self-extraction rather than through the loose layout Whisper
/// needs. Nothing in the automated tests can tell those two layouts apart, and a canceller that
/// silently fails to start does not stop d47 — it just quietly leaves hands-free listening
/// half-duplex, which is precisely the kind of failure that ships.
/// </para>
/// </summary>
internal static class SelfTest
{
    internal const string Flag = "--selftest";

    private const int PassedExitCode = 0;
    private const int CrashedExitCode = 1;
    private const int LoadFailedExitCode = 2;
    private const int NoModelExitCode = 3;
    private const int ErrorsLoggedExitCode = 4;
    private const int NativeLoadFailedExitCode = 5;
    private const int ControllerProjectionFailedExitCode = 6;
    private const int CompositionFailedExitCode = 7;

    /// <summary>The GPU natives did not make it into the payload (#187).</summary>
    private const int GpuRuntimeMissingExitCode = 8;

    public static int Run()
    {
        var paths = AppPaths.ForRunningBuild();
        paths.EnsureCreated();

        // Everything, down to Trace: Whisper.net narrates each candidate path it probes at
        // Debug and below, and when this fails, those lines are the diagnosis.
        var verbosity = new SerilogVerbosityControl();
        verbosity.SetDefault(LogLevel.Trace);

        Log.Logger = LoggingSetup.Create(paths, verbosity);

        try
        {
            using var loggers = new CountingLoggerFactory(new SerilogLoggerFactory(Log.Logger));

            return Check(paths, loggers);
        }
        catch (Exception ex)
        {
            Report($"SELFTEST FAIL: {ex}");
            return CrashedExitCode;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static int Check(AppPaths paths, CountingLoggerFactory loggers)
    {
        var logger = loggers.CreateLogger("D47.App.SelfTest");

        logger.LogInformation("Self-test: D47 {Version} in {Root}", BuildInfo.Full, paths.InstallRoot);

        // Stated up front, pass or fail, because "which files were actually beside the exe"
        // is the first question anyone debugging a load failure asks.
        //
        // Both folders, since #187: the GPU runtime lives in its own one, and a publish that
        // dropped it would not fail anything. Whisper falls through to the CPU library and
        // transcribes perfectly well — so the toggle would quietly stop reaching a GPU, which is
        // the exact defect #187 fixed, arriving the second time as a packaging accident.
        foreach (var folder in new[]
                 {
                     Path.Combine(paths.InstallRoot, "runtimes", "win-x64"),
                     Path.Combine(paths.InstallRoot, "runtimes", "vulkan", "win-x64"),
                 })
        {
            logger.LogInformation(
                "Native folder {Folder}: {Files}",
                folder,
                Directory.Exists(folder)
                    ? string.Join(", ", Directory.EnumerateFiles(folder).Select(Path.GetFileName))
                    : "absent");
        }

        // Named rather than merely listed. The GPU natives are the ones nothing else would
        // notice missing, and a release gate that reports "absent" without failing is a log
        // line nobody reads.
        var vulkan = Path.Combine(paths.InstallRoot, "runtimes", "vulkan", "win-x64", "ggml-vulkan-whisper.dll");

        if (!File.Exists(vulkan))
        {
            Report(
                $"SELFTEST FAIL: the GPU runtime is missing from this payload ({vulkan}). "
                + "Transcription would still work, on the CPU, with the GPU setting doing nothing.");
            return GpuRuntimeMissingExitCode;
        }

        // Cheapest of the three and the only one that touches no user data at all, so it runs
        // first and its answer stands even on a machine with no controllers.
        using (var controllers = new Input.HotasControllers(loggers.CreateLogger<Input.HotasControllers>()))
        {
            _ = controllers.Poll();

            if (controllers.Fault is { } fault)
            {
                Report($@"SELFTEST FAIL: {fault} See data\logs for what the projection reported.");
                return ControllerProjectionFailedExitCode;
            }

            logger.LogInformation(
                "Windows.Gaming.Input reachable; {Count} controller interface(s) reported so far",
                controllers.Interfaces);
        }

        // Before the model lookup, because it is the cheaper of the two checks, it depends on no
        // user data, and a machine missing the Visual C++ runtime fails here in a way that
        // explains the transcriber's failure as well.
        if (CheckEchoCancellation(loggers, logger) is { } echoFailure)
        {
            Report(echoFailure);
            return NativeLoadFailedExitCode;
        }

        var models = Path.Combine(paths.Data, "models");

        // The smallest, because any ggml file proves the natives load and the small ones prove
        // it fastest. Straight off the disk rather than through the catalog: the catalog is
        // product policy, and this is a plumbing check.
        var modelPath = (Directory.Exists(models)
                ? Directory.EnumerateFiles(models, "ggml-*.bin")
                : Enumerable.Empty<string>())
            .OrderBy(file => new FileInfo(file).Length)
            .FirstOrDefault();

        if (modelPath is null)
        {
            Report($"SELFTEST FAIL: no ggml-*.bin under {models}; put a speech model there first.");
            return NoModelExitCode;
        }

        var modelId = Path.GetFileNameWithoutExtension(modelPath)["ggml-".Length..];

        using var transcriber = new WhisperTranscriber(loggers.CreateLogger<WhisperTranscriber>());

        if (!transcriber.Load(modelPath, modelId, useGpu: false))
        {
            Report($"SELFTEST FAIL: {transcriber.Unavailable} See data\\logs for what the loader probed.");
            return LoadFailedExitCode;
        }

        // One second of silence, through the same call push-to-talk uses. The words do not
        // matter — silence cleans to an empty transcript — surviving inference does.
        var heard = transcriber
            .TranscribeAsync(new Utterance(new float[16000], 16000), properNouns: [])
            .GetAwaiter()
            .GetResult();

        // TranscribeAsync reports trouble by logging an error and returning empty, which is
        // right for push-to-talk and wrong for a gate; the count is what makes failure loud.
        if (loggers.Errors > 0)
        {
            Report($"SELFTEST FAIL: {loggers.Errors} error(s) logged; see data\\logs.");
            return ErrorsLoggedExitCode;
        }

        // Last, and much the widest. Everything above proves one native or one projection loads
        // in this layout; this proves the app can be *composed* — every capability registered,
        // the settings surface bound, the callouts and tick subscribers wired. That is where
        // 0.76.0 and 0.76.1 died, on every launch, before a window existed (#78).
        //
        // It runs the real AppHost.Compose rather than rebuilding the registry here, and that is
        // the whole point: a copy of composition is exactly what let #78 through. The test
        // surfaces mirrored it, the mirror was missing the rows that broke it, and 5,042 tests
        // passed a build that could not start. A third mirror would buy nothing.
        //
        // Gated on nothing throwing rather than on the error count, unlike the transcriber check
        // above. Composition legitimately logs errors on a machine with no keys and no model, and
        // a gate that fails on a fresh install is one that gets switched off.
        try
        {
            using var composed = AppHost.Compose();

            logger.LogInformation(
                "Composed: {Capabilities} capabilities, {Tools} tools, {Rows} settings rows",
                composed.Capabilities.All.Count,
                composed.Capabilities.ToolNames.Count(),
                composed.Settings.Sections.Sum(section => section.Rows.Count));
        }
        catch (Exception ex)
        {
            Report($"SELFTEST FAIL: the app could not be composed, so it would not start: {ex}");
            return CompositionFailedExitCode;
        }

        Report(
            $"SELFTEST OK: {modelId} loaded and transcribed 1s of silence "
            + $"in {heard.Elapsed.TotalMilliseconds:0} ms, echo cancellation loaded, "
            + "Windows.Gaming.Input activated, and the app composed.");
        return PassedExitCode;
    }

    /// <summary>
    /// Proves <c>webrtc-apm</c> loads and processes a frame in this layout, and returns null when
    /// it does.
    /// <para>
    /// A frame rather than only a constructor, because the constructor is managed and the
    /// P/Invoke is where a missing native actually surfaces. One 10 ms frame of silence through
    /// the same <see cref="ICaptureSink.Write"/> the microphone uses is enough to reach it.
    /// </para>
    /// </summary>
    private static string? CheckEchoCancellation(CountingLoggerFactory loggers, ILogger logger)
    {
        var reached = 0;

        using var canceller = new EchoCanceller(
            new Counting(() => reached++),
            new NoRender(),
            16_000,
            loggers.CreateLogger<EchoCanceller>());

        canceller.Start();

        if (!canceller.IsActive)
        {
            return $@"SELFTEST FAIL: {canceller.Unavailable} See data\logs for what the loader probed.";
        }

        canceller.Write(new float[160]);

        if (reached == 0)
        {
            return "SELFTEST FAIL: echo cancellation started but processed no audio.";
        }

        logger.LogInformation("Echo cancellation loaded and processed a frame");
        return null;
    }

    /// <summary>A gate that only counts, for the frame above.</summary>
    private sealed class Counting(Action onWrite) : ICaptureSink
    {
        public void Write(ReadOnlySpan<float> samples) => onWrite();

        public void Reset()
        {
        }
    }

    /// <summary>
    /// Nothing is playing during a self-test, so the reference never fires and the forward path
    /// is what is being proved. Empty accessors rather than a field, so there is no event here
    /// that is declared and never raised.
    /// </summary>
    private sealed class NoRender : D47.Core.Audio.IRenderReferenceTap
    {
        public event Action<D47.Core.Audio.RenderReferenceFrame>? Rendered
        {
            add { }
            remove { }
        }
    }

    /// <summary>
    /// The verdict, to both audiences: stdout for the workflow log, Serilog for the file that
    /// gets read when the workflow log has scrolled away.
    /// </summary>
    private static void Report(string line)
    {
        Console.WriteLine(line);
        Log.Information("{SelfTest}", line);
    }

    /// <summary>
    /// Counts errors on their way through to Serilog. The transcriber deliberately never
    /// throws — failure is a state it logs and reports — so "did anything log an error" is the
    /// selftest's pass condition, not exceptions.
    /// </summary>
    private sealed class CountingLoggerFactory(ILoggerFactory inner) : ILoggerFactory
    {
        private int _errors;

        public int Errors => Volatile.Read(ref _errors);

        public ILogger CreateLogger(string categoryName) => new CountingLogger(inner.CreateLogger(categoryName), this);

        public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

        public void Dispose() => inner.Dispose();

        private sealed class CountingLogger(ILogger inner, CountingLoggerFactory owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

            public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error)
                {
                    Interlocked.Increment(ref owner._errors);
                }

                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
