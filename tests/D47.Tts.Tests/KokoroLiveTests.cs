using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public class KokoroLiveTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("D47_TTS_LIVE") == "1";

    /// <summary>Beside the installed build's models, so neither re-downloads what the other has.</summary>
    private static string Folder =>
        Environment.GetEnvironmentVariable("D47_KOKORO_FOLDER")
        ?? Path.Combine(Path.GetTempPath(), "d47-kokoro");

    [Fact]
    public async Task ItDownloadsAndThenSpeaks()
    {
        Assert.SkipUnless(Enabled, "set D47_TTS_LIVE=1 to run tests that download and synthesise");

        using var installer = new KokoroInstaller(Folder, NullLogger<KokoroInstaller>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));

        var result = await installer.InstallAsync(cancellationToken: timeout.Token);

        Assert.True(
            result.Outcome is KokoroInstall.Installed or KokoroInstall.AlreadyPresent,
            $"{result.Outcome}: {result.Detail}");

        Assert.True(KokoroAssets.IsInstalled(Folder));

        using var provider = new KokoroTtsProvider(Folder, NullLogger<KokoroTtsProvider>.Instance);

        var voices = await provider.ListVoicesAsync(timeout.Token);

        Assert.Equal(28, voices.Voices.Count);
        Assert.Contains(voices.Voices, voice => voice.Id == "bm_george");

        // A line with the two hard cases in it: a system name no dictionary holds, and a designation that has
        // to be spelled.
        var clip = await provider.SynthesizeAsync(
            "Docking granted at Shinrarta Dezhra. Route via COL 385 SECTOR B0-GQPI.",
            new VoiceSelection("bm_george"),
            timeout.Token);

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.True(clip.Pcm.Length > 0, "nothing was synthesised");

        // 48 kHz, 16-bit mono: two bytes a sample.
        var seconds = clip.Pcm.Length / 2.0 / 48_000;

        Assert.True(seconds > 2.0, $"only {seconds:F2}s of audio");

        var wav = Path.Combine(Folder, "spoken.wav");
        WriteWav(wav, clip.Pcm.Span);

        // Deliberately loud: the point of this test is that a person then listens to the file.
        Assert.True(File.Exists(wav), $"wrote {seconds:F2}s to {wav}");
    }

    /// <summary>16-bit mono PCM at the arbiter's rate, so the result can be played.</summary>
    private static void WriteWav(string path, ReadOnlySpan<byte> pcm)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        const int Rate = 48_000;

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(Rate);
        writer.Write(Rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
    }
}
