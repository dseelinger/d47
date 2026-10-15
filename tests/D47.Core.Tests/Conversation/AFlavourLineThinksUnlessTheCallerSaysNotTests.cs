using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A flavour line is asked with thinking unless its caller turns it off.</summary>
public class AFlavourLineThinksUnlessTheCallerSaysNotTests
{
    private static Task<FlavourReply> AskForAsync(FakeLlmProvider provider, bool? thinking) =>
        thinking is { } allowed
            ? FlavourTurn.AskForAsync(
                provider,
                model: null,
                persona: null,
                aboutMe: null,
                instruction: "Make one short remark.",
                gameState: null,
                spend: null,
                prices: null,
                logger: null,
                cancellationToken: TestContext.Current.CancellationToken,
                thinking: allowed)
            : FlavourTurn.AskForAsync(
                provider,
                model: null,
                persona: null,
                aboutMe: null,
                instruction: "Make one short remark.",
                gameState: null,
                spend: null,
                prices: null,
                logger: null,
                cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task ByDefaultTheModelMayThinkAtLow()
    {
        var provider = FakeLlmProvider.Answering("Quiet out here.");

        await AskForAsync(provider, thinking: null);

        Assert.True(provider.LastRequest!.ThinkingAllowed);
        Assert.Equal(ThinkingEffort.Low, provider.LastRequest!.Effort);
    }

    [Fact]
    public async Task ACallerCanAskWithoutThinking()
    {
        var provider = FakeLlmProvider.Answering("Quiet out here.");

        await AskForAsync(provider, thinking: false);

        Assert.False(provider.LastRequest!.ThinkingAllowed);
        Assert.Equal(ThinkingEffort.Low, provider.LastRequest!.Effort);
    }

    [Fact]
    public void ARewordedLineHasFourSecondsAndANarrationFifteen()
    {
        var rewording = new Rewording(new RewordChance(), logger: null);

        Assert.Equal(TimeSpan.FromSeconds(4), rewording.Budget);
        Assert.Equal(TimeSpan.FromSeconds(15), rewording.NarrationBudget);
    }

    [Fact]
    public void OnlyANarrationIsAskedWithThinking()
    {
        Assert.True(Rewording.IsNarration(new Announcement($"{NarratorCallout.KeyPrefix}opening", "It began.")));
        Assert.False(Rewording.IsNarration(new Announcement("recap.last-session", "Welcome back.")));
        Assert.False(Rewording.IsNarration(new Announcement("continuity.resume", "Resuming.")));
    }
}
