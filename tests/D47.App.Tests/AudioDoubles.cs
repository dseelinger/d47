using D47.Core.Audio;

namespace D47.App.Tests;

/// <summary>Plays nothing and finishes nothing until <see cref="FinishEverything"/>.</summary>
internal sealed class SilentSink : IAudioSink
{
    private readonly List<long> _playing = [];

    public event Action<long>? Finished;

    public IRenderReferenceTap ReferenceTap { get; } = new SilentTap();

    public void Play(PlaybackRequest request) => _playing.Add(request.Id);

    /// <summary>Completes everything in flight, and everything that starts because of it.</summary>
    public void FinishEverything()
    {
        while (_playing.Count > 0)
        {
            var playing = _playing[0];
            _playing.RemoveAt(0);
            Finished?.Invoke(playing);
        }
    }

    public void Stop(long playbackId)
    {
    }

    public void Pause(long playbackId)
    {
    }

    public void Resume(long playbackId)
    {
    }

    public void SetGain(long playbackId, float gain)
    {
    }

    public List<(long Id, float To, TimeSpan Over, bool HoldAtEnd)> Fades { get; } = [];

    public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false) =>
        Fades.Add((playbackId, to, over, holdAtEnd));

    public TrackProgress? Progress(long playbackId) => null;

    public bool Seek(long playbackId, TimeSpan to) => false;
}

/// <summary>Keeps each clip it is asked to play and never finishes one.</summary>
internal sealed class CollectingSink : IAudioSink
{
    public List<AudioClip> Played { get; } = [];

    public event Action<long>? Finished;

    public IRenderReferenceTap ReferenceTap { get; } = new SilentTap();

    public void Play(PlaybackRequest request)
    {
        Played.Add(request.Clip!);
        _ = Finished;
    }

    public void Stop(long playbackId)
    {
    }

    public void Pause(long playbackId)
    {
    }

    public void Resume(long playbackId)
    {
    }

    public void SetGain(long playbackId, float gain)
    {
    }

    public List<(long Id, float To, TimeSpan Over, bool HoldAtEnd)> Fades { get; } = [];

    public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false) =>
        Fades.Add((playbackId, to, over, holdAtEnd));

    public TrackProgress? Progress(long playbackId) => null;

    public bool Seek(long playbackId, TimeSpan to) => false;
}

/// <summary>A reference tap that never renders.</summary>
internal sealed class SilentTap : IRenderReferenceTap
{
    public event Action<RenderReferenceFrame>? Rendered;

    public void Dispose() => _ = Rendered;
}

/// <summary>The arbiter's tap, driven by hand.</summary>
internal sealed class HandDrivenTap : IRenderReferenceTap
{
    public event Action<RenderReferenceFrame>? Rendered;

    public void Render(int bytes, AudioFormat? format = null) =>
        Rendered?.Invoke(new RenderReferenceFrame(0, new byte[bytes], format ?? AudioFormat.Standard));
}

/// <summary>Starts and finishes clips on demand and renders nothing.</summary>
internal sealed class HandDrivenSink : IAudioSink
{
    public List<long> Started { get; } = [];

    public event Action<long>? Finished;

    public IRenderReferenceTap ReferenceTap { get; } = new HandDrivenTap();

    public void Play(PlaybackRequest request) => Started.Add(request.Id);

    public void Stop(long playbackId)
    {
    }

    public void Pause(long playbackId)
    {
    }

    public void Resume(long playbackId)
    {
    }

    public void SetGain(long playbackId, float gain)
    {
    }

    public List<(long Id, float To, TimeSpan Over, bool HoldAtEnd)> Fades { get; } = [];

    public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false) =>
        Fades.Add((playbackId, to, over, holdAtEnd));

    public TrackProgress? Progress(long playbackId) => null;

    public bool Seek(long playbackId, TimeSpan to) => false;

    public void Finish(long playbackId) => Finished?.Invoke(playbackId);
}

/// <summary>Synthesis with the network taken out: a 900 Hz tone for any line.</summary>
internal sealed class OneToneProvider : ITtsProvider
{
    public const int Samples = 4_800;

    public string Id => "one-tone";

    public string Name => "One tone";

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("one-tone", "One Tone", "en-GB")]));

    public Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AudioClip(text, TestTone.Pcm(Samples), AudioFormat.Standard));
}

/// <summary>A 900 Hz sine at 48 kHz as 16-bit mono PCM.</summary>
internal static class TestTone
{
    public static byte[] Pcm(int samples)
    {
        var pcm = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 900 * index / 48_000.0) * 0.4 * short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return pcm;
    }
}
