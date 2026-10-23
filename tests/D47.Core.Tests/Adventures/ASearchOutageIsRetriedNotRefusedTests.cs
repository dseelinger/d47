using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class ASearchOutageIsRetriedNotRefusedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string Beats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    [Fact]
    public async Task OneFailureIsRetriedAndTheChapterIsWrittenWithNoRepairCall()
    {
        var time = new ManualTime();
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));
        var galaxy = new AdventureGeneratorTests.Galaxy { StationOutages = 1 };
        var generator = AdventureGeneratorTests.Generator(provider, galaxy, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, time);

        var outcome = await PumpAsync(generator.GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None), time);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public async Task ASecondFailureStopsTheWriteWithTheOutageAndStartsNoFurtherDraft()
    {
        var time = new ManualTime();
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));
        var galaxy = new AdventureGeneratorTests.Galaxy { StationOutages = 2 };
        var generator = AdventureGeneratorTests.Generator(provider, galaxy, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, time);
        var again = 0;

        var outcome = await PumpAsync(generator.DraftAsync(new AdventureAsk(Length: AdventureLength.Short), Now, _ => again++, CancellationToken.None), time);

        Assert.Null(outcome.Draft);
        Assert.False(outcome.Redraftable);
        Assert.Contains("galaxy search is unavailable", outcome.Message);
        Assert.Contains("The station search reported a server error.", outcome.Message);
        Assert.Equal(2, provider.CallCount);
        Assert.Equal(0, again);
    }

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
