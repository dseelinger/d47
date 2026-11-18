using D47.App.Voice;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class APageWaitPastASecondPlaysTheWaitingBedTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly LoopSink _sink = new();
    private readonly TaskCompletionSource _second = new();
    private TimeSpan _asked;

    private WaitSound Build(bool enabled = true)
    {
        var arbiter = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
        var cues = CueLibrary.Load();

        return new WaitSound(arbiter, () => cues, () => enabled, (length, token) =>
        {
            _asked = length;
            token.Register(() => _second.TrySetCanceled(token));
            return _second.Task;
        });
    }

    [Fact]
    public async Task ATaskStillRunningAfterTheDelayPlaysUntilItEnds()
    {
        var running = new TaskCompletionSource();
        var playing = Build().PlayWhile(running.Task);

        Assert.Empty(_sink.Live);

        _second.SetResult();

        Assert.True(SpinWait.SpinUntil(() => _sink.Live.Contains(CueLibrary.DefaultWait), Patience));
        Assert.Equal(WaitSound.Delay, _asked);

        running.SetResult();
        await playing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Empty(_sink.Live);
    }

    [Fact]
    public async Task ATaskThatEndsBeforeTheDelayPlaysNothing()
    {
        var running = new TaskCompletionSource();
        var playing = Build().PlayWhile(running.Task);

        running.SetResult();
        await playing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.True(_second.Task.IsCanceled);
        Assert.Empty(_sink.Started);
    }

    [Fact]
    public async Task WithTheSettingOffTheWaitIsSilent()
    {
        var running = new TaskCompletionSource();
        var playing = Build(enabled: false).PlayWhile(running.Task);

        _second.SetResult();
        running.SetResult();
        await playing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Empty(_sink.Started);
    }

    [Fact]
    public async Task AFailedOrCancelledTaskStopsTheLoopWithoutThrowing()
    {
        var running = new TaskCompletionSource();
        var playing = Build().PlayWhile(running.Task);

        _second.SetResult();
        Assert.True(SpinWait.SpinUntil(() => _sink.Live.Count == 1, Patience));

        running.SetException(new InvalidOperationException("the search failed"));
        await playing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Empty(_sink.Live);
    }

    /// <summary>Records which named clips are playing.</summary>
    private sealed class LoopSink : IAudioSink
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<long, string> _live = [];

        public List<string> Started { get; } = [];

        public IReadOnlyCollection<string> Live
        {
            get
            {
                lock (_gate)
                {
                    return [.. _live.Values];
                }
            }
        }

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new SilentTap();

        public void Play(PlaybackRequest request)
        {
            lock (_gate)
            {
                _live[request.Id] = request.Name;
                Started.Add(request.Name);
            }
        }

        public void Stop(long playbackId)
        {
            lock (_gate)
            {
                _live.Remove(playbackId);
            }
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

        public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false)
        {
        }

        public TrackProgress? Progress(long playbackId) => null;

        public bool Seek(long playbackId, TimeSpan to) => false;

        public void Ignore() => Finished?.Invoke(0);
    }
}
