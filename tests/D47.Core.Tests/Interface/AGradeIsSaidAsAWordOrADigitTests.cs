using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A value on a button is named by saying it, with or without the prompt's own word (#434).</summary>
public class AGradeIsSaidAsAWordOrADigitTests
{
    private static readonly IReadOnlyList<EntryButton> Kit = EntryButton.Range(2, 5);

    private static readonly IReadOnlyList<EntryButton> Ship =
        [.. EntryButton.Range(1, 5), new EntryButton("Any", string.Empty)];

    [Theory]
    [InlineData("four")]
    [InlineData("grade four")]
    [InlineData("4")]
    [InlineData("Grade 4.")]
    [InlineData("Four!")]
    public void FourGradeFourAndTheDigitAllNameFour(string said) =>
        Assert.Equal("4", EntryButton.Named(said, "Grade", Kit)?.Value);

    [Theory]
    [InlineData("one")]
    [InlineData("seven")]
    [InlineData("grade")]
    [InlineData("")]
    public void AValueOffTheRowNamesNothing(string said) =>
        Assert.Null(EntryButton.Named(said, "Grade", Kit));

    [Fact]
    public void AnyIsNamedByItsLabelAndCommitsEmpty()
    {
        Assert.Equal(string.Empty, EntryButton.Named("any", "Grade", Ship)?.Value);
        Assert.Equal(string.Empty, EntryButton.Named("Any grade.", "Grade", Ship)?.Value);
    }

    [Fact]
    public void ARangeIsOneButtonPerNumber() =>
        Assert.Equal(["2", "3", "4", "5"], Kit.Select(button => button.Label));

    [Fact]
    public void ReducingLeavesTheValueForTheCallerToJudge() =>
        Assert.Equal("7", EntryButton.Reduce("Grade seven.", "Grade"));
}
