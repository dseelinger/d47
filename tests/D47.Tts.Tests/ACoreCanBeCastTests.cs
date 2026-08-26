using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Persona;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// A Guardian core is cast rather than merely given a larynx
/// (<a href="https://github.com/dseelinger/d47/issues/49">#49</a>).
/// <para>
/// <c>gpt-4o-mini-tts</c> takes an <c>instructions</c> field steering accent, tone, intonation and
/// delivery. It is the one thing no other provider offers, and it is the reason to want OpenAI at
/// all — <c>guardian-personas.md</c> already describes each core's manner in prose, and that prose
/// is the input this field wants.
/// </para>
/// </summary>
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

    /// <summary>
    /// The whole point: what the core is like reaches the synthesiser rather than only the model.
    /// </summary>
    [Fact]
    public async Task TheDirectionReachesTheWire()
    {
        var sent = await SentAsync(() => "Speak as Warden. A steady, unhurried older man.");

        Assert.Equal(
            "Speak as Warden. A steady, unhurried older man.",
            sent.GetProperty("instructions").GetString());
    }

    /// <summary>
    /// <b>Absent, not empty.</b> A slot with nobody to perform sends exactly the request it sent
    /// before this existed — which is what keeps every other slot, and personality-off, unchanged.
    /// </summary>
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

    /// <summary>A provider built the way every existing caller builds it is untouched.</summary>
    [Fact]
    public async Task AProviderBuiltWithoutOneIsUnchanged()
    {
        var sent = await SentAsync(direction: null);

        Assert.False(sent.TryGetProperty("instructions", out _));
        Assert.Equal("pcm", sent.GetProperty("response_format").GetString());
    }

    /// <summary>
    /// The direction is asked at synthesis rather than captured once, because a Commander switches
    /// core while d47 is running and the shared client is not rebuilt for it.
    /// </summary>
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

    /// <summary>
    /// <b>Derived from the catalogue, not from the pack</b> — the choice the issue asked to be
    /// stated rather than discovered. The personas are written twice and keeping them in step is a
    /// cost this repository already carries; an instruction built from <c>guardian-personas.md</c>
    /// would be a third copy. Built from <see cref="PersonaCatalog"/>, it comes from the same text
    /// the prompt is built from, so a core cannot sound like one thing and speak like another.
    /// </summary>
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

    /// <summary>
    /// Personality off means nobody is being performed, so there is no direction — the same
    /// reasoning that makes prompt position 3 absent rather than neutral.
    /// </summary>
    [Fact]
    public void WithNoCoreThereIsNothingToPerform()
    {
        Assert.Null(VoiceDirection.For(null));
    }
}
