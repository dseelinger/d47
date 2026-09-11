using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Ready seam: a load runs off the caller's thread and a capture waits for it (#147).</summary>
public class APressMadeWhileTheModelLoadsIsNotThrownAwayTests
{
    [Fact]
    public async Task AnUtteranceCapturedWhileTheModelLoadsWaitsForIt()
    {
        using var transcriber = New();
        using var held = new ManualResetEventSlim();
        var entered = new TaskCompletionSource();

        transcriber.LoadsWith = _ =>
        {
            entered.TrySetResult();
            held.Wait();
            return false;
        };

        var loading = transcriber.LoadAsync("ggml-medium.en.bin", "medium.en", useGpu: false);
        await entered.Task;

        Assert.True(transcriber.IsLoading);
        Assert.False(transcriber.Ready.IsCompleted);

        var transcribing = transcriber.TranscribeAsync(
            Silence(), properNouns: [], TestContext.Current.CancellationToken);

        // Still waiting, rather than answered with an empty transcription because the load had not finished.
        Assert.NotSame(transcribing, await Task.WhenAny(
            transcribing, Task.Delay(250, TestContext.Current.CancellationToken)));

        held.Set();

        await loading;
        await transcribing;

        Assert.False(transcriber.IsLoading);
        Assert.True(transcriber.Ready.IsCompleted);
    }

    [Fact]
    public async Task ASecondRequestSupersedesTheFirstAndLoadsAfterIt()
    {
        using var transcriber = New();
        using var held = new ManualResetEventSlim();
        var entered = new TaskCompletionSource();
        var asked = new List<string>();

        transcriber.LoadsWith = path =>
        {
            lock (asked)
            {
                asked.Add(path);
            }

            if (path == "first")
            {
                entered.TrySetResult();
                held.Wait();
            }

            return true;
        };

        var first = transcriber.LoadAsync("first", "first", useGpu: false);
        await entered.Task;

        var second = transcriber.LoadAsync("second", "second", useGpu: false);
        held.Set();

        // The first load ran to the end and its result was discarded; the second's is the one that counts.
        Assert.False(await first);
        Assert.True(await second);

        string[] order;

        lock (asked)
        {
            order = [.. asked];
        }

        Assert.Equal(["first", "second"], order);

        // Ready follows the second request, not the first.
        Assert.False(transcriber.IsLoading);
        Assert.True(transcriber.Ready.IsCompleted);
    }

    [Fact]
    public async Task AFailedLoadLetsTheUtteranceThroughAsEmpty()
    {
        using var transcriber = New();

        Assert.False(await transcriber.LoadAsync(@"C:\nowhere\ggml-none.bin", "none", useGpu: false));

        Assert.False(transcriber.IsLoading);
        Assert.True(transcriber.Ready.IsCompleted);
        Assert.NotNull(transcriber.Unavailable);

        Assert.Equal(string.Empty, (await transcriber.TranscribeAsync(
                Silence(), properNouns: [], TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task UnloadingReleasesWhateverIsWaitingOnAPendingLoad()
    {
        using var transcriber = New();
        using var held = new ManualResetEventSlim();
        var entered = new TaskCompletionSource();

        transcriber.LoadsWith = _ =>
        {
            entered.TrySetResult();
            held.Wait();
            return true;
        };

        var loading = transcriber.LoadAsync("ggml-medium.en.bin", "medium.en", useGpu: false);
        await entered.Task;

        // Off the calling thread: Unload waits for the load to let go of the semaphore, but releases Ready
        // before it does.
        var unloading = Task.Run(transcriber.Unload, TestContext.Current.CancellationToken);

        var ready = transcriber.Ready;
        Assert.Same(ready, await Task.WhenAny(ready, Task.Delay(2000, TestContext.Current.CancellationToken)));

        held.Set();

        await loading;
        await unloading;

        Assert.False(transcriber.IsReady);
    }

    private static WhisperTranscriber New() =>
        new(NullLogger<WhisperTranscriber>.Instance);

    /// <summary>One second of it, which is what the selftest presses with.</summary>
    private static Utterance Silence() => new(new float[16000], 16000);
}
