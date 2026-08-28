using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// The local voice, end to end: downloaded, loaded, and actually speaking (#101).
/// <para>
/// Opt-in via <c>D47_TTS_LIVE=1</c>, because the first run fetches about 350 MB and CI has no
/// business doing that. It writes a WAV beside the model so the result can be <em>listened to</em>
/// rather than only measured — which is the lesson Phase 60 paid for: Cartesia shipped fully green
/// with nobody having heard it, and the verdict when somebody finally did was "it works, I am not
/// wowed".
/// </para>
/// <code>
/// D47_TTS_LIVE=1 dotnet test tests/D47.Tts.Tests --filter FullyQualifiedName~Kokoro
/// </code>
/// </summary>
public class KokoroLiveTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("D47_TTS_LIVE") == "1";

    /// <summary>
    /// Beside the installed build's own models, so a run here does not re-download what the app
    /// already has and vice versa.
    /// </summary>
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

        // A line with the two hard cases in it: a system name no dictionary holds, and a
        // designation that has to be spelled.
        var clip = await provider.SynthesizeAsync(
            "Docking granted at Shinrarta Dezhra. Route via COL 385 SECTOR B0-GQPI.",
            new VoiceSelection("bm_george"),
            timeout.Token);

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.True(clip.Pcm.Length > 0, "nothing was synthesised");

        // 48 kHz, 16-bit mono: two bytes a sample. A line that long is seconds rather than
        // milliseconds, and a near-empty clip is the failure this catches.
        var seconds = clip.Pcm.Length / 2.0 / 48_000;

        Assert.True(seconds > 2.0, $"only {seconds:F2}s of audio");

        var wav = Path.Combine(Folder, "spoken.wav");
        WriteWav(wav, clip.Pcm.Span);

        // Deliberately loud in the output, because the point of this test is that a person then
        // goes and listens to the file.
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
