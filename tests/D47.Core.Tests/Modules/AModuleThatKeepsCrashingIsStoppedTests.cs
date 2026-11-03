using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class AModuleThatKeepsCrashingIsStoppedTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ThreeExitsAreRestartedAfterTwoTenAndThirtySeconds()
    {
        var restarts = new ModuleRestarts();

        Assert.Equal(TimeSpan.FromSeconds(2), restarts.Exited(Start));
        Assert.Equal(TimeSpan.FromSeconds(10), restarts.Exited(Start.AddMinutes(1)));
        Assert.Equal(TimeSpan.FromSeconds(30), restarts.Exited(Start.AddMinutes(2)));
    }

    [Fact]
    public void TheFourthExitWithinTenMinutesStopsIt()
    {
        var restarts = new ModuleRestarts();

        restarts.Exited(Start);
        restarts.Exited(Start.AddMinutes(3));
        restarts.Exited(Start.AddMinutes(6));

        Assert.Null(restarts.Exited(Start.AddMinutes(9)));
    }

    [Fact]
    public void AnExitTenMinutesOldNoLongerCounts()
    {
        var restarts = new ModuleRestarts();

        restarts.Exited(Start);
        restarts.Exited(Start.AddMinutes(3));
        restarts.Exited(Start.AddMinutes(6));

        Assert.Equal(TimeSpan.FromSeconds(30), restarts.Exited(Start.AddMinutes(10)));
    }

    [Fact]
    public void ExitsSpreadOverHoursAreAlwaysRestartedAfterTwoSeconds()
    {
        var restarts = new ModuleRestarts();

        for (var hour = 0; hour < 6; hour++)
        {
            Assert.Equal(TimeSpan.FromSeconds(2), restarts.Exited(Start.AddHours(hour)));
        }
    }

    [Fact]
    public void RestartClearsTheCount()
    {
        var restarts = new ModuleRestarts();

        restarts.Exited(Start);
        restarts.Exited(Start.AddSeconds(10));
        restarts.Exited(Start.AddSeconds(30));
        Assert.Null(restarts.Exited(Start.AddSeconds(70)));

        restarts.Clear();

        Assert.Equal(TimeSpan.FromSeconds(2), restarts.Exited(Start.AddSeconds(80)));
    }
}
