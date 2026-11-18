using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// HttpClient reports its own timeout as a cancellation. A list that timed out must read as unreachable, not as
/// an account with no voices.
/// </summary>
public class AVoiceListThatTimesOutSaysItCouldNotBeReachedTests
{
    [Fact]
    public async Task ElevenLabs()
    {
        using var http = new HttpClient(new TimesOut());
        using var provider = new ElevenLabsTtsProvider(() => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        AssertUnreachable(await provider.ListVoicesAsync(TestContext.Current.CancellationToken), "ElevenLabs");
    }

    [Fact]
    public async Task Cartesia()
    {
        using var provider = new CartesiaTtsProvider(() => "sk_test", NullLogger<CartesiaTtsProvider>.Instance, new TimesOut());

        AssertUnreachable(await provider.ListVoicesAsync(TestContext.Current.CancellationToken), "Cartesia");
    }

    [Fact]
    public async Task EdgeNeural()
    {
        using var http = new HttpClient(new TimesOut());
        using var provider = new EdgeNeuralTtsProvider(NullLogger<EdgeNeuralTtsProvider>.Instance, http);

        AssertUnreachable(await provider.ListVoicesAsync(TestContext.Current.CancellationToken), "Edge Neural");
    }

    [Fact]
    public async Task ACancellationTheCallerAskedForIsStillACancellation()
    {
        using var http = new HttpClient(new WaitsForCancellation());
        using var provider = new ElevenLabsTtsProvider(() => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ListVoicesAsync(cancelled.Token));
    }

    private static void AssertUnreachable(VoiceCatalogue listed, string provider)
    {
        Assert.Equal(VoiceListing.Unreachable, listed.Listing);

        var said = listed.WhyEmpty(provider)!;
        Assert.Contains("could not be reached", said, StringComparison.Ordinal);
        Assert.Contains("did not answer in time", said, StringComparison.Ordinal);
        Assert.DoesNotContain("no voices", said, StringComparison.Ordinal);
    }

    /// <summary>Throws what HttpClient throws when its Timeout elapses.</summary>
    private sealed class TimesOut : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
                new TimeoutException());
    }

    private sealed class WaitsForCancellation : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable");
        }
    }
}
