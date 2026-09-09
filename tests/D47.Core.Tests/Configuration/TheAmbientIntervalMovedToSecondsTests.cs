using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The ambient interval was in minutes and is now in seconds.</summary>
public class TheAmbientIntervalMovedToSecondsTests
{
    [Fact]
    public void AChosenIntervalIsCarriedOverToTheSecond()
    {
        var loaded = Load("""{ "callouts": { "ambientMinutes": 30 } }""");

        Assert.Equal(1800, loaded.Callouts.AmbientSeconds);
    }

    /// <summary>
    /// Fifteen minutes is what every file got without anyone asking for it, so it is read as a default
    /// rather than as a decision — carrying it forward would mean the new default reached nobody who
    /// had ever run d47.
    /// </summary>
    [Fact]
    public void TheOldDefaultGivesWayToTheNewOne()
    {
        var loaded = Load("""{ "callouts": { "ambientMinutes": 15 } }""");

        Assert.Equal(new CalloutSettings().AmbientSeconds, loaded.Callouts.AmbientSeconds);
    }

    [Fact]
    public void SilenceStaysSilence()
    {
        // Zero is the one value that means something other than a length.
        var loaded = Load("""{ "callouts": { "ambientMinutes": 0 } }""");

        Assert.Equal(0, loaded.Callouts.AmbientSeconds);
    }

    [Fact]
    public void TheOldKeyIsClearedSoTheConversionHappensOnce()
    {
        var loaded = Load("""{ "callouts": { "ambientMinutes": 30 } }""");

        Assert.Null(loaded.Callouts.AmbientMinutes);
    }

    [Fact]
    public void AFileWrittenSinceIsLeftAlone()
    {
        var loaded = Load("""{ "callouts": { "ambientSeconds": 90 } }""");

        Assert.Equal(90, loaded.Callouts.AmbientSeconds);
    }

    /// <summary>
 /// The ceiling arrived after the floor, so a file written before it carries only the floor —
    /// and a floor above the new ceiling's default reads as a pinned cadence, which is exactly what
    /// that file already had.
    /// </summary>
    [Fact]
    public void AFileFromBeforeTheCeilingKeepsTheCadenceItChose()
    {
        var loaded = Load("""{ "callouts": { "ambientSeconds": 1800 } }""");

        Assert.Equal(1800, loaded.Callouts.AmbientSeconds);
        Assert.Equal(600, loaded.Callouts.AmbientMaxSeconds);

        // Which is the whole point: a floor above the ceiling's default reads as the minimum, so that file
        // goes on ticking at exactly the cadence it was already ticking at.
        Assert.True(loaded.Callouts.AmbientSeconds > loaded.Callouts.AmbientMaxSeconds);
    }

    [Fact]
    public void TheDefaultsAreTheOnesTheCommanderAskedFor()
    {
        var fresh = new CalloutSettings();

        // Set by hand and flown before they were written down (2026-09-02). #258 said the spread's numbers
        // could only come by ear, and these are the ones that came.
        Assert.Equal(300, fresh.AmbientSeconds);
        Assert.Equal(600, fresh.AmbientMaxSeconds);
        Assert.Equal(300, fresh.NpcChatterSeconds);
        Assert.Equal(600, fresh.NpcChatterMaxSeconds);
        Assert.Equal(3, fresh.RouteEveryNJumps);
        Assert.Equal(30, fresh.LongJumpSeconds);
    }

    private static D47Settings Load(string json)
    {
        using var install = new TempInstall();

        File.WriteAllText(install.Paths.SettingsFile, json);

        return new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance).Load();
    }
}
