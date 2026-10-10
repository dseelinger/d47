using D47.Core.Storage;
using D47.Core;
using D47.App.Timekeeping;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Timers and alarms exist only for a run started with the switch (#90).</summary>
[Trait("Category", "Integration")]
public class TimersAndAlarmsAreOffUnlessARunAsksTests : IDisposable
{
    /// <summary>The switch and the variable are process-wide state, so both are put back.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        TimersAndAlarms.ReadCommandLine([]);
        Environment.SetEnvironmentVariable(TimersAndAlarms.EnvironmentVariable, null);
    }

    [Fact]
    public void AnOrdinaryLaunchLeavesThemOff()
    {
        TimersAndAlarms.ReadCommandLine(["--selftest"]);

        Assert.False(TimersAndAlarms.Enabled);
    }

    [Fact]
    public void TheSwitchTurnsThemOn()
    {
        TimersAndAlarms.ReadCommandLine(["--utilities"]);

        Assert.True(TimersAndAlarms.Enabled);
    }

    [Fact]
    public void TheVariableTurnsThemOn()
    {
        TimersAndAlarms.ReadCommandLine([]);
        Environment.SetEnvironmentVariable(TimersAndAlarms.EnvironmentVariable, "1");

        Assert.True(TimersAndAlarms.Enabled);
    }

    [Theory]
    [InlineData("--utility")]
    [InlineData("--Utilities")]
    [InlineData("utilities")]
    public void ANearMissIsNotTheSwitch(string argument)
    {
        TimersAndAlarms.ReadCommandLine([argument]);

        Assert.False(TimersAndAlarms.Enabled);
    }

    /// <summary>Per run: nothing is written to settings, and the next launch without the switch is off again.</summary>
    [Trait("Category", "Integration")]
    [Fact]
    public void TheSwitchLastsOneRunAndIsNotSaved()
    {
        var paths = Paths();

        TimersAndAlarms.ReadCommandLine([TimersAndAlarms.Flag]);
        Assert.NotNull(TimersAndAlarms.Create(paths, new DiskFileSystem(), NullLoggerFactory.Instance));

        Assert.False(File.Exists(paths.SettingsFile));

        TimersAndAlarms.ReadCommandLine([]);
        Assert.Null(TimersAndAlarms.Create(paths, new DiskFileSystem(), NullLoggerFactory.Instance));
    }

    /// <summary>Off: no stores, and the game-state block still carries both dates with no reminder list.</summary>
    [Trait("Category", "Integration")]
    [Fact]
    public void OffNothingIsComposedAndTheGameStateStillCarriesTheDate()
    {
        TimersAndAlarms.ReadCommandLine([]);

        var clocks = TimersAndAlarms.Create(Paths(), new DiskFileSystem(), NullLoggerFactory.Instance);
        var live = TimersAndAlarms.Live(clocks, new DateTimeOffset(2026, 8, 17, 21, 4, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        Assert.Null(clocks);
        Assert.NotNull(live);
        Assert.StartsWith("Date: ", live, StringComparison.Ordinal);
        Assert.Contains("The Commander's own clock reads", live, StringComparison.Ordinal);
        Assert.DoesNotContain("Running:", live, StringComparison.Ordinal);
    }

    /// <summary>On: the game-state block carries both dates and what is running, as it always has.</summary>
    [Trait("Category", "Integration")]
    [Fact]
    public void OnTheGameStateCarriesBothDatesAndWhatIsRunning()
    {
        TimersAndAlarms.ReadCommandLine([TimersAndAlarms.Flag]);

        var now = new DateTimeOffset(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);
        var clocks = TimersAndAlarms.Create(Paths(), new DiskFileSystem(), NullLoggerFactory.Instance);

        Assert.NotNull(clocks);
        clocks.Timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), now);

        var live = TimersAndAlarms.Live(clocks, now, TimeZoneInfo.Utc);

        Assert.NotNull(live);
        Assert.Contains("3312", live, StringComparison.Ordinal);
        Assert.Contains("2026", live, StringComparison.Ordinal);
        Assert.Contains("Running: mining run", live, StringComparison.Ordinal);
    }

    private static AppPaths Paths()
    {
        var paths = new AppPaths(TempFolders.Create("d47-timers-switch"));
        paths.EnsureCreated();

        return paths;
    }
}
