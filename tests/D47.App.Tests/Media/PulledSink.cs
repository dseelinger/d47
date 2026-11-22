using System.Collections.Concurrent;
using D47.Audio;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests.Media;

/// <summary>
/// A sink with no device: a streamed track is read through the app's <see cref="StreamSampleProvider"/> by a thread that
/// takes <c>speed</c> times 10 ms of sound every 10 ms, unless the track is paused. Clips finish only when told.
/// </summary>
internal sealed class PulledSink(int speed = 1) : IAudioSink, IDisposable
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(10);

    private readonly ConcurrentDictionary<long, Pulled> _tracks = new();

    public ConcurrentQueue<PlaybackRequest> Started { get; } = new();

    public ConcurrentDictionary<long, float> Gains { get; } = new();

    public event Action<long>? Finished;

    public void Play(PlaybackRequest request)
    {
        Started.Enqueue(request);
        Gains[request.Id] = request.Gain;

        if (request.Track is { } track)
        {
            var pulled = new Pulled(request.Id, new StreamSampleProvider(track, NullLogger.Instance));
            _tracks[request.Id] = pulled;
            pulled.Thread = new Thread(() => Pump(pulled)) { IsBackground = true, Name = "pulled sink" };
            pulled.Thread.Start();
        }
    }

    /// <summary>Lets a clip run to its end.</summary>
    public void Finish(long playbackId) => Finished?.Invoke(playbackId);

    public void Stop(long playbackId)
    {
        if (_tracks.TryRemove(playbackId, out var pulled))
        {
            pulled.Stopped = true;
            pulled.Source.Dispose();
        }
    }

    public void Pause(long playbackId)
    {
        if (_tracks.TryGetValue(playbackId, out var pulled))
        {
            pulled.Paused = true;
        }
    }

    public void Resume(long playbackId)
    {
        if (_tracks.TryGetValue(playbackId, out var pulled))
        {
            pulled.Paused = false;
        }
    }

    public bool IsPaused(long playbackId) => _tracks.TryGetValue(playbackId, out var pulled) && pulled.Paused;

    public void SetGain(long playbackId, float gain) => Gains[playbackId] = gain;

    public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false)
    {
    }

    public TrackProgress? Progress(long playbackId) => _tracks.TryGetValue(playbackId, out var pulled) ? pulled.Source.Progress : null;

    public bool Seek(long playbackId, TimeSpan to) => _tracks.TryGetValue(playbackId, out var pulled) && pulled.Source.Seek(to);

    public void Dispose()
    {
        foreach (var id in _tracks.Keys)
        {
            Stop(id);
        }
    }

    private void Pump(Pulled pulled)
    {
        var samples = new float[AudioFormat.Music.SampleRate / 100 * AudioFormat.Music.Channels];

        while (!pulled.Stopped)
        {
            Thread.Sleep(Step);

            for (var chunk = 0; chunk < speed && !pulled.Paused; chunk++)
            {
                if (pulled.Source.Read(samples, 0, samples.Length) < samples.Length)
                {
                    if (_tracks.TryRemove(pulled.Id, out _))
                    {
                        pulled.Source.Dispose();
                        Finished?.Invoke(pulled.Id);
                    }

                    return;
                }
            }
        }
    }

    private sealed class Pulled(long id, StreamSampleProvider source)
    {
        public long Id { get; } = id;

        public StreamSampleProvider Source { get; } = source;

        public Thread? Thread { get; set; }

        public volatile bool Paused;

        public volatile bool Stopped;
    }
}
