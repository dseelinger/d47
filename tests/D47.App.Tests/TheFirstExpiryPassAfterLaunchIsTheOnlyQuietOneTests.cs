using Xunit;

namespace D47.App.Tests;

public class TheFirstExpiryPassAfterLaunchIsTheOnlyQuietOneTests
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(10);
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheFirstPassRunsOnTheFirstCallAndIsMarkedFirst()
    {
        var pacing = new MemoryExpiryPacing(Every);

        Assert.True(pacing.TryBegin(Start, out var first));
        Assert.True(first);
    }

    [Fact]
    public void TheFirstPassIsStillFirstWhenTheTickThatRunsItIsNotTickOne()
    {
        var pacing = new MemoryExpiryPacing(Every);

        Assert.True(pacing.TryBegin(Start.AddMilliseconds(100), out var first));
        Assert.True(first);
    }

    [Fact]
    public void ALaterPassIsNotFirst()
    {
        var pacing = new MemoryExpiryPacing(Every);
        pacing.TryBegin(Start, out _);

        Assert.True(pacing.TryBegin(Start + Every, out var first));
        Assert.False(first);
    }

    [Fact]
    public void NoPassIsDueBeforeTheIntervalHasElapsed()
    {
        var pacing = new MemoryExpiryPacing(Every);
        pacing.TryBegin(Start, out _);

        Assert.False(pacing.TryBegin(Start + Every - TimeSpan.FromSeconds(1), out _));
    }
}
