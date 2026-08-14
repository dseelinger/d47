using D47.App.Logging;
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
/// </summary>
internal static class SelfTest
{
    internal const string Flag = "--selftest";

    private const int PassedExitCode = 0;
    private const int CrashedExitCode = 1;
    private const int LoadFailedExitCode = 2;
    private const int NoModelExitCode = 3;
    private const int ErrorsLoggedExitCode = 4;

    public static int Run()
    {
        var paths = AppPaths.BesideExecutable();
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
        var natives = Path.Combine(paths.InstallRoot, "runtimes", "win-x64");
        logger.LogInformation(
            "Native folder {Folder}: {Files}",
            natives,
            Directory.Exists(natives)
                ? string.Join(", ", Directory.EnumerateFiles(natives).Select(Path.GetFileName))
                : "absent");

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

        Report(
            $"SELFTEST OK: {modelId} loaded and transcribed 1s of silence "
            + $"in {heard.Elapsed.TotalMilliseconds:0} ms.");
        return PassedExitCode;
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
