using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

[Trait("Category", "Integration")]
public class ChatterboxLiveTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("D47_TTS_LIVE") == "1";

    private static string Folder =>
        Environment.GetEnvironmentVariable("D47_CHATTERBOX_FOLDER")
        ?? Path.Combine(Path.GetTempPath(), "d47-chatterbox");

    private static string Voices => Path.Combine(AppContext.BaseDirectory, "voices", "chatterbox");

    [Fact]
    public async Task ItDownloadsAndThenSpeaks()
    {
        Assert.SkipUnless(Enabled, "set D47_TTS_LIVE=1 to run tests that download and synthesise");

        using var installer = new ChatterboxInstaller(Folder, NullLogger<ChatterboxInstaller>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        var result = await installer.InstallAsync(cancellationToken: timeout.Token);

        Assert.True(
            result.Outcome is KokoroInstall.Installed or KokoroInstall.AlreadyPresent,
            $"{result.Outcome}: {result.Detail}");
        Assert.True(ChatterboxAssets.IsInstalled(Folder));

        using var provider = new ChatterboxTtsProvider(Folder, Voices, Path.Combine(Path.GetTempPath(), "d47-chatterbox-fetched"), NullLogger<ChatterboxTtsProvider>.Instance);

        var voices = await provider.ListVoicesAsync(timeout.Token);
        Assert.Contains(voices.Voices, voice => voice.Id == "marlow");

        var clip = await provider.SynthesizeAsync(
            "Docking granted at Shinrarta Dezhra. [chuckle] Mind the Anaconda on approach, Commander.",
            new VoiceSelection("marlow"),
            timeout.Token);

        Assert.Equal(AudioFormat.Standard, clip.Format);

        var seconds = clip.Pcm.Length / 2.0 / 48_000;
        Assert.True(seconds > 2.0, $"only {seconds:F2}s of audio");

        var wav = Path.Combine(Folder, "spoken.wav");
        File.WriteAllBytes(wav, WavWriter.ToBytes(clip.Pcm.Span, clip.Format));
    }
}
