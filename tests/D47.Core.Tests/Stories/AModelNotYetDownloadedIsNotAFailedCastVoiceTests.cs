using D47.Core.Audio;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AModelNotYetDownloadedIsNotAFailedCastVoiceTests
{
    private sealed class Throws(TtsException thrown) : ITtsProvider
    {
        public string Id => "chosen";

        public string Name => "Chosen";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default) =>
            throw thrown;
    }

    private sealed class Speaks : ITtsProvider
    {
        public List<string?> Voices { get; } = [];

        public string Id => "pinned";

        public string Name => "Pinned";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            Voices.Add(voice.VoiceId);
            return Task.FromResult(new AudioClip(text, ReadOnlyMemory<byte>.Empty, AudioFormat.Standard));
        }
    }

    [Fact]
    public async Task ItIsSpokenInThePinnedVoiceAndNotReportedAsFailed()
    {
        var pinned = new Speaks();
        var failures = new List<string>();
        var cast = new FallingBackTtsProvider(
            new Throws(new TtsException("Download it first.", fault: TtsFault.NotDownloaded)), pinned, "pinned-voice", failures.Add);

        await cast.SynthesizeAsync("こんにちは", new VoiceSelection("marlow") { Language = "ja" }, TestContext.Current.CancellationToken);

        Assert.Equal(["pinned-voice"], pinned.Voices);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task AnyOtherRefusalIsStillReported()
    {
        var pinned = new Speaks();
        var failures = new List<string>();
        var cast = new FallingBackTtsProvider(new Throws(new TtsException("It broke.")), pinned, "pinned-voice", failures.Add);

        await cast.SynthesizeAsync("Hold position.", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);

        Assert.Equal(["It broke."], failures);
    }
}
