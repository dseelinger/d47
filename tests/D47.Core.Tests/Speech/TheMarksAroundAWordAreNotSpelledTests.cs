using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Words that reached the wrong rung because of what was written around them (#153).
/// <para>
/// <b>Three words came out wrong on one day and they failed differently, which is the diagnosis.</b>
/// <em>observe</em> was reported as <em>observ-eh</em>; <em>Guardian</em> and <em>Booster</em> were
/// reported as being spelled out. A segment is spelled only when it fails
/// <c>All(char.IsLetter)</c>, and none of those three has a character in it that is not a letter —
/// so the decoration around them was the suspect, and it is confirmed here.
/// </para>
/// <para>
/// <b>What d47 actually said, from the log of 2026-08-28:</b> <em>"At a **human tech broker** that
/// carries **Guardian modules**"</em>. Markdown emphasis, in the model's own prose, on exactly the
/// two words that were reported. It is stripped nowhere between the model and the tokenizer, so
/// <c>**Guardian</c> failed the letters test, skipped the dictionary <em>and</em> the rules, and
/// was read out <em>gee, you, ay, ar, dee, eye, ay, en</em>.
/// </para>
/// <para>
/// <b>And <em>observe</em> never missed anything.</b> It is in the shipped dictionary, it was
/// looked up, and it was said <c>əbzˈɜːv</c> — asserted below, which is the check the issue asked
/// for. What was heard was the word after it: the build running at 15:38 was 0.84.4, which still
/// marked the syllable rather than the vowel, and <c>starport</c> is not in the dictionary, so the
/// rules answered <c>ˈstæɹpɑːɹt</c> — the shape Kokoro renders as an intruded vowel. The vowel
/// landed between <em>observe</em> and <em>starport</em> and was heard as the end of the first.
/// <c>aeda4a3</c> fixed that at 19:25 the same evening, four hours after the report, and is in
/// v0.85.0; <see cref="TheStressMarkGoesBeforeTheVowelTests"/> is the guard on it.
/// </para>
/// </summary>
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

    /// <summary>
    /// <b>The check the issue asked for, and it passes at HEAD as it passed at 0.84.4.</b> That is
    /// the finding rather than a formality: it rules the dictionary rung out and sends the
    /// investigation to the word after it.
    /// </summary>
    [Fact]
    public void TheReportedSentenceSaysObserveTheDictionarysWay() =>
        Assert.Contains(
            "əbzˈɜːv",
            Rules.ToPhonemes("Ensure to observe starport protocol during your visit, pilot."),
            StringComparison.Ordinal);

    /// <summary>
    /// <b>And the line d47 actually said, which is where the spelled family came from.</b> Both
    /// emphasised words are said rather than spelled, and the emphasis itself makes no sound.
    /// </summary>
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

    /// <summary>
    /// One word wearing each mark in turn. The asterisk is the one that was reported; the rest are
    /// here because a model writes all of them freely and every one of them fails the same way.
    /// </summary>
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

    /// <summary>
    /// <b>Phrasing is kept and decoration is not.</b> Kokoro's vocabulary holds the em dash, the
    /// ellipsis and the curly quotes, and reads them as breath — so they are carried through beside
    /// the word rather than trimmed away with the asterisks, which it has no token for at all.
    /// </summary>
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

    /// <summary>
    /// <b>An em dash joins a compound the way a hyphen does.</b> A model writes
    /// <c>Booster—engineered</c> as readily as <c>well-known</c>, and the whole thing was one
    /// unsayable segment. The dash rule is unchanged: silent between two words, voiced where either
    /// side had to be spelled.
    /// </summary>
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
    /// And the dash a Commander's own example depends on is still voiced, because both sides of it
    /// are being spelled. This is the assertion that stops the widening above from being paid for
    /// by <c>COL 385 SECTOR B0-GQPI</c>.
    /// </summary>
    [Fact]
    public void ADashBetweenSpelledSegmentsIsStillSaid() =>
        Assert.Contains(
            "dˈæʃ", Rules.ToPhonemes("COL 385 SECTOR B0-GQPI"), StringComparison.Ordinal);

    // ---- The rung, said out loud ---------------------------------------------------------

    /// <summary>
    /// <b>The line that would have made this issue a read instead of an investigation.</b> Three
    /// words, three different rungs, named. It is off unless somebody is listening — the note is a
    /// callback the provider only fills in with a Debug log line.
    /// </summary>
    [Fact]
    public void TheLadderSaysWhichRungAnsweredForEachSegment()
    {
        var fell = new List<(string Segment, PhonemeRung Rung)>();

        var watched = new Phonemiser(
            new Shipped(), (segment, rung, _) => fell.Add((segment, rung)));

        watched.ToPhonemes("Guardian Kamitra GQPI");

        Assert.Contains(("Guardian", PhonemeRung.Dictionary), fell);
        Assert.Contains(("Kamitra", PhonemeRung.Rules), fell);
        Assert.Contains(("GQPI", PhonemeRung.Spelled), fell);
    }

    /// <summary>
    /// And it says it for the words that were reported, which is the read that was not available:
    /// the emphasised one now comes off the dictionary rather than off the last rung.
    /// </summary>
    [Fact]
    public void TheRungIsNamedForTheReportedWord()
    {
        var fell = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(new Shipped(), (segment, rung, _) => fell.Add((segment, rung)))
            .ToPhonemes("**Guardian**");

        Assert.Contains(("Guardian", PhonemeRung.Dictionary), fell);
    }
}
