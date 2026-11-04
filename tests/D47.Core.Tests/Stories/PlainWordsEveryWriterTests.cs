using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Logbook;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>Every prompt that writes text the Commander reads or hears carries the one plain-language rule.</summary>
public sealed class PlainWordsEveryWriterTests
{
    [Fact]
    public void TheTurnLoopGuardrailsCarryIt() =>
        Assert.Contains(PlainWords.Rule, Guardrails.Text, StringComparison.Ordinal);

    [Fact]
    public void TheStoryAsideCarriesIt() =>
        Assert.Contains(PlainWords.Rule, AdventureContext.Label, StringComparison.Ordinal);

    [Fact]
    public void NpcChatterCarriesIt() =>
        Assert.Contains(PlainWords.Rule, NpcChatter.Speaker, StringComparison.Ordinal);

    [Fact]
    public void TheNudgeCarriesIt() =>
        Assert.Contains(PlainWords.Rule, FlavourBriefs.Nudge("facts").Instruction, StringComparison.Ordinal);

    [Theory]
    [InlineData(LogVoice.ShipsAi)]
    [InlineData(LogVoice.FirstPersonWithCommentary)]
    [InlineData(LogVoice.FirstPerson)]
    public void TheCommanderLogCarriesIt(LogVoice voice) =>
        Assert.Contains(PlainWords.Rule, LogPrompt.Instructions(voice, LogLength.Standard), StringComparison.Ordinal);
}
