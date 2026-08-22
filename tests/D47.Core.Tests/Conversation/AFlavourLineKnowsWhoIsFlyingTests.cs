using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The Commander's own account of themselves reaches the lines said in character (list.md Phase
/// 43, "About Me reaches the conversation and nothing else").
/// <para>
/// Before this, <c>FlavourTurn.AskAsync</c> took provider, model, persona, instruction and game
/// state and had no parameter for it — so every ambient remark, the opening line and a core's
/// introduction were written by a model that had never heard of the person flying.
/// </para>
/// </summary>
public class AFlavourLineKnowsWhoIsFlyingTests
{
    [Fact]
    public async Task TheStoryIsPositionFourOfTheFlavourPrompt()
    {
        var provider = FakeLlmProvider.Answering("Quiet out here, Reyes.");

        var line = await FlavourTurn.AskAsync(
            provider,
            model: null,
            persona: "You are Warden.",
            aboutMe: "Commander Reyes. Born on Achenar 6C, 2041. Imperial accent.",
            instruction: "Make one short remark.",
            gameState: "Docked at Jameson Memorial.",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Quiet out here, Reyes.", line);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Equal("Commander Reyes. Born on Achenar 6C, 2041. Imperial accent.", prompt.AboutMe);

        // In the cached block, after the persona and under the committing label — the same place
        // and the same words a turn puts it, so the ship's AI is the same AI in both.
        var block = prompt.RenderCachedSystemBlock();
        Assert.True(
            block.IndexOf("You are Warden.", StringComparison.Ordinal)
            < block.IndexOf(PromptAssembly.AboutMeLabel, StringComparison.Ordinal)
            && block.IndexOf(PromptAssembly.AboutMeLabel, StringComparison.Ordinal)
            < block.IndexOf("Achenar 6C", StringComparison.Ordinal));
    }

    /// <summary>
    /// Null is a real argument, not a default: a call that is not the ship's AI speaking to the
    /// Commander — the carrier, a lore lookup, the voice casting question — carries nothing.
    /// </summary>
    [Fact]
    public async Task NullCarriesNothing()
    {
        var provider = FakeLlmProvider.Answering("Jump complete, Commander.");

        await FlavourTurn.AskAsync(
            provider,
            model: null,
            persona: "You are the tower controller.",
            aboutMe: null,
            instruction: "Say this in your own words.",
            gameState: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(provider.LastRequest!.Prompt.AboutMe);
        Assert.DoesNotContain(PromptAssembly.AboutMeLabel, provider.LastRequest.Prompt.RenderCachedSystemBlock(), StringComparison.Ordinal);
    }
}
