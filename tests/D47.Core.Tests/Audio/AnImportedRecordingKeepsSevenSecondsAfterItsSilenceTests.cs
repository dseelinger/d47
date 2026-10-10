using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AnImportedRecordingKeepsSevenSecondsAfterItsSilenceTests
{
    private const int Rate = 24_000;

    private readonly MemoryInstall _install = new();

    private CustomVoices Store() => new(_install.Paths.Data, _install.Files, new DpapiSecretProtector());

    /// <summary>Silence, then sound whose level rises by 0.02 each second, so each second is recognisable.</summary>
    private static float[] Recording(double quiet, double loud) =>
    [
        .. new float[(int)(quiet * Rate)],
        .. Enumerable.Range(0, (int)(loud * Rate)).Select(i => 0.1f + (0.02f * (i / Rate))),
    ];

    [Fact]
    public void ThirtySecondsWithTwoOfSilenceKeepsSecondsTwoToNine()
    {
        var store = Store();

        Assert.Null(store.Save("Mum", "", "mid", "even", Recording(2, 28), Rate, out var id));

        var kept = store.Load(id!)!;

        Assert.Equal(7 * Rate, kept.Length);
        Assert.InRange(kept[Rate / 2], 0.095f, 0.105f);
        Assert.InRange(kept[(6 * Rate) + (Rate / 2)], 0.215f, 0.225f);
    }

    [Fact]
    public void UnderFiveSecondsOfSoundIsRefusedWithTheReason()
    {
        var store = Store();

        var refused = store.Save("Mum", "", "mid", "even", Recording(3, 4), Rate);

        Assert.NotNull(refused);
        Assert.Contains("4.0 s long and needs at least 5 s", refused, StringComparison.Ordinal);
        Assert.Empty(store.List());
    }

    [Fact]
    public void ASilentFileIsRefusedAsSilence()
    {
        var refused = Store().Save("Mum", "", "mid", "even", new float[10 * Rate], Rate);

        Assert.NotNull(refused);
        Assert.Contains("louder than -40 dBFS", refused, StringComparison.Ordinal);
    }
}
