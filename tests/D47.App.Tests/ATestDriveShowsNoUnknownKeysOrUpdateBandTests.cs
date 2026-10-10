using D47.App;
using Xunit;

namespace D47.App.Tests;

public class ATestDriveShowsNoUnknownKeysOrUpdateBandTests
{
    private static readonly string[] Keys = ["callouts.narratorD47Tips", "callouts.narratorEliteTips"];

    [Fact]
    public void AnInstalledBuildNamesTheKeysItDoesNotKnow()
    {
        var message = StartupProblems.UnknownKeys(false, "settings.json", Keys);

        Assert.Equal(
            "Settings at 'settings.json' hold 'callouts.narratorD47Tips', 'callouts.narratorEliteTips', which this build does not know; kept as written.",
            message);
    }

    [Fact]
    public void ATestDriveSaysNothingAboutKeysItDoesNotKnow()
    {
        Assert.Null(StartupProblems.UnknownKeys(true, "settings.json", Keys));
    }

    [Fact]
    public void NoUnknownKeysIsNoMessage()
    {
        Assert.Null(StartupProblems.UnknownKeys(false, "settings.json", []));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void ATestDriveNeverChecksForAnUpdateAtStartup(bool isTestDrive, bool checkOnStartup, bool expected)
    {
        Assert.Equal(expected, StartupProblems.ChecksForUpdateAtStartup(isTestDrive, checkOnStartup));
    }
}
