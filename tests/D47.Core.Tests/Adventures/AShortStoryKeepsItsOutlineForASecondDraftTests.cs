using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A short story's second draft reuses the first draft's name and spine when it was read; its third draft writes a new one.</summary>
public sealed class AShortStoryKeepsItsOutlineForASecondDraftTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Silence = "No JSON here.";

    [Fact]
    public async Task ASecondDraftMakesNoSpineCallAndKeepsTheName()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Silence, AdventureGeneratorTests.GoodBeats);

        var outcome = await Draft(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(4, provider.CallCount);
        Assert.Equal("The Unrecoverable Column", outcome.Draft!.Name);
    }

    [Fact]
    public async Task AThirdDraftMakesASpineCall()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Silence, Silence, AdventureGeneratorTests.Spine, AdventureGeneratorTests.GoodBeats);

        var outcome = await Draft(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(6, provider.CallCount);
    }

    [Fact]
    public async Task ASecondDraftMakesASpineCallWhenTheFirstSpineWasUnreadable()
    {
        var provider = Scripted("""{"name": 3}""", AdventureGeneratorTests.Spine, AdventureGeneratorTests.GoodBeats);

        var outcome = await Draft(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(4, provider.CallCount);
    }

    private static RoundScriptedLlmProvider Scripted(params string[] rounds) =>
        new([.. rounds.Select(round => RoundScriptedLlmProvider.Saying(round))]);

    private static Task<AdventureOutcome> Draft(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .DraftAsync(new AdventureAsk(Length: AdventureLength.Short), Now, null, CancellationToken.None);
}
