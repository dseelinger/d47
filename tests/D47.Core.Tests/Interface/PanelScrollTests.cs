using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Which utterances move a page (<a href="https://github.com/dseelinger/d47/issues/34">#34</a>).
/// </summary>
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
    /// <b>Whole-utterance, which is the rule that keeps this path safe.</b> "Page down" is an
    /// instruction and "what does page down do" is a question, and a scroll that fired on the
    /// second would move the page out from under somebody mid-sentence.
    /// </summary>
    [Theory]
    [InlineData("what does page down do")]
    [InlineData("can you page down for me")]
    [InlineData("page down is the phrase I keep forgetting")]
    [InlineData("scroll down to the bottom of the cargo hold")]
    public void ASentenceContainingAPhraseIsNotThePhrase(string spoken) =>
        Assert.Null(PanelScroll.Match(spoken));

    /// <summary>
    /// <b>Leaving a page and reading further up it are different things</b>, so the phrases that
    /// mean the first belong to <see cref="PanelPhrases.Back"/> and must not be taken here.
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
}
