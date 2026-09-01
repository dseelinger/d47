using System.Diagnostics;
using System.Text;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

namespace D47.Stt;

/// <summary>
/// Whisper, loaded from a local ggml file (architecture.md D3).
/// <para>
/// The model is loaded once and reused. Loading is the expensive part — hundreds of megabytes
/// off disk — and doing it per utterance would put that cost between the Commander releasing
/// the key and hearing anything back.
/// </para>
/// <para>
/// <b>Everything here is local.</b> The model file is on disk, inference runs in-process, and no
/// audio and no transcript leaves the machine. That is what keeps local-only operation reachable
/// with listening switched on.
/// </para>
/// </summary>
public sealed class WhisperTranscriber : ISpeechTranscriber
{
    private readonly ILogger<WhisperTranscriber> _logger;
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>
    /// The last things Whisper.net said, kept so a failed load can replay them at a level that
    /// reaches the log file. The loader narrates every candidate path it probes — but at Debug,
    /// below the default sink level, which is how "Native Library not found in default paths"
    /// shipped for weeks with nobody able to see which paths those were.
    /// </summary>
    private readonly Queue<string> _recentNativeLog = new();
    private const int RecentNativeLogLines = 48;

    /// <summary>
    /// The Whisper.net log subscription, held so <see cref="Dispose"/> can unhook it. The
    /// provider's list is static: an undisposed subscription keeps a dead transcriber reachable
    /// and still receiving every later instance's lines.
    /// </summary>
    private readonly IDisposable _nativeLogSubscription;

    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    /// <summary>
    /// The no-speech probe (#196): tiny.en, promptless, on its own semaphore so it can run
    /// beside the prompted pass. Kept apart from the main factory because the two answer
    /// different questions — the main pass answers "what was said", and this answers "was
    /// anything said at all" from weights the name hints never touch.
    /// </summary>
    private WhisperFactory? _probeFactory;
    private WhisperProcessor? _probe;
    private string? _probeFrom;
    private bool _probeMissingSaid;
    private readonly SemaphoreSlim _probeOne = new(1, 1);

    /// <summary>
    /// The names the current processor was built to expect, or null for none. Compared rather
    /// than recomputed, so a conversation in one system costs one processor
    /// (remediation.md 10, item 17).
    /// </summary>
    private string? _prompt;
    private string? _loadedFrom;
    private bool _disposed;

