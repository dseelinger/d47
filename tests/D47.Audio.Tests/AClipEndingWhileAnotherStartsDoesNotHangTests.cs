using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// <see cref="WasapiAudioSink.Finished"/> is raised after the mixer's read has returned, not inside the mixer's lock:
/// a handler that waits on another thread starting a clip, as the arbiter's lock makes one do, must not hang the
/// render thread (#532).
/// </summary>
public class AClipEndingWhileAnotherStartsDoesNotHangTests
{
    private static readonly AudioClip Short = new("short", new byte[8], new AudioFormat(48_000, 2));

    [Fact]
    public void AnotherThreadCanStartAClipWhileFinishedIsRunning()
    {
        var output = new RenderedByHand();
        using var sink = new WasapiAudioSink(
            NullLogger<WasapiAudioSink>.Instance, new FakeEndpointEnumerator(), _ => output);

        sink.Open();
        sink.Play(new PlaybackRequest(1, Short, Loop: false, Gain: 1f));

        bool? started = null;

        sink.Finished += id =>
        {
            if (id == 1)
            {
                // A dedicated thread, so the wait below measures the sink rather than how soon the pool has a thread free.
                var next = Task.Factory.StartNew(
                    () => sink.Play(new PlaybackRequest(2, Short, Loop: false, Gain: 1f)),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                started = next.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            }
        };

        output.Render(4_800);

        Assert.NotNull(started);
        Assert.True(started.Value, "Starting a clip did not finish while Finished was running");
    }

    [Fact]
    public void FinishedIsRaisedOnceForAClipThatEnded()
    {
        var output = new RenderedByHand();
        using var sink = new WasapiAudioSink(
            NullLogger<WasapiAudioSink>.Instance, new FakeEndpointEnumerator(), _ => output);

        var finished = new List<long>();
        sink.Finished += finished.Add;

        sink.Open();
        sink.Play(new PlaybackRequest(7, Short, Loop: false, Gain: 1f));

        output.Render(4_800);
        output.Render(4_800);

        Assert.Equal([7L], finished);
    }
}
