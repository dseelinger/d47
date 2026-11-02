using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>The spine and line prompts keep the premise off the objective list and the lines plain.</summary>
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
    public void TheSpineKeepsThePremiseAndWantOffTheObjectives() =>
        Assert.Contains("Neither lists the objectives or turns them into a checklist, ledger, column or tally", Prompt("spine"), StringComparison.Ordinal);

    [Theory]
    [InlineData("beats-short-story")]
    [InlineData("beats-new-chapter")]
    public void ThePlacesAreCalledByNameAndSetUpsMakeSenseAlone(string prompt)
    {
        var text = Prompt(prompt);
        Assert.Contains("A place is called by its name, never by a figurative name", text, StringComparison.Ordinal);
        Assert.Contains("makes sense when heard alone", text, StringComparison.Ordinal);
    }
}
