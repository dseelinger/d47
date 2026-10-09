using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

[Trait("Category", "Integration")]
public class TheMissionBoardIsAnsweredWithoutTheModelTests
{
    [Theory]
    [InlineData("mission board")]
    [InlineData("what missions do I have")]
    [InlineData("read my missions")]
    [InlineData("what am I hauling")]
    public async Task TheKeywordRouterAnswersWithNoModelCall(string phrase)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("The model should not be asked.");

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        TurnResult? result = null;
        var text = new System.Text.StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(phrase, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
            else if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        Assert.Equal(TurnRoute.KeywordRouter, result.Route);
        Assert.Null(provider.LastRequest);
        Assert.Contains("no missions", text.ToString(), StringComparison.Ordinal);
    }
}
