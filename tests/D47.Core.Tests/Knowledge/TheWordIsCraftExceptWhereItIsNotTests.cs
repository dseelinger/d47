using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary> "Roll" is a word from a version of engineering that no longer exists, and five uses of it are
/// correct English about something else. </summary>
[Trait("Category", "Integration")]
public class TheWordIsCraftExceptWhereItIsNotTests
{
    /// <summary>The sentence a Commander hears most, and the one #33 made sure they hear once.</summary>
    [Fact]
    public void TheRankSentenceSaysCraft()
    {
        Assert.Contains("craft", EngineeringRules.RankRises, StringComparison.Ordinal);
        Assert.DoesNotContain("roll", EngineeringRules.RankRises, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What d47 hears is widened and never narrowed.</summary>
    [Fact]
    public void BothWordsAreStillHeard()
    {
        var keywords = EngineeringCapability.Create(() => null).Keywords;

        Assert.Contains(keywords, keyword => keyword.Phrase.Contains("roll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(keywords, keyword => keyword.Phrase.Contains("craft", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>On foot, "not rolled" is the point of the sentence.</summary>
    [Fact]
    public void OnFootStillSaysNothingIsRolled()
    {
        var said = OnFootCapability.Create(() => null).Tools
            .Select(tool => tool.Description)
            .ToList();

        Assert.Contains(said, line => line.Contains("nothing on foot is rolled", StringComparison.Ordinal));
    }

    /// <summary>
    /// Pre-roll is audio, not engineering, and one of the two is a published URL fragment — changing it
    /// would break a link rather than a sentence, which is the quietest way for a sweep to do damage.
    /// </summary>
    [Trait("Category", "Gate")]
    [Fact]
    public void ThePreRollAnchorIsUntouched()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryFolder.Root(), "src", "D47.Core",
            "Capabilities", "Builtin", "ListeningCapability.cs"));

        Assert.Contains("DocsAnchor = \"pre-roll\"", source, StringComparison.Ordinal);
    }
}
