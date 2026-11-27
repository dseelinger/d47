using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AFastSpinIsSaidInAUnitThatFitsTests
{
    [Theory]
    [InlineData("0.0864", "turning about 12 times a second")]
    [InlineData("0.5", "turning twice a second")]
    [InlineData("0.959931", "turning once every second")]
    [InlineData("1", "turning once every second")]
    [InlineData("38.203648", "turning once every 38 seconds")]
    [InlineData("59.6", "turning once every minute")]
    [InlineData("427.8", "turning once every 7 minutes")]
    [InlineData("1230.234111", "turning once every 21 minutes")]
    [InlineData("3576", "turning once every hour")]
    [InlineData("7200", "turning once every 2 hours")]
    [InlineData("16077", "turning once every 4 hours")]
    public void ThePeriodIsSaidInSecondsMinutesOrHours(string seconds, string said) =>
        Assert.Equal(said, NotableBodyCallout.SpinPhrase(seconds));
}
