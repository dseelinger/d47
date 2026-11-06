using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>The lines prompts keep the premise off the objective list and the lines plain.</summary>
public sealed class AChapterPremiseIsNotItsChecklistTests
{
    private static string Prompt(string name)
    {
        using var stream = typeof(AChapterPremiseIsNotItsChecklistTests).Assembly.GetManifestResourceStream($"Prompts.{name}.txt")
            ?? throw new InvalidOperationException($"Prompts.{name}.txt is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void TheLinesPromptKeepsThePremiseAndWantOffTheObjectives() =>
        Assert.Contains("Neither the premise nor the want lists the objectives.", Prompt("lines-new-chapter"), StringComparison.Ordinal);

    [Theory]
    [InlineData("lines-short-story")]
    [InlineData("lines-new-chapter")]
    [InlineData("lines-next-chapter-after-failed")]
    [InlineData("lines-revise-remark")]
    [InlineData("lines-rewrite-refused")]
    [InlineData("line-repairs")]
    public void EveryChapterPromptCarriesThePlainWordsRule(string prompt) =>
        Assert.Contains(PlainWords.Rule, Prompt(prompt), StringComparison.Ordinal);

    [Theory]
    [InlineData("lines-short-story")]
    [InlineData("lines-new-chapter")]
    public void ThePlacesAreCalledByNameAndSetUpsMakeSenseAlone(string prompt)
    {
        var text = Prompt(prompt);
        Assert.Contains("A place is called by its name, never by a figurative name", text, StringComparison.Ordinal);
        Assert.Contains("makes sense when heard alone", text, StringComparison.Ordinal);
    }
}
