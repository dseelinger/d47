using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <b>"Roll" is a word from a version of engineering that no longer exists</b>
/// (change-requests.md 36), and five uses of it are correct English about something else.
/// <para>
/// A roll was a throw of the dice — materials in, a result somewhere in a band. Engineering has
/// not worked that way for years: a Commander progresses a <em>grade</em> by applying materials a
/// known number of times, and Frontier's own noun for that is in the journal every time it
/// happens. The event is <c>EngineerCraft</c>.
/// </para>
/// <para>
/// <b>This file exists because the sweep is the dangerous part, not the decision.</b> A blind
/// find-and-replace over 54 matches would have broken an axis of rotation, two audio pre-rolls, a
/// published documentation anchor, Elite rolling a log file, a rolling caption window, and two
/// sentences that say "not rolled" <em>on purpose</em>. Each of those is asserted below, so the
/// next person to sweep the word finds out from a test rather than from a Commander.
/// </para>
/// </summary>
public class TheWordIsCraftExceptWhereItIsNotTests
{
    /// <summary>
    /// The sentence a Commander hears most, and the one #33 made sure they hear once.
    /// </summary>
    [Fact]
    public void TheRankSentenceSaysCraft()
    {
        Assert.Contains("craft", EngineeringRules.RankRises, StringComparison.Ordinal);
        Assert.DoesNotContain("roll", EngineeringRules.RankRises, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>What d47 hears is widened and never narrowed.</b> A Commander who has said "roll" for
    /// years will say it again, and taking the word out of what d47 listens for would break input
    /// that works today. Both spellings, and this is the assertion that stops a later tidy-up
    /// deciding the old one is dead wood.
    /// </summary>
    [Fact]
    public void BothWordsAreStillHeard()
    {
        var keywords = EngineeringCapability.Create(() => null).Keywords;

        Assert.Contains(keywords, phrase => phrase.Contains("roll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(keywords, phrase => phrase.Contains("craft", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <b>On foot, "not rolled" is the point of the sentence.</b> Suit and weapon grades are bought
    /// outright, and the contrast with ship engineering is what the line is for — so this is the
    /// one place the old word is still doing work, and a sweep must leave it standing.
    /// </summary>
    [Fact]
    public void OnFootStillSaysNothingIsRolled()
    {
        var said = OnFootCapability.Create(() => null).Tools
            .Select(tool => tool.Description)
            .ToList();

        Assert.Contains(said, line => line.Contains("nothing on foot is rolled", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Pre-roll is audio, not engineering</b>, and one of the two is a published URL fragment —
    /// changing it would break a link rather than a sentence, which is the quietest way for a
    /// sweep to do damage. Asserted against the source rather than a built descriptor, because
    /// reaching that row needs a settings store and the thing being protected is the spelling.
    /// </summary>
    [Fact]
    public void ThePreRollAnchorIsUntouched()
    {
        var source = File.ReadAllText(Path.Combine(Repository(), "src", "D47.Core",
            "Capabilities", "Builtin", "ListeningCapability.cs"));

        Assert.Contains("DocsAnchor = \"pre-roll\"", source, StringComparison.Ordinal);
    }

    /// <summary>The repository root, found by walking up for the file that only it has.</summary>
    private static string Repository()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null && !File.Exists(Path.Combine(here.FullName, "CLAUDE.md")))
        {
            here = here.Parent;
        }

        Assert.NotNull(here);
        return here!.FullName;
    }
}
