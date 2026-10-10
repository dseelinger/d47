using System.Net;
using D47.Core.Audio;
using D47.Core.Storage;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>A provider with no voice chosen and none listed refuses, rather than guessing a voice.</summary>
public class ANoChosenVoiceIsRefusedTests
{
    [Fact]
    public async Task EdgeNeuralRefusesWhenItListsNoVoices()
    {
        using var http = new HttpClient(new ListsNothing());
        using var provider = new EdgeNeuralTtsProvider(NullLogger<EdgeNeuralTtsProvider>.Instance, http);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Equal("No Edge Neural voice has been chosen. Pick one in Settings.", failure.Message);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task KokoroRefusesWithNoVoiceWhileItIsNotDownloaded()
    {
        using var provider = new KokoroTtsProvider(new MemoryFileSystem(), Path.GetTempPath(), NullLogger<KokoroTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Equal("No Kokoro voice has been chosen. Pick one in Settings.", failure.Message);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task KokoroRefusesAVoiceItDoesNotHave()
    {
        using var provider = new KokoroTtsProvider(new MemoryFileSystem(), Path.GetTempPath(), NullLogger<KokoroTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "test", new VoiceSelection("xx_nobody"), TestContext.Current.CancellationToken));

        Assert.Contains("xx_nobody", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A voice list that answers with no voices.</summary>
    internal sealed class ListsNothing(string body = "[]") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
