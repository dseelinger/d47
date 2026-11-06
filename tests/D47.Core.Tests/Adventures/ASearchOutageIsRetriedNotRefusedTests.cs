using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class ASearchOutageIsRetriedNotRefusedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AFailedLookupIsRetriedAfterTheDelayAndLeavesNoOutage()
    {
        var time = new ManualTime();
        var galaxy = new AdventureGeneratorTests.Galaxy { StationOutages = 1 };
        var resolver = new AdventureResolver(galaxy) { Time = time };

        var resolution = await PumpAsync(
            resolver.ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", "Maren Anchorage", null, "Beat 1", null, CancellationToken.None), time);

        Assert.True(resolution.Succeeded);
        Assert.Null(resolver.Outage);
    }

    private static async Task<T> PumpAsync<T>(Task<T> task, ManualTime time)
    {
        for (var pass = 0; pass < 200 && !task.IsCompleted; pass++)
        {
            time.Advance(AdventureResolver.OutageRetryDelay);
            await Task.WhenAny(task, Task.Delay(10, TestContext.Current.CancellationToken));
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }
}
