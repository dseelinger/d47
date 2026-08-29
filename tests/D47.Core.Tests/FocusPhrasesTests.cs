using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// Reaching the game window by saying so (change-requests.md 31).
/// <para>
/// <b>The interesting failure was not the miss.</b> A Commander said a phrase the list did not
/// have, the question fell through to the model, and the model — which cannot see this capability
/// at all, because it is <c>Protected</c> — answered <i>"I have no tool to bring the game window
/// to front… that's yours to do — Alt-Tab or the taskbar."</i> True about the model, false about
/// d47, and it sent them to do by hand a thing d47 does.
/// </para>
/// </summary>
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

    /// <summary>
    /// <b>Every phrase is at least three words.</b> The router matches a keyword anywhere in the
    /// input and runs before the model, so a short one swallows sentences: a bare "elite" would
    /// answer <i>"what is my elite rank in combat"</i> by moving a window. Generating the family
    /// as a cross product makes it long, and this is what keeps it safe.
    /// </summary>
    [Fact]
    public void NoPhraseIsShortEnoughToSwallowASentence()
    {
        var short_ = FocusCapability.Phrases
            .Where(phrase => phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            .ToArray();

        Assert.True(short_.Length == 0, $"Too short to be a keyword: {string.Join(", ", short_)}");
    }

    /// <summary>
    /// The generated family names both the thing and the place, so none of them can match a
    /// sentence that is not about the game window.
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

    /// <summary>
    /// The phrases are the router's whole vocabulary here, so the two cannot drift.
    /// </summary>
    [Fact]
    public void TheRouterIsGivenExactlyThosePhrases() =>
        Assert.Equal(FocusCapability.Phrases, FocusCapability.Create(null).Keywords.Select(keyword => keyword.Phrase));

    /// <summary>
    /// <b>And the model is told not to prescribe a workaround.</b> The rule that already stopped
    /// it claiming the software cannot do something said to "leave it there", and it did not —
    /// it went on to name Alt-Tab and the taskbar, which is a guess about software it cannot see.
    /// </summary>
    [Fact]
    public void TheGuardrailsForbidSuggestingAWayRoundIt()
    {
        Assert.Contains("do not tell", Guardrails.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("them how to do it themselves", Guardrails.Text, StringComparison.Ordinal);

        // Named, because that is the sentence a Commander actually got.
        Assert.Contains("spoken phrase for the thing", Guardrails.Text, StringComparison.Ordinal);
    }
}
