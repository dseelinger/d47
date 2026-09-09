using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>"It's why we have pronouns".</summary>
public class SpokenReferentTests
{
    private const string Name = "Scorpii Sector BB-O a6-2";

    private static readonly DateTimeOffset Start = new(2026, 8, 23, 20, 0, 0, TimeSpan.Zero);

    private static string Say(SpokenReferent referent, string text, TimeSpan after = default) =>
        referent.Speak(text, [Name], Start + after);

    /// <summary>The first line says the name.</summary>
    [Fact]
    public void TheFirstMentionSurvives()
    {
        var referent = new SpokenReferent();

        Assert.Equal($"Arrived in {Name}.", Say(referent, $"Arrived in {Name}."));
    }

    /// <summary>And the second line does not.</summary>
    [Fact]
    public void TheNextLineSaysItInstead()
    {
        var referent = new SpokenReferent();

        Say(referent, $"Arrived in {Name}.");

        Assert.Equal(
            "There is a lore entry for it.",
            Say(referent, $"There is a lore entry for {Name}.", TimeSpan.FromSeconds(20)));
    }

    /// <summary>
    /// Within one line the first mention still survives and the rest go, so a line never opens with a
    /// dangling pronoun.
    /// </summary>
    [Fact]
    public void WithinOneLineOnlyTheRepeatsGo()
    {
        var referent = new SpokenReferent();

        Assert.Equal(
            $"{Name} has a station, and it has high grade emissions.",
            Say(referent, $"{Name} has a station, and {Name} has high grade emissions."));
    }

    [Fact]
    public void FourLinesRunningSayTheNameOnce()
    {
        var referent = new SpokenReferent();

        var said = new[]
        {
            Say(referent, $"Arrived in {Name}."),
            Say(referent, $"{Name} is unexplored.", TimeSpan.FromSeconds(5)),
            Say(referent, $"No stations in {Name}.", TimeSpan.FromSeconds(9)),
            Say(referent, $"{Name} holds high grade emissions.", TimeSpan.FromSeconds(14)),
        };

        Assert.Equal(1, said.Count(line => line.Contains(Name, StringComparison.Ordinal)));
        Assert.Equal("It is unexplored.", said[1]);
        Assert.Equal("No stations in it.", said[2]);
        Assert.Equal("It holds high grade emissions.", said[3]);
    }

    /// <summary>A second system clears the referent rather than being pronouned.</summary>
    [Fact]
    public void TwoSystemsInOneLineLeaveBothNamed()
    {
        var referent = new SpokenReferent();

        Say(referent, $"Arrived in {Name}.");

        var both = referent.Speak(
            $"Sol is 40 light years from {Name}.", [Name, "Sol"], Start + TimeSpan.FromSeconds(10));

        Assert.Equal($"Sol is 40 light years from {Name}.", both);

        // And the referent is gone, so the line after it introduces the name again rather than reaching back
        // over the ambiguous one.
        Assert.Equal(
            $"{Name} is unexplored.",
            Say(referent, $"{Name} is unexplored.", TimeSpan.FromSeconds(20)));
    }

    /// <summary>It expires.</summary>
    [Fact]
    public void ItStopsMeaningAnythingAfterAWhile()
    {
        var referent = new SpokenReferent { Holds = TimeSpan.FromMinutes(3) };

        Say(referent, $"Arrived in {Name}.");

        Assert.Equal(
            $"{Name} is unexplored.",
            Say(referent, $"{Name} is unexplored.", TimeSpan.FromMinutes(4)));
    }

    /// <summary>
    /// The pronoun starts a sentence like a word that starts sentences. "it is unexplored" at the head
    /// of a line is the difference between sounding written and sounding substituted.
    /// </summary>
    [Fact]
    public void ItIsCapitalisedWhereASentenceStarts()
    {
        var referent = new SpokenReferent();

        Say(referent, $"Arrived in {Name}.");

        Assert.Equal(
            "It is unexplored. It has no stations.",
            Say(referent, $"{Name} is unexplored. {Name} has no stations.", TimeSpan.FromSeconds(5)));
    }

    /// <summary>A line about no system at all is untouched, and forgets nothing it should keep.</summary>
    [Fact]
    public void ALineAboutNothingIsLeftAlone()
    {
        var referent = new SpokenReferent();

        Assert.Equal("Fuel is low.", referent.Speak("Fuel is low.", [], Start));
    }

    /// <summary>A jump makes "it" mean something else, so the referent is dropped on request.</summary>
    [Fact]
    public void ForgettingMakesTheNextLineSayTheNameAgain()
    {
        var referent = new SpokenReferent();

        Say(referent, $"Arrived in {Name}.");
        referent.Forget();

        Assert.Equal(
            $"{Name} is unexplored.",
            Say(referent, $"{Name} is unexplored.", TimeSpan.FromSeconds(5)));
    }
}
