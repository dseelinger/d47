using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The ambient interval was in minutes and is now in seconds.
/// <para>
/// Minutes could not express the interesting end of the range — the difference between a
/// companion that speaks up now and then and one that never stops is finer than a minute — so
/// the row changed unit. Every settings file already on disk holds the old key, and unknown keys
/// are refused on load, so the old one is still a property: read once, converted, and cleared.
/// </para>
/// </summary>
public class TheAmbientIntervalMovedToSecondsTests
{
    [Fact]
    public void AChosenIntervalIsCarriedOverToTheSecond()
    {
        var loaded = Load("""{ "callouts": { "ambientMinutes": 30 } }""");

        Assert.Equal(1800, loaded.Callouts.AmbientSeconds);
    }

    /// <summary>
    /// Fifteen minutes is what every file got without anyone asking for it, so it is read as a
    /// default rather than as a decision — carrying it forward would mean the new default
    /// reached nobody who had ever run d47.
    /// </summary>
    [Fact]
    public void TheOldDefaultGivesWayToTheNewOne()
    {
        var loaded = Load("""{ "callouts": { "ambientMinutes": 15 } }""");

        Assert.Equal(45, loaded.Callouts.AmbientSeconds);
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

    [Fact]
    public void TheDefaultsAreTheOnesTheCommanderAskedFor()
    {
        var fresh = new CalloutSettings();

        Assert.Equal(45, fresh.AmbientSeconds);
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
