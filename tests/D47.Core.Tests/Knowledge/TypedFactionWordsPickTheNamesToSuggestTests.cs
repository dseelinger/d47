using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TypedFactionWordsPickTheNamesToSuggestTests
{
    private static readonly string[] Names =
    [
        "Mac Moti Gold Energy Partners",
        "Motilekui Empire Party",
        "Mother Gaia",
        "Independent Fleet of Mothership",
    ];

    [Theory]
    [InlineData("Mother G", "Mother")]
    [InlineData("  Mot ", "Mot")]
    [InlineData("Fleet of Mothership", "Mothership")]
    [InlineData("   ", "")]
    public void TheLongestWordIsTheOneSent(string typed, string sent) =>
        Assert.Equal(sent, FactionNameSuggestions.WordToSend(typed));

    [Fact]
    public void EveryTypedWordMustStartOneOfTheNamesWords() =>
        Assert.Equal(["Mother Gaia"], FactionNameSuggestions.Filter("Mother G", Names));

    [Fact]
    public void NamesStartingWithTheTextComeFirstThenTheRestAlphabetically() =>
        Assert.Equal(
            ["Mother Gaia", "Motilekui Empire Party", "Independent Fleet of Mothership", "Mac Moti Gold Energy Partners"],
            FactionNameSuggestions.Filter("Mot", Names));

    [Fact]
    public void AWordThatStartsNoNameWordDropsTheName() =>
        Assert.Empty(FactionNameSuggestions.Filter("Mother Z", Names));

    [Fact]
    public void AtMostEightAreKept() =>
        Assert.Equal(8, FactionNameSuggestions.Filter("a", Enumerable.Range(0, 20).Select(i => $"Alpha {i:00}")).Count);
}