    public WhisperTranscriber(ILogger<WhisperTranscriber> logger)
    {
        _logger = logger;

        // Whisper.net logs through its own sink. Routed into Serilog so a native-side problem
        // lands in the same file as everything else rather than on a console nobody is watching.
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

    /// <summary>Why it is not ready, when it is not. A state to report, not an exception.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>
    /// Whether inference is actually on the GPU — assigned from the runtime library Whisper.net
    /// reports having loaded, never from the flag the caller asked with (#187). The two differed
    /// for months: no GPU native was shipped at all, the CPU runtime accepts a GPU request
    /// without complaint, and this property repeated the request back as if it were the result.
    /// A GPU runtime ships now, and this still reports the result rather than the request —
    /// a machine with no capable driver falls through to the CPU and is told so.
    /// </summary>
    public bool UsingGpu { get; private set; }

    /// <summary>
    /// The flag the current load was asked with, kept apart from <see cref="UsingGpu"/> so the
    /// already-loaded check compares request against request. Compared against the honest result,
    /// a GPU request landing on the CPU would look like a change and reload the model on every
    /// settings pass.
    /// </summary>
    private bool _requestedGpu;

    /// <summary>
    /// Loads a model file, replacing whatever was loaded before. Returns false with
    /// <see cref="Unavailable"/> set rather than throwing: no model is the normal condition on a
    /// fresh install, and it is a capability being off rather than a failure to handle.
    /// </summary>
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
                // Which native libraries may load, named rather than left to the default order,
                // which also lists CUDA and OpenVino this build does not ship (#187).
                //
                // Vulkan is offered whatever the setting says, and that is what makes the toggle
                // live. The library choice is process-wide and one-shot — Whisper.net keeps the
                // first one that loads for the rest of the run — so a CPU-only list here would
                // strand a Commander who switches the GPU on until they restarted d47, while
                // leaving Vulkan loadable costs nothing measurable: what reserves video memory is
                // the offload below, not the library. Measured at zero MB idle, and the memory
                // comes back when the setting goes off again.
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
                    // The load that succeeds on the wrong device: no Vulkan-capable driver, so
                    // the loader fell through to the CPU library and whisper loaded on it
                    // happily. This is the case the catch below was written for and can never
                    // see, because nothing threw (#187) — and it is the whole defect, since for
                    // months this path reported a GPU instead. Warning, not Information: the
                    // Commander asked for a device they are not getting.
                    _logger.LogWarning(
                        "The GPU was asked for, but the native runtime that loaded is {Library} — "
                        + "inference is on the CPU.",
                        RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown");
                }

                return true;
            }
            catch (Exception ex)
            {
                // A throw here is the model or the file, not the device: a machine with no usable
                // GPU falls through to the CPU library and succeeds, which is handled above
                // rather than here.
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
                    // At Error, deliberately: these lines went out at Debug and below as they
                    // happened, which the log file does not keep by default — and when a load
                    // fails, which paths the native loader actually probed is the diagnosis.
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
    /// Whether a successful load is actually on the GPU: it was asked for, <b>and</b> the native
    /// library that loaded is one that puts inference there (#187).
    /// <para>
    /// <b>Both halves are load-bearing.</b> The request alone proves nothing — the CPU runtime
    /// accepts <c>UseGpu = true</c> without complaint, which is how the request copied back
    /// reported a GPU for months while no GPU native shipped. The loaded library alone proves
    /// nothing either, now that Vulkan is offered on every load: it is loaded whether or not the
    /// Commander asked for the GPU, and with the setting off nothing is offloaded to it.
    /// </para>
    /// <para>
    /// CoreML and OpenVino count as CPU on purpose: understating what an exotic runtime delivers
    /// is recoverable, and claiming a GPU not in use is the lie this exists to end.
    /// </para>
    /// </summary>
    internal static bool RunsOnGpu(bool requested, RuntimeLibrary? loaded) =>
        requested && loaded is RuntimeLibrary.Cuda or RuntimeLibrary.Cuda12 or RuntimeLibrary.Vulkan;

    /// <summary>
    /// One processor, optionally primed with the names to expect (remediation.md 10, item 17).
    /// <para>
    /// The prompt is a builder setting rather than a per-call argument, so biasing towards a
    /// different set of names means a new processor. See <see cref="Prime"/> for why that is
    /// affordable.
    /// </para>
    /// </summary>
    private static WhisperProcessor Processor(WhisperFactory factory, string? prompt)
    {
        var builder = factory.CreateBuilder()
            .WithLanguage("en")

            // Or whisper.cpp uses four (#182). See ThreadsFor.
            .WithThreads(ThreadsFor(Environment.ProcessorCount))

            // One segment callback per utterance is what d47 wants; token timestamps
            // and per-token probabilities are work with nothing reading them.
            .WithProbabilities();

        if (prompt is { Length: > 0 })
        {
            builder = builder.WithPrompt(prompt);
        }

        return builder.Build();
    }

    /// <summary>
    /// How many threads inference gets, from how many the machine has
    /// (<a href="https://github.com/dseelinger/d47/issues/182">#182</a>).
    /// <para>
    /// <b>Whisper's own default is <c>min(4, hardware_concurrency)</c>, and nothing here used to
    /// override it</b> — so a 24-core machine transcribed on four threads. That was most of the
    /// three seconds #182 measured. Same clip, same model, same 40 hints, on the 24-core machine
    /// the figures were taken on:
    /// </para>
    /// <list type="table">
    /// <item><description>unset (four threads) — 2,939 ms</description></item>
    /// <item><description>eight — 1,754 ms</description></item>
    /// <item><description>twelve — 1,343 ms</description></item>
    /// <item><description>sixteen — 1,166 ms</description></item>
    /// <item><description>twenty-four — 1,155 ms</description></item>
    /// </list>
    /// <para>
    /// <b>Four is the floor and sixteen is the ceiling, and both are measured rather than
    /// chosen.</b> Four is what whisper.cpp already does, so a small machine keeps exactly the
    /// behaviour it has today and this can make nothing slower. Sixteen is where the curve
    /// flattens — twenty-four bought 11 ms over sixteen, which is 1% for eight more cores — so
    /// asking for more would be taking the machine for nothing. The same knee appeared on all
    /// three models: <c>tiny.en</c> 431→215 ms, <c>base.en</c> 889→401 ms.
    /// </para>
    /// <para>
    /// <b>And four cores are left alone on purpose.</b> d47 runs beside Elite, which is the whole
    /// point of it; a burst that takes every core for a second is how a transcription becomes a
    /// stutter in the headset — the same class of surprise as running the model on a GPU that is
    /// already drawing the game. Shorter and narrower beats longer and wider here: at sixteen
    /// threads the burst is both less than half as long and still leaves the game a machine.
    /// </para>
    /// </summary>
    internal static int ThreadsFor(int processors) => Math.Clamp(processors - 4, 4, 16);

    /// <summary>
    /// Points the processor at the names this utterance might contain, rebuilding it only when
    /// they have changed (remediation.md 10, item 17).
    /// <para>
    /// <b>This is the half that was missing.</b> <c>properNouns</c> has been a parameter of this
    /// method since Phase 6, and the list has been built from the journal, capped and handed over
    /// on every utterance the whole time — and then counted in a log line and dropped on the
    /// floor. The log said "with 23 name hints" while nothing was biased by anything, which is
    /// the worst shape a gap can have: it reports as working.
    /// </para>
    /// <para>
    /// <b>Rebuilt on change rather than per utterance.</b> Whisper takes an initial prompt when
    /// the processor is built, so a different set of names is a different processor. The names
    /// come from where the Commander is and what they fly, so the set is stable across a
    /// conversation and turns over on a jump — a handful of rebuilds an hour, against one per
    /// utterance if this were unconditional. The factory and the loaded weights are untouched
    /// either way; only the processor around them is remade.
    /// </para>
    /// </summary>
    /// <summary>
    /// The names as an initial prompt, or null when there are none.
    /// <para>
    /// Comma-separated, which is how an initial prompt is meant to carry a vocabulary: Whisper
    /// reads it as text that came just before the audio, and a list of names is exactly the
    /// context that makes the next name likelier. Blanks are dropped rather than joined into
    /// ", , " — a Commander whose ship has no name should not spend prompt on the fact.
    /// </para>
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

        // One at a time. Whisper's processor is not reentrant, and two utterances in flight is
        // not a state worth supporting — the Commander cannot say two things at once.
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

            // The model's own confidence, kept as the worst segment rather than the average
            // (Phase 25, "Say it, or type it"). One badly-heard word in an otherwise
            // clear sentence is exactly the failure this figure exists to catch — a system name
            // among ordinary English — and an average over the sentence buries it.
            var confidence = 1d;

            await foreach (var segment in processor
                               .ProcessAsync(utterance.Samples, cancellationToken)
                               .ConfigureAwait(false))
            {
                text.Append(segment.Text);
                confidence = Math.Min(confidence, segment.Probability);
            }

            var transcribed = Clean(text.ToString());

            // The thread count is here because this line is where #182 was diagnosed from, and
            // the one number that turned out to explain it was the one the line did not carry.
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
    /// An unprompted second opinion on whether the clip contains speech at all
    /// (<a href="https://github.com/dseelinger/d47/issues/196">#196</a>): the smallest
    /// <c>NoSpeechProbability</c> across the clip's segments, from tiny.en with no prompt — or
    /// null where no answer is possible: tiny.en not on disk, nothing loaded, the probe failing.
    /// Null means "taken at its word", never "refused".
    /// <para>
    /// <b>Unprompted is the whole design, and it is measured rather than argued</b>
    /// (spike/NoSpeechProbe). The name-hint prompt is what turns silence into hint-vocabulary
    /// words <em>and</em> what destroys the prompted pass's own no-speech signal — 0.96
    /// unprompted against 0.0001 primed, on the same room tone — so the ruling has to come from
    /// a pass the prompt never touches. The populations it separates: real speech 0.017–0.26
    /// against room tone 0.946–0.958, at a flat ~350 ms whatever the clip length. The smallest
    /// segment is the aggregate because a clip is speech if any segment is — a real sentence
    /// with a silent tail must not be refused for its tail.
    /// </para>
    /// <para>
    /// Its own semaphore rather than <see cref="_one"/>, so a caller runs it in parallel with
    /// the prompted pass and the gate costs nothing in latency.
    /// </para>
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

    /// <summary>
    /// The probe's own load, lazy and beside the main model's file. Missing is a state to
    /// mention once rather than a failure: the gate simply does not exist on that install.
    /// </summary>
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

        // Four threads flat: the probe's cost is already ~350 ms on tiny weights, and it runs
        // beside the main pass, which is the one the thread budget was measured for.
        _probeFactory = WhisperFactory.FromPath(path, new WhisperFactoryOptions());
        _probe = _probeFactory.CreateBuilder().WithLanguage("en").WithThreads(4).WithProbabilities().Build();
        _probeFrom = path;

        _logger.LogInformation("No-speech probe loaded from {File}", ProbeFileName);

        return true;
    }

