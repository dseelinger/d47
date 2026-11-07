using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public sealed class TheAudioPlayerClockTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(102, "1:42")]
    [InlineData(258, "4:18")]
    [InlineData(3725, "62:05")]
    public void ATimeIsMinutesAndTwoDigitSeconds(int seconds, string shown) =>
        Assert.Equal(shown, AudioPlayerReadout.Clock(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void AnUnknownTimeHasItsOwnText() =>
        Assert.Equal("–:––", AudioPlayerReadout.Clock(null));
}
