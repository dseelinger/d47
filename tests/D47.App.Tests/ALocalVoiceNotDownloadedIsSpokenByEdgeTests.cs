using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Speech;
using Xunit;

namespace D47.App.Tests;

/// <summary>The client for a local voice speaks through Edge until the install check passes, then through the local voice.</summary>
public class ALocalVoiceNotDownloadedIsSpokenByEdgeTests
{
    private static async Task<(string Provider, string? Voice, double Rate)> Heard(
        bool installed, VoiceSelection chosen, Recorder local, Recorder edge)
    {
        var client = new LocalVoiceWithEdgeStandIn(local, edge, () => installed, () => 1.25);

        await client.SynthesizeAsync("Docking request granted.", chosen, TestContext.Current.CancellationToken);

        var (provider, asked) = local.Asked.Count > 0 ? (local.Id, local.Asked[^1]) : (edge.Id, edge.Asked[^1]);
        return (provider, asked.VoiceId, asked.Rate);
    }

    [Fact]
    public async Task EdgeSpeaksInSoniaAtItsOwnRateWhileTheModelIsMissing()
    {
        var (provider, voice, rate) = await Heard(
            installed: false, new VoiceSelection("af_heart", 0.8), new Recorder("kokoro"), new Recorder("edge"));

        Assert.Equal("edge", provider);
        Assert.Equal("en-GB-SoniaNeural", voice);
        Assert.Equal(1.25, rate);
    }

    [Fact]
    public async Task TheChosenVoiceSpeaksOnceTheModelIsInstalled()
    {
        var (provider, voice, rate) = await Heard(
            installed: true, new VoiceSelection("af_heart", 0.8), new Recorder("kokoro"), new Recorder("edge"));

        Assert.Equal("kokoro", provider);
        Assert.Equal("af_heart", voice);
        Assert.Equal(0.8, rate);
    }

    [Fact]
    public async Task TheNextLineFollowsTheInstallWithoutARebuild()
    {
        var installed = false;
        var local = new Recorder("kokoro");
        var edge = new Recorder("edge");
        var client = new LocalVoiceWithEdgeStandIn(local, edge, () => installed, () => 1.0);

        await client.SynthesizeAsync("First line.", new VoiceSelection("af_heart"), TestContext.Current.CancellationToken);
        installed = true;
        await client.SynthesizeAsync("Second line.", new VoiceSelection("af_heart"), TestContext.Current.CancellationToken);

        Assert.Equal(["First line."], edge.Asked.Select(asked => asked.Text));
        Assert.Equal(["Second line."], local.Asked.Select(asked => asked.Text));
    }

    [Fact]
    public async Task AFailedLocalLineIsNotRetriedThroughEdge()
    {
        var local = new Recorder("kokoro") { Fail = true };
        var edge = new Recorder("edge");
        var client = new LocalVoiceWithEdgeStandIn(local, edge, () => true, () => 1.0);

        await Assert.ThrowsAsync<TtsException>(() => client.SynthesizeAsync("Line.", new VoiceSelection("af_heart"), TestContext.Current.CancellationToken));

        Assert.Empty(edge.Asked);
    }

    private sealed class Recorder(string id) : ITtsProvider
    {
        public List<(string Text, string? VoiceId, double Rate)> Asked { get; } = [];

        public bool Fail { get; init; }

        public string Id => id;

        public string Name => id;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            Asked.Add((text, voice.VoiceId, voice.Rate));

            return Fail
                ? throw new TtsException("The model failed.")
                : Task.FromResult(new AudioClip(text, new byte[960], AudioFormat.Standard));
        }
    }
}
