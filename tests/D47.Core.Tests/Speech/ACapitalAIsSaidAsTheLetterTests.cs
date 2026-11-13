using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>A capital A inside a sentence is a rating, not the article (#2412).</summary>
public class ACapitalAIsSaidAsTheLetterTests
{
    [Theory]
    [InlineData("Super Capacitor is not on A Shield Booster.", "Super Capacitor is not on ay Shield Booster.")]
    [InlineData("Fit five A Thrusters.", "Fit five ay Thrusters.")]
    [InlineData("Grade: A.", "Grade: ay.")]
    [InlineData("an A-rated Power Plant", "an ay-rated Power Plant")]
    [InlineData("It is fitted. A Shield Booster is next.", "It is fitted. A Shield Booster is next.")]
    [InlineData("A Shield Booster is fitted.", "A Shield Booster is fitted.")]
    [InlineData("Take a Shield Booster.", "Take a Shield Booster.")]
    [InlineData("Fitted: AAA and Alpha.", "Fitted: AAA and Alpha.")]
    [InlineData("Is it fitted? \"A Shield Booster.\"", "Is it fitted? \"A Shield Booster.\"")]
    public void OnlyAStandaloneNonInitialCapitalIsRespelled(string line, string expected) =>
        Assert.Equal(expected, SpokenRatingA.Rewrite(line));

    [Fact]
    public void ARatingAfterDesignationsIsRespelled() =>
        Assert.Equal(
            "Fit five ay Thrusters.",
            SpokenRatingA.Rewrite(SpokenDesignations.Rewrite("Fit 5A Thrusters.")));

    [Fact]
    public void KokoroSaysTheRespellingAsTheLetterWithStress() =>
        Assert.Equal("ˈeɪ", new Phonemiser().ToPhonemes(SpokenRatingA.Form));

    [Fact]
    public void KokoroSaysARatingInASentenceWithStress()
    {
        var said = new Phonemiser().ToPhonemes(SpokenRatingA.Rewrite("not on A Shield Booster"));
        Assert.Contains(" ˈeɪ ", " " + said + " ");
    }
}
