using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests;

/// <summary>Reaching the game window by saying so.</summary>
public class FocusPhrasesTests
{
    /// <summary>The four spellings the request named, plus the one its slash implied.</summary>
    [Theory]
    [InlineData("set elite to front")]
    [InlineData("set game to front")]
    [InlineData("put elite in front")]
    [InlineData("put game in front")]
    [InlineData("put elite in focus")]
    public void TheAskedForSpellingsAreThere(string phrase) =>
        Assert.Contains(phrase, FocusCapability.Phrases);

    /// <summary>Every phrase is at least three words.</summary>
    [Fact]
    public void NoPhraseIsShortEnoughToSwallowASentence()
    {
        var short_ = FocusCapability.Phrases
            .Where(phrase => phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            .ToArray();

        Assert.True(short_.Length == 0, $"Too short to be a keyword: {string.Join(", ", short_)}");
    }

    /// <summary>
    /// The generated family names both the thing and the place, so none of them can match a sentence
    /// that is not about the game window.
    /// </summary>
    [Fact]
    public void TheGeneratedFamilyNamesTheGameAndWhereToPutIt()
    {
        var generated = FocusCapability.Phrases
            .Where(phrase => phrase.Contains(" front", StringComparison.Ordinal)
                || phrase.Contains(" focus", StringComparison.Ordinal))
            .ToArray();

        Assert.True(generated.Length >= 20, $"Only {generated.Length} placement phrases.");

        Assert.All(generated, phrase => Assert.True(
            phrase.Contains("elite", StringComparison.Ordinal)
            || phrase.Contains("game", StringComparison.Ordinal),
            $"'{phrase}' names no target"));
    }

    /// <summary>The phrases are the router's whole vocabulary here, so the two cannot drift.</summary>
    [Fact]
    public void TheRouterIsGivenExactlyThosePhrases() =>
        Assert.Equal(FocusCapability.Phrases, FocusCapability.Create(null).Keywords.Select(keyword => keyword.Phrase));

    /// <summary>And the model is told not to prescribe a workaround.</summary>
    [Fact]
    public void TheGuardrailsForbidSuggestingAWayRoundIt()
    {
        Assert.Contains("do not tell", Guardrails.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("them how to do it themselves", Guardrails.Text, StringComparison.Ordinal);

        // Named, because that is the sentence a Commander actually got.
        Assert.Contains("spoken phrase for the thing", Guardrails.Text, StringComparison.Ordinal);
    }
}
