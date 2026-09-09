using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Which utterances move a page.</summary>
public class PanelScrollTests
{
    [Theory]
    [InlineData("page down", PanelScrollStep.PageDown)]
    [InlineData("page up", PanelScrollStep.PageUp)]
    [InlineData("scroll down", PanelScrollStep.LineDown)]
    [InlineData("scroll up", PanelScrollStep.LineUp)]
    public void TheFourThatWereAskedFor(string spoken, PanelScrollStep step) =>
        Assert.Equal(step, PanelScroll.Match(spoken));

    /// <summary>Punctuation and spacing are the transcriber's, not the Commander's.</summary>
    [Theory]
    [InlineData("Page Down")]
    [InlineData("page down.")]
    [InlineData("  page   down  ")]
    public void SaidHoweverItArrives(string spoken) =>
        Assert.Equal(PanelScrollStep.PageDown, PanelScroll.Match(spoken));

    /// <summary>
    /// Whole-utterance, which is the rule that keeps this path safe. "Page down" is an instruction and
    /// "what does page down do" is a question, and a scroll that fired on the second would move the
    /// page out from under somebody mid-sentence.
    /// </summary>
    [Theory]
    [InlineData("what does page down do")]
    [InlineData("can you page down for me")]
    [InlineData("page down is the phrase I keep forgetting")]
    [InlineData("scroll down to the bottom of the cargo hold")]
    public void ASentenceContainingAPhraseIsNotThePhrase(string spoken) =>
        Assert.Null(PanelScroll.Match(spoken));

    /// <summary>
    /// Leaving a page and reading further up it are different things, so the phrases that mean the
    /// first belong to <see cref="PanelPhrases.Back"/> and must not be taken here.
    /// </summary>
    [Fact]
    public void GoingBackIsNotScrolling()
    {
        foreach (var phrase in PanelPhrases.Back)
        {
            Assert.Null(PanelScroll.Match(phrase));
        }
    }

    /// <summary>And nothing declared here is a phrase something else already answers.</summary>
    [Fact]
    public void NoPhraseCollidesWithNavigation()
    {
        var clash = PanelScroll.Phrases.Keys
            .Where(phrase => PanelPhrases.Back.Contains(phrase, StringComparer.OrdinalIgnoreCase)
                             || PanelPhrases.Help.Contains(phrase, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(clash.Count == 0, $"Scroll phrases that already mean something else: {string.Join(", ", clash)}");
    }

    [Fact]
    public void SilenceAndNonsenseAskForNothing()
    {
        Assert.Null(PanelScroll.Match(string.Empty));
        Assert.Null(PanelScroll.Match("   "));
        Assert.Null(PanelScroll.Match("where am i"));
    }

    /// <summary>Every declared phrase resolves, so a table entry is never a phrase said into silence.</summary>
    [Fact]
    public void EveryDeclaredPhraseMatchesItself()
    {
        Assert.NotEmpty(PanelScroll.Phrases);

        foreach (var (phrase, step) in PanelScroll.Phrases)
        {
            Assert.Equal(step, PanelScroll.Match(phrase));
        }
    }
    /// <summary>A matched phrase is always answered, and the answer says which of the three things
    /// happened.</summary>
    [Theory]

    // Something moved, which is the ordinary case and reads exactly as it always did.
    [InlineData(PanelScrollStep.PageDown, new[] { PanelScrollOutcome.Moved }, "Page down.")]
    [InlineData(PanelScrollStep.PageUp, new[] { PanelScrollOutcome.Moved }, "Page up.")]
    [InlineData(PanelScrollStep.LineDown, new[] { PanelScrollOutcome.Moved }, "Scrolled down.")]
    [InlineData(PanelScrollStep.LineUp, new[] { PanelScrollOutcome.Moved }, "Scrolled up.")]

    // A page that exists and is at the end the Commander asked for.
    [InlineData(PanelScrollStep.PageDown, new[] { PanelScrollOutcome.AlreadyThere }, "Already at the bottom.")]
    [InlineData(PanelScrollStep.LineDown, new[] { PanelScrollOutcome.AlreadyThere }, "Already at the bottom.")]
    [InlineData(PanelScrollStep.PageUp, new[] { PanelScrollOutcome.AlreadyThere }, "Already at the top.")]
    [InlineData(PanelScrollStep.LineUp, new[] { PanelScrollOutcome.AlreadyThere }, "Already at the top.")]

    // No page at all, which looks identical to "already there" from outside.
    [InlineData(PanelScrollStep.PageDown, new[] { PanelScrollOutcome.NothingToScroll }, "There is nothing to scroll here.")]

    // Three surfaces, one phrase, said once into the room.
    [InlineData(
        PanelScrollStep.PageDown,
        new[] { PanelScrollOutcome.NothingToScroll, PanelScrollOutcome.AlreadyThere, PanelScrollOutcome.Moved },
        "Page down.")]

    // And being at the end of a real page beats there being no page: the surface showing something is the one
    // that answers for the room.
    [InlineData(
        PanelScrollStep.PageUp,
        new[] { PanelScrollOutcome.NothingToScroll, PanelScrollOutcome.AlreadyThere },
        "Already at the top.")]

    // Nobody registered at all, which is every surface saying nothing rather than a phrase that was not a
    // scroll — it still gets an answer rather than a model turn.
    [InlineData(PanelScrollStep.PageDown, new PanelScrollOutcome[0], "There is nothing to scroll here.")]
    public void AMatchedPhraseIsAlwaysAnswered(
        PanelScrollStep step, PanelScrollOutcome[] outcomes, string expected) =>
        Assert.Equal(expected, PanelScroll.Answer(step, outcomes));
}
