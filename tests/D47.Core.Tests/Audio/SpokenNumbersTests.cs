using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Numerals written out for a voice (remediation.md, "ElevenLabs switches Warden to German
/// mid-callout").
/// <para>
/// The half that matters most is what it leaves alone. A system name, a model id and a sample
/// rate all contain digits and none of them is a quantity, and a normaliser that reworded them
/// on the way to a synthesiser would be a worse bug than the one it fixes.
/// </para>
/// </summary>
public class SpokenNumbersTests
{
    [Theory]
    [InlineData("0", "zero")]
    [InlineData("7", "seven")]
    [InlineData("13", "thirteen")]
    [InlineData("20", "twenty")]
    [InlineData("88", "eighty-eight")]
    [InlineData("100", "one hundred")]
    [InlineData("101", "one hundred and one")]
    [InlineData("999", "nine hundred and ninety-nine")]
    [InlineData("1000", "one thousand")]
    [InlineData("1,000", "one thousand")]
    [InlineData("2500", "two thousand five hundred")]
    [InlineData("1000000", "one million")]
    [InlineData("12,345,678", "twelve million three hundred and forty-five thousand six hundred and seventy-eight")]
    public void AWholeNumberIsWrittenOut(string numeral, string said) =>
        Assert.Equal(said, SpokenNumbers.Expand(numeral));

    /// <summary>
    /// Digit by digit after the point, which is how a decimal is read aloud. "twelve point
    /// seventy-five" is a different number said badly.
    /// </summary>
    [Theory]
    [InlineData("12.5", "twelve point five")]
    [InlineData("0.25", "zero point two five")]
    [InlineData("22.75", "twenty-two point seven five")]
    public void ADecimalIsReadDigitByDigitAfterThePoint(string numeral, string said) =>
        Assert.Equal(said, SpokenNumbers.Expand(numeral));

    /// <summary>The line that was reported, end to end.</summary>
    [Fact]
    public void TheReportedCalloutComesOutEntirelyInWords()
    {
        Assert.Equal(
            "Adaptive Encryptors Capture at seventy-five percent. eighty-eight of one hundred.",
            SpokenNumbers.Expand("Adaptive Encryptors Capture at 75 percent. 88 of 100."));
    }

    /// <summary>
    /// A full stop that ends a sentence is not a decimal point, and the difference is whether a
    /// digit follows it. "100. Next" would otherwise become one number a hundred times too big.
    /// </summary>
    [Fact]
    public void AFullStopBetweenSentencesIsNotADecimalPoint() =>
        Assert.Equal("one hundred. Next", SpokenNumbers.Expand("100. Next"));

    /// <summary>
    /// Digits touching a letter are part of an identifier. A procedurally generated system name
    /// is the commonest thing d47 says out loud that contains any.
    /// </summary>
    [Theory]
    [InlineData("Hyades Sector DR-V c2-23")]
    [InlineData("eleven_flash_v2_5")]
    [InlineData("24kHz")]
    [InlineData("Col 285 Sector YZ-O b6-2")]
    public void SomethingWithLettersInItIsLeftAlone(string text) =>
        Assert.Contains(text.Split(' ')[^1], SpokenNumbers.Expand(text), StringComparison.Ordinal);

    [Fact]
    public void TextWithNoDigitsIsUntouched()
    {
        const string Line = "Shields are down, Commander.";
        Assert.Same(Line, SpokenNumbers.Expand(Line));
    }

    /// <summary>
    /// A figure past the ceiling is left as digits rather than read as a sentence of its own.
    /// The synthesiser's own reading of it is better than "nine hundred quadrillion".
    /// </summary>
    [Fact]
    public void AnAbsurdlyLargeNumberIsLeftAsDigits() =>
        Assert.Equal("9999999999999999", SpokenNumbers.Expand("9999999999999999"));
}
