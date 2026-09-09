using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>Words that reached the wrong rung because of what was written around them.</summary>
public class TheMarksAroundAWordAreNotSpelledTests
{
    /// <summary>The entries this needs, copied from the shipped dictionary exactly.</summary>
    private sealed class Shipped : IPronunciationDictionary
    {
        private readonly Dictionary<string, string> _words = new(StringComparer.OrdinalIgnoreCase)
        {
            ["observe"] = "əbzˈɜːv",
            ["guardian"] = "ɡˈɑːɹdiən",
            ["booster"] = "bˈuːstɚ",
            ["modules"] = "mˈɑːdʒuːlz",
            ["shield"] = "ʃˈiːld",
            ["ensure"] = "ɛnʃˈʊɹ",
            ["to"] = "tuː",
            ["protocol"] = "pɹˈoʊɾəkˌɑːl",
            ["during"] = "dˈʊɹɹɪŋ",
            ["your"] = "jʊɹ",
            ["visit"] = "vˈɪzɪt",
            ["pilot"] = "pˈaɪlət",
            ["engineered"] = "ˌɛndʒɪnˈɪɹd",
        };

        public string? Lookup(string word) => _words.GetValueOrDefault(word);
    }

    private static readonly Phonemiser Rules = new(new Shipped());

    /// <summary>The letters a spelled-out word is read as, which is what must not appear.</summary>
    private const string SpelledGuardian = "dʒiː, juː, eɪ, ɑːɹ, diː, aɪ, eɪ, ˈɛn";

    // ---- The reported sentence -----------------------------------------------------------

    /// <summary>The check the issue asked for, and it passes at HEAD as it passed at 0.84.4.</summary>
    [Fact]
    public void TheReportedSentenceSaysObserveTheDictionarysWay() =>
        Assert.Contains(
            "əbzˈɜːv",
            Rules.ToPhonemes("Ensure to observe starport protocol during your visit, pilot."),
            StringComparison.Ordinal);

    /// <summary>And the line d47 actually said, which is where the spelled family came from.</summary>
    [Fact]
    public void TheEmphasisedWordsAreSaidRatherThanSpelled()
    {
        var said = Rules.ToPhonemes(
            "At a **human tech broker** that carries **Guardian modules**.");

        Assert.Contains("ɡˈɑːɹdiən", said, StringComparison.Ordinal);
        Assert.Contains("mˈɑːdʒuːlz", said, StringComparison.Ordinal);
        Assert.DoesNotContain(SpelledGuardian, said, StringComparison.Ordinal);
        Assert.DoesNotContain("*", said, StringComparison.Ordinal);
    }

    // ---- Every mark a model writes -------------------------------------------------------

    /// <summary>One word wearing each mark in turn.</summary>
    [Theory]
    [InlineData("**Guardian**")]
    [InlineData("**Guardian**.")]
    [InlineData("*Guardian*")]
    [InlineData("_Guardian_")]
    [InlineData("`Guardian`")]
    [InlineData("~~Guardian~~")]
    [InlineData("“Guardian”")]
    [InlineData("“Guardian”,")]
    [InlineData("‘Guardian’")]
    [InlineData("Guardian…")]
    [InlineData("(Guardian)")]
    public void AWordIsSaidWhateverIsWrittenAroundIt(string token)
    {
        var said = Rules.ToPhonemes(token);

        Assert.Contains("ɡˈɑːɹdiən", said, StringComparison.Ordinal);
        Assert.DoesNotContain(SpelledGuardian, said, StringComparison.Ordinal);
    }

    /// <summary>Phrasing is kept and decoration is not.</summary>
    [Fact]
    public void ThePhrasingSurvivesAndTheEmphasisDoesNot()
    {
        var said = Rules.ToPhonemes("“Guardian” Booster… **engineered**");

        Assert.Contains("”", said, StringComparison.Ordinal);
        Assert.Contains("…", said, StringComparison.Ordinal);
        Assert.Contains("ˌɛndʒɪnˈɪɹd", said, StringComparison.Ordinal);
        Assert.DoesNotContain("*", said, StringComparison.Ordinal);
        Assert.DoesNotContain("“", said, StringComparison.Ordinal);
    }

    /// <summary>An em dash joins a compound the way a hyphen does.</summary>
    [Theory]
    [InlineData("Booster—engineered")]
    [InlineData("Booster–engineered")]
    [InlineData("Booster-engineered")]
    public void EveryDashIsACompoundsJoint(string token)
    {
        var said = Rules.ToPhonemes(token);

        Assert.Contains("bˈuːstɚ", said, StringComparison.Ordinal);
        Assert.Contains("ˌɛndʒɪnˈɪɹd", said, StringComparison.Ordinal);
        Assert.DoesNotContain("dˈæʃ", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the dash a Commander's own example depends on is still voiced, because both sides of it are
    /// being spelled.
    /// </summary>
    [Fact]
    public void ADashBetweenSpelledSegmentsIsStillSaid() =>
        Assert.Contains(
            "dˈæʃ", Rules.ToPhonemes("COL 385 SECTOR B0-GQPI"), StringComparison.Ordinal);

    // ---- The rung, said out loud ---------------------------------------------------------

    /// <summary>The line that would have made this issue a read instead of an investigation.</summary>
    [Fact]
    public void TheLadderSaysWhichRungAnsweredForEachSegment()
    {
        var fell = new List<(string Segment, PhonemeRung Rung)>();

        var watched = new Phonemiser(
            new Shipped(), overrides: null, note: (segment, rung, _) => fell.Add((segment, rung)));

        watched.ToPhonemes("Guardian Kamitra GQPI");

        Assert.Contains(("Guardian", PhonemeRung.Dictionary), fell);
        Assert.Contains(("Kamitra", PhonemeRung.Rules), fell);
        Assert.Contains(("GQPI", PhonemeRung.Spelled), fell);
    }

    /// <summary>
    /// And it says it for the words that were reported, which is the read that was not available: the
    /// emphasised one now comes off the dictionary rather than off the last rung.
    /// </summary>
    [Fact]
    public void TheRungIsNamedForTheReportedWord()
    {
        var fell = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(new Shipped(), overrides: null, note: (segment, rung, _) => fell.Add((segment, rung)))
            .ToPhonemes("**Guardian**");

        Assert.Contains(("Guardian", PhonemeRung.Dictionary), fell);
    }
}
