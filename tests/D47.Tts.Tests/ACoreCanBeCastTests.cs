using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Persona;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public class ACoreCanBeCastTests
{
    private sealed class Capturing : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // 24 kHz mono 16-bit silence: enough bytes to be a clip, no bearing on the assertion.
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[4_800]),
            };
        }
    }

    private static async Task<JsonElement> SentAsync(Func<string?>? direction)
    {
        var handler = new Capturing();

        using var provider = new OpenAiTtsProvider(
            () => "sk-not-a-real-key",
            NullLogger<OpenAiTtsProvider>.Instance,
            handler,
            direction);

        await provider.SynthesizeAsync("Course laid in.", VoiceSelection.Default, TestContext.Current.CancellationToken);

        Assert.NotNull(handler.Body);

        return JsonDocument.Parse(handler.Body!).RootElement;
    }

    [Fact]
    public async Task TheDirectionReachesTheWire()
    {
        var sent = await SentAsync(() => "Speak as Warden. A steady, unhurried older man.");

        Assert.Equal(
            "Speak as Warden. A steady, unhurried older man.",
            sent.GetProperty("instructions").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NothingToPerformSendsNoFieldAtAll(string? nothing)
    {
        var sent = await SentAsync(nothing is null ? null : () => nothing);

        Assert.False(
            sent.TryGetProperty("instructions", out _),
            "An empty instruction must be left out of the request rather than sent as a blank one.");
    }

    [Fact]
    public async Task AProviderBuiltWithoutOneIsUnchanged()
    {
        var sent = await SentAsync(direction: null);

        Assert.False(sent.TryGetProperty("instructions", out _));
        Assert.Equal("pcm", sent.GetProperty("response_format").GetString());
    }

    [Fact]
    public async Task TheDirectionIsAskedEveryTimeRatherThanCapturedOnce()
    {
        var handler = new Capturing();
        var core = "Speak as Warden.";

        using var provider = new OpenAiTtsProvider(
            () => "sk-not-a-real-key",
            NullLogger<OpenAiTtsProvider>.Instance,
            handler,
            () => core);

        await provider.SynthesizeAsync("One.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        Assert.Contains("Warden", handler.Body!, StringComparison.Ordinal);

        core = "Speak as Archivist.";

        await provider.SynthesizeAsync("Two.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        Assert.Contains("Archivist", handler.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDirectionIsTheCataloguesOwnVoiceHint()
    {
        foreach (var persona in PersonaCatalog.All)
        {
            var direction = VoiceDirection.For(persona);

            Assert.NotNull(direction);
            Assert.Contains(persona.Name, direction!, StringComparison.Ordinal);
            Assert.Contains(persona.VoiceHint.Description.Trim(), direction, StringComparison.Ordinal);
            Assert.True(direction.Length <= VoiceDirection.MaximumCharacters);
        }
    }

    [Fact]
    public void WithNoCoreThereIsNothingToPerform()
    {
        Assert.Null(VoiceDirection.For(null));
    }
}
