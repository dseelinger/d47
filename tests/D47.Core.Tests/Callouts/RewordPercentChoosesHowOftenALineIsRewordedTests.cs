using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Whether a line with a brief is put to the model at all, or spoken as written (#214).</summary>
public class RewordPercentChoosesHowOftenALineIsRewordedTests
{
    private static Announcement Routine(string key) => new(key, "As written.");

    [Fact]
    public void ZeroPercentNeverRewords()
    {
        var chance = new RewordChance(new Random(1));

        for (var i = 0; i < 50; i++)
        {
            Assert.False(chance.ShouldReword(Routine("route.progress"), rewordPercent: 0));
        }
    }

    [Fact]
    public void OneHundredPercentAlwaysRewords()
    {
        var chance = new RewordChance(new Random(1));

        for (var i = 0; i < 50; i++)
        {
            Assert.True(chance.ShouldReword(Routine("route.progress"), rewordPercent: 100));
        }
    }

    [Fact]
    public void AnAmbientKeyAlwaysRewordsWhateverThePercent()
    {
        var chance = new RewordChance(new Random(1));
        var ambient = new Announcement($"{AmbientCallout.KeyPrefix}Docked", "Quiet out here.");

        for (var i = 0; i < 50; i++)
        {
            Assert.True(chance.ShouldReword(ambient, rewordPercent: 0));
        }
    }

    /// <summary>
    /// The chance only ever applies to a line that has a brief in the first place — a danger callout and
    /// a routine one keep going out as authored, whatever the percent.
    /// </summary>
    [Theory]
    [InlineData("danger.shields")]
    [InlineData("route.progress")]
    public void ADangerOrRoutineKeyStillGetsNoBrief(string key)
    {
        Assert.Null(FlavourBriefs.For(Routine(key), personalityEnabled: true));
    }

    [Fact]
    public void OutOfRangePercentsAreClamped()
    {
        var alwaysBelow = new RewordChance(new Random(2));
        var alwaysAt = new RewordChance(new Random(2));

        for (var i = 0; i < 50; i++)
        {
            Assert.False(alwaysBelow.ShouldReword(Routine("route.progress"), rewordPercent: -10));
            Assert.True(alwaysAt.ShouldReword(Routine("route.progress"), rewordPercent: 200));
        }
    }
}
