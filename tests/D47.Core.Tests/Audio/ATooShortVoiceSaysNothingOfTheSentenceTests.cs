using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ATooShortVoiceSaysNothingOfTheSentenceTests
{
    private const int Rate = 24_000;

    private static float[] ThreeSecondsOfSound() => [.. Enumerable.Repeat(0.1f, 3 * Rate)];

    [Fact]
    public void OwnVoiceNamesTheLengthAndTheMinimumOnly()
    {
        var reason = OwnVoice.Refuse(ThreeSecondsOfSound(), Rate);

        Assert.NotNull(reason);
        Assert.Contains("3.0 s long and needs at least 5 s", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("sentence", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACustomVoiceNamesTheLengthAndTheMinimumOnly()
    {
        var install = new MemoryInstall();
        var store = new CustomVoices(install.Paths.Data, install.Files, new DpapiSecretProtector());

        var reason = store.Save("Mum", "", "mid", "even", ThreeSecondsOfSound(), Rate);

        Assert.NotNull(reason);
        Assert.Contains("3.0 s long and needs at least 5 s", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("sentence", reason, StringComparison.OrdinalIgnoreCase);
    }
}
