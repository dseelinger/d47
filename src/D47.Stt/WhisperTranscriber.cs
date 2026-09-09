using System.Diagnostics;
using System.Text;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

namespace D47.Stt;

/// <summary>Whisper, loaded from a local ggml file.</summary>
public sealed class WhisperTranscriber : ISpeechTranscriber
{
    private readonly ILogger<WhisperTranscriber> _logger;
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>
    /// The last things Whisper.net said, kept so a failed load can replay them at a level that reaches
    /// the log file.
    /// </summary>
    private readonly Queue<string> _recentNativeLog = new();
    private const int RecentNativeLogLines = 48;

    /// <summary>The Whisper.net log subscription, held so <see cref="Dispose"/> can unhook it.</summary>
    private readonly IDisposable _nativeLogSubscription;

    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    /// <summary>
    /// The no-speech probe (#196): tiny.en, promptless, on its own semaphore so it can run beside the
    /// prompted pass.
    /// </summary>
    private WhisperFactory? _probeFactory;
    private WhisperProcessor? _probe;
    private string? _probeFrom;
    private bool _probeMissingSaid;
    private readonly SemaphoreSlim _probeOne = new(1, 1);

    /// <summary>The names the current processor was built to expect, or null for none.</summary>
    private string? _prompt;
    private string? _loadedFrom;
    private bool _disposed;

    public WhisperTranscriber(ILogger<WhisperTranscriber> logger)
    {
        _logger = logger;

        // Whisper.net logs through its own sink.
        _nativeLogSubscription = LogProvider.AddLogger((level, message) =>
        {
            var line = message?.TrimEnd();

            lock (_recentNativeLog)
            {
                _recentNativeLog.Enqueue($"{level}: {line}");

                while (_recentNativeLog.Count > RecentNativeLogLines)
                {
                    _recentNativeLog.Dequeue();
                }
            }

            _logger.Log(
                level switch
                {
                    WhisperLogLevel.Error => LogLevel.Error,
                    WhisperLogLevel.Warning => LogLevel.Warning,
                    WhisperLogLevel.Info => LogLevel.Debug,
                    _ => LogLevel.Trace,
                },
                "whisper: {Message}",
                line);
        });
    }

    public string? Model { get; private set; }

    public bool IsReady => _processor is not null;

    /// <summary>Why it is not ready, when it is not.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>
    /// Whether inference is actually on the GPU — assigned from the runtime library Whisper.net reports
    /// having loaded, never from the flag the caller asked with (#187).
    /// </summary>
    public bool UsingGpu { get; private set; }

    /// <summary>
    /// The flag the current load was asked with, kept apart from <see cref="UsingGpu"/> so the
    /// already-loaded check compares request against request.
    /// </summary>
    private bool _requestedGpu;

