using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace D47.Audio;

/// <summary>
/// The one output device, driven by the arbiter above it (architecture.md D7).
/// <para>
/// The mixer is what makes "one arbiter" physically true: cues, speech and the bed are three
/// inputs to one graph feeding one <see cref="WasapiOut"/>, so there is no second path a line
/// could come out of in the wrong voice or at the wrong level.
/// </para>
/// </summary>
public sealed class WasapiAudioSink : IAudioSink, IDisposable
{
    /// <summary>
    /// The graph runs at 48 kHz stereo float regardless of what the clips are, because that
    /// is what shared-mode WASAPI wants on essentially every device this will meet, and one
    /// conversion at the edge beats a resampler per input.
    /// </summary>
    private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);

    private readonly ILogger<WasapiAudioSink> _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<long, Input> _inputs = [];
    private readonly MixingSampleProvider _mixer;
    private readonly RenderTap _tap;

    private WasapiOut? _output;
    private bool _disposed;

    private sealed record Input(VolumeSampleProvider Volume, ISampleProvider Root);

    public WasapiAudioSink(ILogger<WasapiAudioSink> logger)
    {
        _logger = logger;

        _mixer = new MixingSampleProvider(MixFormat)
        {
            // Without this the mixer stops when the last input ends, and the next Play would
            // be silent until the device were restarted. A permanently-running silent mixer
            // is the normal shape for a graph that is idle most of the time.
            ReadFully = true,
        };

        _mixer.MixerInputEnded += OnMixerInputEnded;
        _tap = new RenderTap(_mixer);
    }

    public IRenderReferenceTap ReferenceTap => _tap;

    public event Action<long>? Finished;

    /// <summary>
    /// The output devices to offer in settings. Friendly names, keyed by the device id that
    /// actually gets stored — a name is not stable across driver updates.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name)> Devices()
    {
        using var enumerator = new MMDeviceEnumerator();

        return
        [
            .. enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(device => (device.ID, device.FriendlyName)),
        ];
    }

    /// <summary>
    /// Opens the device. Separate from the constructor because it is the part that can fail
    /// on a machine with no working output, and a sink that exists but cannot render is
    /// exactly the "capability is off" state rather than a startup crash (Phase 3).
    /// </summary>
    public void Open(string? deviceId = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_output is not null)
            {
                return;
            }

            var device = Resolve(deviceId);

            // Shared mode, so d47 never takes exclusive hold of the Commander's output — the
            // game is the thing that matters on that device. 60 ms of latency is inaudible
            // for speech and forgiving of a busy machine.
            _output = device is null
                ? new WasapiOut(AudioClientShareMode.Shared, useEventSync: true, latency: 60)
                : new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, latency: 60);

            _output.Init(_tap);
            _output.Play();

            _logger.LogInformation(
                "Audio output open on {Device}",
                device?.FriendlyName ?? "the system default device");
        }
    }

    /// <summary>
    /// Moves to another output device. Closing and reopening is the only way WASAPI offers, so
    /// it happens on a settings change rather than making the Commander restart for it
    /// (Phase 4). The mixer graph survives — only the device behind it changes.
    /// </summary>
    public void Reopen(string? deviceId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _mixer.RemoveAllMixerInputs();
            _inputs.Clear();
            _output?.Dispose();
            _output = null;
        }

        Open(deviceId);
    }

    public void Play(PlaybackRequest request)
    {
        lock (_gate)
        {
            if (_output is null)
            {
                _logger.LogDebug("Play({Id}) with no output open; dropped", request.Id);
                Finished?.Invoke(request.Id);
                return;
            }

            ISampleProvider source = new ClipSampleProvider(request.Clip, request.Loop);

            if (source.WaveFormat.Channels != MixFormat.Channels)
            {
                source = new MonoToStereoSampleProvider(source);
            }

            var volume = new VolumeSampleProvider(source) { Volume = request.Gain };
            var tracked = new TrackedSampleProvider(request.Id, volume);

            _inputs[request.Id] = new Input(volume, tracked);
            _mixer.AddMixerInput(tracked);
        }
    }

    public void Stop(long playbackId)
    {
        lock (_gate)
        {
            if (_inputs.Remove(playbackId, out var input))
            {
                // Removing the input is the stop. MixerInputEnded does not fire for a removal,
                // which is what the arbiter wants — it already knows, since it asked.
                _mixer.RemoveMixerInput(input.Root);
            }
        }
    }

    public void StopAll()
    {
        lock (_gate)
        {
            _mixer.RemoveAllMixerInputs();
            _inputs.Clear();
        }
    }

    public void SetGain(long playbackId, float gain)
    {
        lock (_gate)
        {
            if (_inputs.TryGetValue(playbackId, out var input))
            {
                input.Volume.Volume = gain;
            }
        }
    }

    private void OnMixerInputEnded(object? sender, SampleProviderEventArgs e)
    {
        long? ended = null;

        lock (_gate)
        {
            if (e.SampleProvider is TrackedSampleProvider tracked && _inputs.Remove(tracked.Id))
            {
                ended = tracked.Id;
            }
        }

        if (ended is { } id)
        {
            Finished?.Invoke(id);
        }
    }

    private MMDevice? Resolve(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDevice(deviceId);
        }
        catch (Exception ex)
        {
            // A device that has been unplugged since it was chosen. Falling back to the
            // default is right: the alternative is no audio at all, which as a symptom is
            // indistinguishable from the app being broken.
            _logger.LogWarning(ex, "Output device {DeviceId} is unavailable; using the default", deviceId);
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _mixer.MixerInputEnded -= OnMixerInputEnded;
            _output?.Dispose();
            _output = null;
            _inputs.Clear();
        }
    }
}
