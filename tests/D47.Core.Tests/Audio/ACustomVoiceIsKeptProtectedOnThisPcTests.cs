using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ACustomVoiceIsKeptProtectedOnThisPcTests : IDisposable
{
    private readonly TempInstall _install = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _install.Dispose();
    }

    private CustomVoices Store() => new(_install.Paths.Data, new DpapiSecretProtector());

    private static float[] Tone(double seconds, int rate) =>
        [.. Enumerable.Range(0, (int)(seconds * rate)).Select(i => 0.5f * (float)Math.Sin(2 * Math.PI * 220 * i / rate))];

    [Fact]
    public void NoPlaintextAudioIsWrittenUnderData()
    {
        var store = Store();

        Assert.Null(store.Save("Mum", "female", "low", "slow", Tone(6, 48_000), 48_000, out var id));
        _ = store.Load(id!);
        _ = store.Clip(id!);

        Assert.Empty(Directory.GetFiles(_install.Paths.Data, "*.wav", SearchOption.AllDirectories));

        var audio = Assert.Single(Directory.GetFiles(store.Folder, "*.bin"));
        var bytes = File.ReadAllBytes(audio);

        Assert.Equal(id + ".bin", Path.GetFileName(audio));
        Assert.NotEqual("RIFF"u8.ToArray(), bytes.Take(4).ToArray());
        Assert.NotEqual(store.Clip(id!)!.Pcm.Length, bytes.Length);
    }

    [Fact]
    public void ASavedVoiceListsWithItsBandsAndDecryptsTo24kHzMono()
    {
        var store = Store();

        Assert.Null(store.Save("  Mum  ", "female", null, null, Tone(6, 48_000), 48_000, out var id));

        var voice = Assert.Single(store.List());

        Assert.Matches("^my-[0-9a-f]{8}$", voice.Id);
        Assert.Equal(id, voice.Id);
        Assert.Equal(("Mum", "female", "mid", "even", "en"), (voice.Name, voice.Gender, voice.Pitch, voice.Pace, voice.Locale));
        Assert.InRange(store.Load(id!)!.Length, (6 * 24_000) - 100, 6 * 24_000);
        Assert.Equal(new AudioFormat(24_000, 1), store.Clip(id!)!.Format);
    }

    [Fact]
    public void ANameIsOneToFortyCharactersAndUniqueIgnoringCase()
    {
        var store = Store();

        Assert.Null(store.Save("Mum", "", "mid", "even", Tone(6, 24_000), 24_000));
        Assert.NotNull(store.Save("mUM", "", "mid", "even", Tone(6, 24_000), 24_000));
        Assert.NotNull(store.Save("  ", "", "mid", "even", Tone(6, 24_000), 24_000));
        Assert.NotNull(store.Save(new string('x', 41), "", "mid", "even", Tone(6, 24_000), 24_000));
        Assert.Null(store.Save(new string('x', 40), "male", "high", "brisk", Tone(6, 24_000), 24_000));
        Assert.Equal(2, store.List().Count);
    }

    [Fact]
    public void RenameKeepsTheAudioAndRefusesATakenName()
    {
        var store = Store();
        store.Save("Mum", "", "mid", "even", Tone(6, 24_000), 24_000, out var mum);
        store.Save("Dad", "", "mid", "even", Tone(6, 24_000), 24_000, out var dad);
        var version = store.Version(mum!);

        Assert.NotNull(store.Rename(mum!, "DAD"));
        Assert.Null(store.Rename(mum!, "MUM"));
        Assert.Equal("MUM", store.List().Single(voice => voice.Id == mum).Name);
        Assert.Equal(version, store.Version(mum!));
        Assert.NotNull(store.Load(mum!));
        Assert.NotNull(store.Load(dad!));
    }

    [Fact]
    public void DeleteRemovesTheFileAndChangesTheVersion()
    {
        var store = Store();
        var changes = 0;
        store.Changed += () => changes++;

        store.Save("Mum", "", "mid", "even", Tone(6, 24_000), 24_000, out var id);
        var saved = store.Version(id!);
        store.Delete(id!);

        Assert.Empty(store.List());
        Assert.False(File.Exists(Path.Combine(store.Folder, id + ".bin")));
        Assert.Null(store.Load(id!));
        Assert.NotEqual(saved, store.Version(id!));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void AVoiceFromAnotherWindowsUserDoesNotLoad()
    {
        new CustomVoices(_install.Paths.Data, new NeverUnprotects())
            .Save("Mum", "", "mid", "even", Tone(6, 24_000), 24_000, out var id);

        var other = new CustomVoices(_install.Paths.Data, new NeverUnprotects());

        Assert.Single(other.List());
        Assert.Null(other.Load(id!));
        Assert.Null(other.Clip(id!));
    }

    [Theory]
    [InlineData("../own")]
    [InlineData("my-ABCDEF01")]
    [InlineData("my-1234567")]
    [InlineData("my-123456789")]
    public void OnlyAWellFormedIdReachesTheDisk(string id)
    {
        Assert.False(CustomVoices.IsId(id));
        Assert.Null(Store().Load(id));
    }

    [Fact]
    public void TheEgressEntrySaysWhereTheyAreKeptAndThatNothingIsSent()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CustomVoices, new D47Settings(), llmKeyPresent: true);

        Assert.Contains(EgressDisclosure.CustomVoices, EgressDisclosure.Ids);
        Assert.False(entry.Active);
        Assert.Equal("Your custom voices", entry.Name);
        Assert.Contains("data\\voices\\custom", entry.What, StringComparison.Ordinal);
        Assert.Contains("encrypted for your Windows user", entry.What, StringComparison.Ordinal);
        Assert.Contains("never sent anywhere and never included in a donation", entry.What, StringComparison.Ordinal);
    }
}