    /// <summary>Loads a model file, replacing whatever was loaded before.</summary>
    public bool Load(string modelPath, string modelId, bool useGpu)
    {
        if (_disposed)
        {
            Unavailable = "The transcriber has been shut down.";
            return false;
        }

        _one.Wait();

        try
        {
            if (_loadedFrom == modelPath && _requestedGpu == useGpu && _processor is not null)
            {
                return true;
            }

            UnloadCore();

            if (!File.Exists(modelPath))
            {
                Unavailable = $"The model file {Path.GetFileName(modelPath)} is not on disk.";
                return false;
            }

            lock (_recentNativeLog)
            {
                // Emptied so a failure's replay is this load's story, not a previous call's.
                _recentNativeLog.Clear();
            }

            try
            {
                // Which native libraries may load, named rather than left to the default order, which also
                // lists CUDA and OpenVino this build does not ship (#187).
                RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];

                _factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions
                {
                    UseGpu = useGpu,
                });

                _processor = Processor(_factory, prompt: null);
                _prompt = null;

                _loadedFrom = modelPath;
                Model = modelId;
                _requestedGpu = useGpu;
                UsingGpu = RunsOnGpu(useGpu, RuntimeOptions.LoadedLibrary);
                Unavailable = null;

                _logger.LogInformation(
                    "Loaded speech model {Model} on {Device}", modelId, UsingGpu ? "the GPU" : "the CPU");

                if (useGpu && !UsingGpu)
                {
                    // The load that succeeds on the wrong device: no Vulkan-capable driver, so the loader
                    // fell through to the CPU library and whisper loaded on it without error.
                    _logger.LogWarning(
                        "The GPU was asked for, but the native runtime that loaded is {Library} — "
                        + "inference is on the CPU.",
                        RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown");
                }

                return true;
            }
            catch (Exception ex)
            {
                // A throw here is the model or the file, not the device: a machine with no usable GPU falls
                // through to the CPU library and succeeds, which is handled above rather than here.
                Unavailable = useGpu
                    ? $"The model could not be loaded on the GPU: {ex.Message} "
                      + "Turn GPU off in Settings to run it on the CPU."
                    : $"The model could not be loaded: {ex.Message}";

                _logger.LogError(ex, "Could not load {Model}", modelId);

                string[] replay;

                lock (_recentNativeLog)
                {
                    replay = [.. _recentNativeLog];
                }

                if (replay.Length > 0)
                {
                    // At Error, deliberately: these lines went out at Debug and below as they happened, which
                    // the log file does not keep by default — and when a load fails, which paths the native
                    // loader actually probed is the diagnosis.
                    _logger.LogError(
                        "What Whisper.net reported while {Model} failed to load:\n{Replay}",
                        modelId,
                        string.Join('\n', replay));
                }

                UnloadCore();
                return false;
            }
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>
    /// Whether a successful load is actually on the GPU: it was asked for, and the native library that
    /// loaded is one that puts inference there (#187).
    /// </summary>
    internal static bool RunsOnGpu(bool requested, RuntimeLibrary? loaded) =>
        requested && loaded is RuntimeLibrary.Cuda or RuntimeLibrary.Cuda12 or RuntimeLibrary.Vulkan;

    /// <summary>One processor, optionally primed with the names to expect (remediation.md 10, item 17).</summary>
    private static WhisperProcessor Processor(WhisperFactory factory, string? prompt)
    {
        var builder = factory.CreateBuilder()
            .WithLanguage("en")

            // Or whisper.cpp uses four (#182).
            .WithThreads(ThreadsFor(Environment.ProcessorCount))

            // One segment callback per utterance is what d47 wants; token timestamps and per-token
            // probabilities are work with nothing reading them.
            .WithProbabilities();

        if (prompt is { Length: > 0 })
        {
            builder = builder.WithPrompt(prompt);
        }

        return builder.Build();
    }

    /// <summary>How many threads inference gets, from how many the machine has (#182).</summary>
    internal static int ThreadsFor(int processors) => Math.Clamp(processors - 4, 4, 16);

    /// <summary>
    /// Points the processor at the names this utterance might contain, rebuilding it only when they
    /// have changed (remediation.md 10, item 17).
    /// </summary>
    internal static string? Vocabulary(IReadOnlyList<string> properNouns)
    {
        var wanted = string.Join(
            ", ",
            properNouns.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()));

        return wanted.Length == 0 ? null : wanted;
    }

    private void Prime(IReadOnlyList<string> properNouns)
    {
        if (_factory is not { } factory)
        {
            return;
        }

        var wanted = Vocabulary(properNouns);

        if (string.Equals(wanted, _prompt, StringComparison.Ordinal))
        {
            return;
        }

        var replacement = Processor(factory, wanted);

        _processor?.Dispose();
        _processor = replacement;
        _prompt = wanted;

        _logger.LogDebug(
            "Biasing transcription towards {Count} names", properNouns.Count);
    }

