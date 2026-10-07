using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// <see cref="WasapiAudioSink.Reopen"/> and <see cref="WasapiAudioSink.Dispose"/> close the old output after releasing
/// the sink's lock: closing waits for the render thread, which takes that lock to settle an ended input (#532).
/// </summary>
public class ClosingTheOutputWhileASoundEndsDoesNotHangTests
{
    private static readonly AudioClip Short = new("short", new byte[8], new AudioFormat(48_000, 2));

    [Theory]
    [InlineData("reopen")]
    [InlineData("dispose")]
    public async Task TheRenderThreadFinishesWhileTheOldOutputCloses(string close)
    {
        var outputs = new List<RenderedByHand>();
        using var sink = new WasapiAudioSink(
            NullLogger<WasapiAudioSink>.Instance,
            new FakeEndpointEnumerator(),
            _ =>
            {
                var opened = new RenderedByHand();
                outputs.Add(opened);
                return opened;
            });

        sink.Open();
        var output = outputs[0];

        // Two clips ending in one read, so the render thread settles the second after Finished returns for the first.
        sink.Play(new PlaybackRequest(1, Short, Loop: false, Gain: 1f));
        sink.Play(new PlaybackRequest(2, Short, Loop: false, Gain: 1f));

        Task? closing = null;
        var closingStarted = false;

        sink.Finished += _ =>
        {
            if (closing is null)
            {
                closing = Task.Run(() =>
                {
                    if (close == "reopen")
                    {
                        sink.Reopen(null);
                    }
                    else
                    {
                        sink.Dispose();
                    }
                });

                closingStarted = output.Disposing.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            }
        };

        output.Render(4_800);

        Assert.True(closingStarted, "The old output was not closed");
        await closing!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(output.RenderReturned, "Closing the output waited on a render that was waiting on the sink's lock");
    }
}
