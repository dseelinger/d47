using D47.Core.Audio;
using D47.Core.Listening;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace D47.Audio.Tests;

/// <summary>A stub enumerator whose default-device notification can be raised by hand.</summary>
internal sealed class FakeEndpointEnumerator : IAudioEndpointEnumerator
{
    private readonly Dictionary<DataFlow, List<AudioEndpoint>> _active = [];
    private readonly Dictionary<(DataFlow Flow, Role Role), AudioEndpoint> _defaults = [];

    public event Action<DataFlow, Role>? DefaultDeviceChanged;

    public IReadOnlyList<AudioEndpoint> Active(DataFlow flow) =>
        _active.TryGetValue(flow, out var endpoints) ? endpoints : [];

    public AudioEndpoint? Default(DataFlow flow, Role role) =>
        _defaults.TryGetValue((flow, role), out var endpoint) ? endpoint : null;

    public void SetActive(DataFlow flow, params AudioEndpoint[] endpoints) => _active[flow] = [.. endpoints];

    public void SetDefault(DataFlow flow, Role role, AudioEndpoint endpoint) => _defaults[(flow, role)] = endpoint;

    public void Raise(DataFlow flow, Role role) => DefaultDeviceChanged?.Invoke(flow, role);
}

/// <summary>A minimal <see cref="IAudioSink"/>, for driving a real <see cref="AudioArbiter"/> in tests.</summary>
internal sealed class FakeAudioSink : IAudioSink
{
    public List<PlaybackRequest> Played { get; } = [];

    public List<long> Stopped { get; } = [];

    public bool StoppedAll { get; private set; }

    public event Action<long>? Finished;

    public IRenderReferenceTap ReferenceTap { get; } = new FakeRenderTap();

    public void Play(PlaybackRequest request) => Played.Add(request);

    public void Stop(long playbackId) => Stopped.Add(playbackId);

    public void StopAll() => StoppedAll = true;

    public void SetGain(long playbackId, float gain)
    {
    }

    public void Complete(long playbackId) => Finished?.Invoke(playbackId);
}

/// <summary>A device that follows the Default Device on command, for exercising <see cref="DefaultDeviceFollowPolicy"/>.</summary>
internal sealed class FakeDeviceReopener : IDefaultDeviceReopener
{
    private bool _due;
    private bool _gone;

    public string? OpenDeviceName { get; private set; } = "Old Device";

    public int ReopenCount { get; private set; }

    public string? LastReopenedDeviceId { get; private set; }

    public void MakeDue() => _due = true;

    public void MakeGone() => _gone = true;

    public bool DefaultDeviceMoved(DateTimeOffset now) => _due;

    public void AcknowledgeDefaultDeviceMove() => _due = false;

    public bool OpenDeviceIsGone() => _gone;

    public void Reopen(string? deviceId)
    {
        ReopenCount++;
        LastReopenedDeviceId = deviceId;
        OpenDeviceName = "New Device";
    }
}

internal sealed class RecordingCaptureSink : ICaptureSink
{
    private readonly List<float[]> _writes = [];

    public IReadOnlyList<float[]> Writes => _writes;

    public int Resets { get; private set; }

    /// <summary>Every sample handed downstream, in order, with the framing flattened out.</summary>
    public float[] All => [.. _writes.SelectMany(write => write)];

    public void Write(ReadOnlySpan<float> samples) => _writes.Add(samples.ToArray());

    public void Reset() => Resets++;
}

internal sealed class FakeRenderTap : IRenderReferenceTap
{
    public event Action<RenderReferenceFrame>? Rendered;

    public bool Subscribed => Rendered is not null;

    public void Raise(RenderReferenceFrame frame) => Rendered?.Invoke(frame);
}

internal sealed class ArraySampleProvider(WaveFormat format, params float[] samples) : ISampleProvider
{
    private int _position;

    public WaveFormat WaveFormat { get; } = format;

    public int Read(float[] buffer, int offset, int count)
    {
        var take = Math.Min(count, samples.Length - _position);

        for (var i = 0; i < take; i++)
        {
            buffer[offset + i] = samples[_position + i];
        }

        _position += take;
        return take;
    }
}
