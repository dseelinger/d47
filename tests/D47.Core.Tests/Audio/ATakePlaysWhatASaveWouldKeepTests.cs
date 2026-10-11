using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ATakePlaysWhatASaveWouldKeepTests
{
    private const int Rate = 48_000;

    private readonly MemoryInstall _install = new();

    private CustomVoices Store() => new(_install.Paths.Data, _install.Files, new DpapiSecretProtector());

    private static float[] Recording(double quiet, double loud) =>
    [
        .. new float[(int)(quiet * Rate)],
        .. Enumerable.Range(0, (int)(loud * Rate)).Select(i => 0.1f + (0.02f * (i / Rate))),
    ];

    [Fact]
    public void TheKeptSpanStartsAtTheFirstSoundAndRunsSevenSeconds()
    {
        var (start, length) = CustomVoices.Kept(Recording(2, 40), Rate);

        Assert.Equal(2 * Rate, start);
        Assert.Equal(7 * Rate, length);
    }

    [Fact]
    public void AShortTakeKeepsAllOfItsSound()
    {
        var (start, length) = CustomVoices.Kept(Recording(1, 6), Rate);

        Assert.Equal(Rate, start);
        Assert.Equal(6 * Rate, length);
    }

    [Fact]
    public void ThePreviewIsTheAudioTheSaveKeeps()
    {
        var store = Store();
        var take = Recording(2, 28);

        var preview = CustomVoices.Preview("take", take, Rate);
        Assert.Null(store.Save("Mum", "", "mid", "even", take, Rate, out var id));

        Assert.Equal(new AudioFormat(CustomVoices.SampleRate, 1), preview.Format);
        Assert.Equal(store.Clip(id!)!.Pcm.ToArray(), preview.Pcm.ToArray());
    }

    [Fact]
    public void ASilentTakeIsRefusedBeforeASaveIsTried()
    {
        var refused = CustomVoices.Refuse(new float[10 * Rate], Rate);

        Assert.NotNull(refused);
        Assert.Contains("louder than -40 dBFS", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameInUseIsRefusedButNotForTheVoiceThatHasIt()
    {
        var store = Store();
        Assert.Null(store.Save("Mum", "", "mid", "even", Recording(0, 8), Rate, out var id));

        Assert.Equal("A custom voice is already named \"mum\".", store.NameProblem(" mum "));
        Assert.Null(store.NameProblem("Mum", except: id));
        Assert.Equal("A voice name is 1 to 40 characters.", store.NameProblem("  "));
    }
}