    public async Task<Transcription> TranscribeAsync(
        Utterance utterance,
        IReadOnlyList<string> properNouns,
        CancellationToken cancellationToken = default)
    {
        if (_processor is null)
        {
            return new Transcription(string.Empty);
        }

        // One at a time.
        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Before the processor is read, because it is what may replace it.
            Prime(properNouns);

            if (_processor is not { } processor)
            {
                return new Transcription(string.Empty);
            }

            var text = new StringBuilder();

            // The model's own confidence, kept as the worst segment rather than the average (Phase 25, "Say
            // it, or type it").
            var confidence = 1d;

            await foreach (var segment in processor
                               .ProcessAsync(utterance.Samples, cancellationToken)
                               .ConfigureAwait(false))
            {
                text.Append(segment.Text);
                confidence = Math.Min(confidence, segment.Probability);
            }

            var transcribed = Clean(text.ToString());

            // The thread count is here because this line is where #182 was diagnosed from, and the one number
            // that turned out to explain it was the one the line did not carry.
            _logger.LogInformation(
                "Transcribed {Seconds:0.#}s of audio in {Elapsed}ms with {Nouns} name hints on {Threads} threads",
                utterance.Duration.TotalSeconds,
                stopwatch.ElapsedMilliseconds,
                properNouns.Count,
                ThreadsFor(Environment.ProcessorCount));

            return new Transcription(transcribed)
            {
                Elapsed = stopwatch.Elapsed,
                Model = Model,
                Confidence = confidence,
            };
        }
        catch (OperationCanceledException)
        {
            return new Transcription(string.Empty) { Elapsed = stopwatch.Elapsed, Model = Model };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return new Transcription(string.Empty) { Elapsed = stopwatch.Elapsed, Model = Model };
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>The file the probe runs on: tiny.en, looked for beside whatever model is loaded.</summary>
    private const string ProbeFileName = "ggml-tiny.en.bin";

    /// <summary>
    /// An unprompted second opinion on whether the clip contains speech at all (#196): the smallest
    /// <c>NoSpeechProbability</c> across the clip's segments, from tiny.en with no prompt — or null
    /// where no answer is possible: tiny.en not on disk, nothing loaded, the probe failing.
    /// </summary>
    public async Task<double?> NoSpeechAsync(Utterance utterance, CancellationToken cancellationToken = default)
    {
        if (_disposed || _loadedFrom is not { } loadedFrom)
        {
            return null;
        }

        await _probeOne.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_disposed || !EnsureProbe(loadedFrom))
            {
                return null;
            }

            var noSpeech = 1d;
            var segments = 0;

            await foreach (var segment in _probe!
                               .ProcessAsync(utterance.Samples, cancellationToken)
                               .ConfigureAwait(false))
            {
                noSpeech = Math.Min(noSpeech, segment.NoSpeechProbability);
                segments++;
            }

            return segments == 0 ? null : noSpeech;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The no-speech probe failed; the utterance is taken at its word");
            return null;
        }
        finally
        {
            _probeOne.Release();
        }
    }

    /// <summary>The probe's own load, lazy and beside the main model's file.</summary>
    private bool EnsureProbe(string loadedFrom)
    {
        var path = Path.Combine(Path.GetDirectoryName(loadedFrom) ?? string.Empty, ProbeFileName);

        if (_probe is not null && string.Equals(_probeFrom, path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _probe?.Dispose();
        _probe = null;
        _probeFactory?.Dispose();
        _probeFactory = null;
        _probeFrom = null;

        if (!File.Exists(path))
        {
            if (!_probeMissingSaid)
            {
                _probeMissingSaid = true;
                _logger.LogInformation(
                    "No {File} beside the loaded model, so a word hallucinated from silence goes unchecked (#196)",
                    ProbeFileName);
            }

            return false;
        }

        // Four threads flat: the probe's cost is already ~350 ms on tiny weights, and it runs beside the main
        // pass, which is the one the thread budget was measured for.
        _probeFactory = WhisperFactory.FromPath(path, new WhisperFactoryOptions());
        _probe = _probeFactory.CreateBuilder().WithLanguage("en").WithThreads(4).WithProbabilities().Build();
        _probeFrom = path;

        _logger.LogInformation("No-speech probe loaded from {File}", ProbeFileName);

        return true;
    }

    /// <summary>
    /// Whisper emits leading spaces on every segment, and emits bracketed annotations —
    /// "[BLANK_AUDIO]", "(wind blowing)" — for stretches with no speech in them.
    /// </summary>
    private static string Clean(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        // A transcript that is *entirely* an annotation is silence.
        if ((trimmed.StartsWith('[') && trimmed.EndsWith(']')) ||
            (trimmed.StartsWith('(') && trimmed.EndsWith(')') && !trimmed.AsSpan(1).Contains(')')))
        {
            return string.Empty;
        }

        return trimmed;
    }

    /// <summary>Drops the loaded model and goes back to not-ready, ready to <see cref="Load"/> again.</summary>
    public void Unload()
    {
        if (_disposed)
        {
            return;
        }

        _one.Wait();

        try
        {
            UnloadCore();
        }
        finally
        {
            _one.Release();
        }
    }

    private void UnloadCore()
    {
        _processor?.Dispose();
        _processor = null;

        // Or the next processor built for the same names would be skipped as already primed.
        _prompt = null;

        _factory?.Dispose();
        _factory = null;

        _loadedFrom = null;
        Model = null;
        UsingGpu = false;
        _requestedGpu = false;
    }

    /// <summary>Teardown, once, at the end of the process.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _one.Wait();

        try
        {
            UnloadCore();
        }
        finally
        {
            _one.Release();
            _one.Dispose();
            _nativeLogSubscription.Dispose();
        }

        // The probe under its own gate, after _disposed has stopped new callers: a wait here is at most one
        // in-flight probe finishing.
        _probeOne.Wait();

        try
        {
            _probe?.Dispose();
            _probe = null;
            _probeFactory?.Dispose();
            _probeFactory = null;
        }
        finally
        {
            _probeOne.Release();
            _probeOne.Dispose();
        }
    }
}
