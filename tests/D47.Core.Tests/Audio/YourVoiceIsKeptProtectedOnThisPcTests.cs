using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class YourVoiceIsKeptProtectedOnThisPcTests
{
    private readonly MemoryInstall _install = new();

    private OwnVoice Store() => new(_install.Paths.Data, _install.Files, new DpapiSecretProtector());

    /// <summary>A 220 Hz tone at half scale.</summary>
    private static float[] Tone(double seconds, int rate) =>
        [.. Enumerable.Range(0, (int)(seconds * rate)).Select(i => 0.5f * (float)Math.Sin(2 * Math.PI * 220 * i / rate))];

    [Fact]
    public void SixSecondsAt48kHzIsSavedAndDecryptsTo24kHzMono()
    {
        var store = Store();

        Assert.Null(store.Save(Tone(6, 48_000), 48_000));

        var clip = store.Clip();

        Assert.NotNull(clip);
        Assert.Equal(new AudioFormat(24_000, 1), clip.Format);
        Assert.Equal(TimeSpan.FromSeconds(6), clip.Duration);

        var samples = store.Load();

        Assert.NotNull(samples);
        Assert.Equal(6 * 24_000, samples.Length);
        Assert.InRange(samples.Skip(1_000).Take(10_000).Max(), 0.45f, 0.55f);
    }

    [Fact]
    public void FourSecondsIsRefusedWithTheReason()
    {
        var store = Store();

        var refused = store.Save(Tone(4, 48_000), 48_000);

        Assert.NotNull(refused);
        Assert.Contains("4.0 s long and needs at least 5 s", refused, StringComparison.Ordinal);
        Assert.False(store.Exists);
    }

    [Fact]
    public void SilenceIsRefusedWithTheReason()
    {
        var store = Store();
        var quiet = new float[6 * 48_000];
        Array.Fill(quiet, 0.009f);

        var refused = store.Save(quiet, 48_000);

        Assert.NotNull(refused);
        Assert.Contains("louder than -40 dBFS", refused, StringComparison.Ordinal);
        Assert.False(store.Exists);
    }

    [Fact]
    public void MoreThanSevenSecondsKeepsTheFirstSeven()
    {
        var store = Store();

        Assert.Null(store.Save(Tone(9, 44_100), 44_100));
        Assert.Equal(7 * 24_000, store.Load()!.Length);
    }

    [Fact]
    public void NoPlaintextWavIsWrittenUnderData()
    {
        var store = Store();

        store.Save(Tone(6, 48_000), 48_000);
        _ = store.Load();
        _ = store.Clip();

        Assert.Equal(Path.Combine(_install.Paths.Data, "voice", "own.bin"), store.FilePath);
        Assert.Empty(_install.Files.Enumerate(_install.Paths.Data, "*.wav", recursive: true));

        var written = Assert.Single(_install.Files.Enumerate(_install.Paths.Data, "*", recursive: true));
        var bytes = _install.Files.ReadBytes(written)!;

        Assert.NotEqual("RIFF"u8.ToArray(), bytes.Take(4).ToArray());
        Assert.NotEqual(store.Clip()!.Pcm.Length, bytes.Length);
    }

    [Fact]
    public void ARecordingFromAnotherWindowsUserDoesNotLoad()
    {
        new OwnVoice(_install.Paths.Data, _install.Files, new NeverUnprotects()).Save(Tone(6, 24_000), 24_000);

        var other = new OwnVoice(_install.Paths.Data, _install.Files, new NeverUnprotects());

        Assert.True(other.Exists);
        Assert.Null(other.Load());
    }

    [Fact]
    public void DeleteRemovesTheFileAndChangesTheVersion()
    {
        var store = Store();
        var changes = 0;
        store.Changed += () => changes++;

        store.Save(Tone(6, 48_000), 48_000);
        var saved = store.Version;
        store.Delete();

        Assert.Null(_install.Files.Stat(store.FilePath));
        Assert.Null(store.Load());
        Assert.NotEqual(saved, store.Version);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void TheEgressEntrySaysWhereItIsKeptAndThatNothingIsSent()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.OwnVoice, new D47Settings(), llmKeyPresent: true);

        Assert.Contains(EgressDisclosure.OwnVoice, EgressDisclosure.Ids);
        Assert.False(entry.Active);
        Assert.Equal("Your recorded voice", entry.Name);
        Assert.Contains("data\\voice\\own.bin", entry.What, StringComparison.Ordinal);
        Assert.Contains("encrypted for your Windows user, and never sent anywhere", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void AShippedVoiceCannotTakeTheOwnId()
    {
        var files = _install.Files;
        var folder = Path.Combine(_install.Root, "voices");
        files.WriteText(
            Path.Combine(folder, ChatterboxVoices.TableName),
            "id\tname\tgender\tlocale\trole\tsource\nown\tOwn\tfemale\ten\t\ta clip\n");
        files.WriteBytes(
            Path.Combine(folder, "own.wav"),
            WavWriter.ToBytes(Tone(6, ChatterboxVoices.SampleRate), ChatterboxVoices.SampleRate));

        Assert.Empty(ChatterboxVoices.Load(files, folder,Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance));
    }
}
