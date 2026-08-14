using System.Diagnostics;
using System.Text;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;
using Whisper.net;
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

    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string? _loadedFrom;
    private bool _disposed;

    public WhisperTranscriber(ILogger<WhisperTranscriber> logger)
    {
        _logger = logger;

        // Whisper.net logs through its own sink. Routed into Serilog so a native-side problem
        // lands in the same file as everything else rather than on a console nobody is watching.
        LogProvider.AddLogger((level, message) =>
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

    /// <summary>Whether the loaded model is running on the GPU.</summary>
    public bool UsingGpu { get; private set; }

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
            if (_loadedFrom == modelPath && UsingGpu == useGpu && _processor is not null)
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
                _factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions
                {
                    UseGpu = useGpu,
                });

                _processor = _factory.CreateBuilder()
                    .WithLanguage("en")

                    // One segment callback per utterance is what d47 wants; token timestamps
                    // and per-token probabilities are work with nothing reading them.
                    .WithProbabilities()
                    .Build();

                _loadedFrom = modelPath;
                Model = modelId;
                UsingGpu = useGpu;
                Unavailable = null;

                _logger.LogInformation(
                    "Loaded speech model {Model} on {Device}", modelId, useGpu ? "the GPU" : "the CPU");

                return true;
            }
            catch (Exception ex)
            {
                // The GPU case this most often means: the CUDA runtime is not present. Reported
                // as itself rather than as a silent fall back to CPU — a GPU toggle that quietly
                // does nothing is the setting the checklist calls hardest to diagnose.
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
            if (_processor is not { } processor)
            {
                return new Transcription(string.Empty);
            }

            var text = new StringBuilder();

            await foreach (var segment in processor
                               .ProcessAsync(utterance.Samples, cancellationToken)
                               .ConfigureAwait(false))
            {
                text.Append(segment.Text);
            }

            var transcribed = Clean(text.ToString());

            _logger.LogInformation(
                "Transcribed {Seconds:0.#}s of audio in {Elapsed}ms with {Nouns} name hints",
                utterance.Duration.TotalSeconds,
                stopwatch.ElapsedMilliseconds,
                properNouns.Count);

            return new Transcription(transcribed)
            {
                Elapsed = stopwatch.Elapsed,
                Model = Model,
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

        _factory?.Dispose();
        _factory = null;

        _loadedFrom = null;
        Model = null;
        UsingGpu = false;
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
        }
    }
}
