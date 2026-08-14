using System.Net;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// D47 never asks ElevenLabs for more at once than the account allows.
/// <para>
/// Reported from a running build: "Too many concurrent requests. Your current subscription is
/// associated with a maximum of 5 concurrent requests." A reply's sentences are synthesised
/// concurrently on purpose — that is what lets speech start at the first sentence boundary —
/// so a long reply asked for all of them at once and the account refused the tail. Callouts,
/// crew lines and re-voiced comms share the same account, so the gate belongs to the provider
/// rather than to any one pipeline.
/// </para>
/// </summary>
public class ConcurrencyIsBoundedTests
{
    [Fact]
    public async Task NoMoreThanThreeSynthesesAreInFlightAtOnce()
    {
        var concurrent = 0;
        var peak = 0;
        var gate = new SemaphoreSlim(0);

        var handler = new BlockingHandler(async () =>
        {
            var now = Interlocked.Increment(ref concurrent);

            // Watched rather than sampled: the peak is the whole assertion.
            InterlockedMax(ref peak, now);

            await gate.WaitAsync();
            Interlocked.Decrement(ref concurrent);
        });

        using var provider = new ElevenLabsTtsProvider(
            () => "a-key",
            NullLogger<ElevenLabsTtsProvider>.Instance,
            new HttpClient(handler));

        var speaking = Enumerable
            .Range(0, 8)
            .Select(i => provider.SynthesizeAsync($"Sentence {i}.", new VoiceSelection("voice-1")))
            .ToArray();

        // Long enough for every request that is going to start to have started.
        await Task.Delay(250, TestContext.Current.CancellationToken);

        var seen = Volatile.Read(ref peak);

        gate.Release(64);

        foreach (var task in speaking)
        {
            try
            {
                await task;
            }
            catch (TtsException)
            {
                // The fake returns nothing playable; what is under test is how many got out.
            }
        }

        Assert.True(seen > 0, "No request reached the transport at all.");
        Assert.True(seen <= 3, $"{seen} syntheses were in flight at once; the account allows few.");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;

        while (value > (seen = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
            {
                return;
            }
        }
    }

    private sealed class BlockingHandler(Func<Task> onRequest) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await onRequest();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([]),
            };
        }
    }
}