    /// <summary>
    /// Whisper emits leading spaces on every segment, and emits bracketed annotations —
    /// "[BLANK_AUDIO]", "(wind blowing)" — for stretches with no speech in them. Those are
    /// descriptions of the audio rather than things the Commander said, and passing one to the
    /// turn loop as a question is how d47 ends up answering the sound of a fan.
    /// </summary>
    private static string Clean(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        // A transcript that is *entirely* an annotation is silence. One that merely contains an
        // aside still has words in it and is left alone.
        if ((trimmed.StartsWith('[') && trimmed.EndsWith(']')) ||
            (trimmed.StartsWith('(') && trimmed.EndsWith(')') && !trimmed.AsSpan(1).Contains(')')))
        {
            return string.Empty;
        }

        return trimmed;
    }

    /// <summary>
    /// Drops the loaded model and goes back to not-ready, ready to <see cref="Load"/> again.
    /// <para>
    /// This is what "no model is selected" means, and it is deliberately not
    /// <see cref="Dispose"/>: the host owns one transcriber for the life of the process and
    /// merely opens and closes it as the setting changes. Disposing on a settings change and
    /// then being asked to load again is how the object ends up unusable while still referenced.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Teardown, once, at the end of the process. Idempotent: a second call is a no-op rather
    /// than an <see cref="ObjectDisposedException"/> out of the semaphore.
    /// </summary>
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

        // The probe under its own gate, after _disposed has stopped new callers: a wait here is
        // at most one in-flight probe finishing. It survives Unload on purpose — a model change
        // does not invalidate tiny weights — so this is the one place it is ever torn down.
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
