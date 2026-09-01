using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Markdown out of what is heard (2026-08-31: a voice read "asterisk asterisk" through a whole
/// docking report). The negative half matters as much: prose that merely contains the
/// characters is not markdown and must come through untouched.
/// </summary>
public class PlainSpeechTests
{
    [Fact]
    public void TheReportedSentenceLosesItsMarkupAndNothingElse()
    {
        Assert.Equal(
            "We're docked at Phoenix Base in Meene. Your Reaper (Cobra MkV) has 15.13 / 16 t fuel.",
            PlainSpeech.Strip(
                "We're docked at **Phoenix Base in Meene**. Your **Reaper (Cobra MkV)** has **15.13 / 16 t** fuel."));
    }

    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("*emphasis*", "emphasis")]
    [InlineData("***both***", "both")]
    [InlineData("__bold__", "bold")]
    [InlineData("_emphasis_", "emphasis")]
    [InlineData("`code`", "code")]
    [InlineData("[the docs](https://example.invalid/page)", "the docs")]
    [InlineData("### Heading first", "Heading first")]
    [InlineData("* a bullet line", "a bullet line")]
    public void EachMarkdownShapeIsUnwrapped(string written, string spoken)
    {
        Assert.Equal(spoken, PlainSpeech.Strip(written));
    }

    /// <summary>
    /// An emphasis split across the sentence splitter leaves one unmatched pair of asterisks in
    /// each half; a voice reading "asterisk asterisk" at the seam is the reported defect.
    /// </summary>
    [Fact]
    public void AStrandedPairIsRemovedRatherThanRead()
    {
        Assert.Equal("Jump capability shows 49.93 ly.", PlainSpeech.Strip("Jump capability shows **49.93 ly."));
        Assert.Equal("max.", PlainSpeech.Strip("max**."));
    }

    [Theory]
    [InlineData("coverage_recorder_on stays itself")]
    [InlineData("5 * 3 is 15")]
    [InlineData("a plain dash - stays a dash")]
    [InlineData("Meene AB 5 d holds steady")]
    public void ProseThatMerelyContainsTheCharactersSurvives(string sentence)
    {
        Assert.Equal(sentence, PlainSpeech.Strip(sentence));
    }
}
