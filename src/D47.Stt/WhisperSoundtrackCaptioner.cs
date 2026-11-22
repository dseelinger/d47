using D47.Core.Listening;
using D47.Core.Viewing;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace D47.Stt;

/// <summary>
/// Writes captions with a Whisper processor of its own, loaded from the Listener's model file on the first stretch.
/// Each cue is one segment of at most two caption lines, split at a word.
/// </summary>
/// <param name="modelFile">The Listener's model file and whether it runs on the GPU, or null when none is on disk.</param>
/// <param name="listenerBusy">Whether the Listener is listening or transcribing, which writing gives way to.</param>
public sealed class WhisperSoundtrackCaptioner(
    Func<(string Path, bool UseGpu)?> modelFile,
    Func<bool> listenerBusy,
    ILogger<WhisperSoundtrackCaptioner> logger,
    TimeProvider? time = null) : ISoundtrackCaptioner, IDisposable
{
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private (string Path, bool UseGpu)? _loaded;
    private bool _disposed;

    public string? Unavailable =>
        modelFile() is not null
            ? null
            : "Writing captions needs a speech model on this computer. Choose one in Settings › Speech recognition.";

    public async Task<IReadOnlyList<SubtitleCue>> CaptionAsync(Utterance sound, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sound);

        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var processor = Load()
                ?? throw new InvalidOperationException(Unavailable ?? "The speech model could not be loaded to write captions.");

            return await GivesWayToListener
                .RunAsync(token => WriteAsync(processor, sound, token), listenerBusy, _time, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _one.Release();
        }
    }

    public void Release()
    {
        if (_disposed)
        {
            return;
        }

        _one.Wait();

        try
        {
            Unload();
        }
        finally
        {
            _one.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Release();
        _disposed = true;
        _one.Dispose();
    }

    private static async Task<IReadOnlyList<SubtitleCue>> WriteAsync(WhisperProcessor processor, Utterance sound, CancellationToken token)
    {
        var cues = new List<SubtitleCue>();

        await foreach (var segment in processor.ProcessAsync(sound.Samples, token).ConfigureAwait(false))
        {
            var text = WhisperTranscriber.Clean(segment.Text);

            if (text.Length > 0 && segment.End > segment.Start)
            {
                cues.Add(new SubtitleCue(segment.Start, segment.End, text));
            }
        }

        return cues;
    }

    /// <summary>The processor for the Listener's model file, loading it when the file or device changed; null with no file.</summary>
    private WhisperProcessor? Load()
    {
        var model = modelFile();

        if (model is null)
        {
            Unload();
            return null;
        }

        if (_processor is not null && _loaded == model)
        {
            return _processor;
        }

        Unload();

        try
        {
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];

            _factory = WhisperFactory.FromPath(model.Value.Path, new WhisperFactoryOptions { UseGpu = model.Value.UseGpu });
            _processor = _factory.CreateBuilder()
                .WithLanguage("en")
                .WithThreads(WhisperTranscriber.ThreadsFor(Environment.ProcessorCount))
                .WithTokenTimestamps()
                .SplitOnWord()
                .WithMaxSegmentLength(Caption.CharactersPerLine * 2)
                .Build();
            _loaded = model;

            logger.LogInformation("Loaded {File} to write captions", Path.GetFileName(model.Value.Path));
            return _processor;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load {File} to write captions", Path.GetFileName(model.Value.Path));
            Unload();
            return null;
        }
    }

    private void Unload()
    {
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
        _loaded = null;
    }
}
