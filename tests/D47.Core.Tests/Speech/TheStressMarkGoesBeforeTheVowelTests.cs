using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Where the stress mark sits inside a syllable, reported 2026-08-28 against the local voice
/// shipped in 0.84.0.
/// <para>
/// <b>Heard as an intruded vowel before every invented name.</b> A Commander asked d47 where they
/// were and it said <em>"JOHN ay DEPARAGON is in ay Kamitra, near ay Hammel Terminal, docked at ay
/// Hammel Terminal"</em>. The text was clean, the phonemes were clean English, and the sound was
/// not.
/// </para>
/// <para>
/// <b>The reported line is what makes the cause certain rather than likely.</b> Every word an
/// intruded vowel was heard before — Deparagon, Kamitra, Hammel, Hammel — is a word these rules
/// answered for. Every word without one — John, is, in, near, Terminal, docked, at — came from the
/// dictionary. Four for four and seven for seven, in a single sentence.
/// </para>
/// <para>
/// <b>And the dictionary says what the difference was.</b> Of its 274,927 entries, <b>not one</b>
/// begins with a stress mark followed by a consonant: it writes <c>dʒˈɑːn</c> and <c>tˈɜːmɪnəl</c>,
/// marking the vowel rather than the syllable. These rules marked the syllable — <c>ˈdɛpæɹæɡɑːn</c>
/// — so every name they produced reached Kokoro in a shape it had never once been given, and Kokoro
/// rendered the unfamiliar shape as a vowel.
/// </para>
/// </summary>
public class TheStressMarkGoesBeforeTheVowelTests
{
    private const char Mark = 'ˈ';

    /// <summary>
    /// The vowel sounds these rules can produce, from <c>Short</c> and <c>Long</c>. A stress mark is
    /// only ever allowed in front of one of these.
    /// <para>
    /// <c>j</c> and <c>w</c> are deliberately absent even though <c>juː</c> is what four vowel
    /// spellings answer with: a leading glide is part of the onset, which is the dictionary's own
    /// reading — <c>jˈuːnɪt</c>, <c>mjˈuːzɪk</c>.
    /// </para>
    /// </summary>
    private const string Vowels = "æɛɪɑʌɔəieaouɜ";

    /// <summary>
    /// The property, stated once: wherever the mark appears, the next sound is a vowel.
    /// <para>
    /// This is the assertion that was missing. Nothing pinned the mark's position, so moving it was
    /// free and getting it wrong was silent — the suite stayed green through the whole of the
    /// reported defect.
    /// </para>
    /// </summary>
    private static void MarksAVowel(string word)
    {
        var said = LetterToSound.Pronounce(word);

        Assert.NotNull(said);

        var at = said.IndexOf(Mark, StringComparison.Ordinal);

        if (at < 0)
        {
            return; // A single syllable carries no mark, which is the existing rule and not this one.
        }

        Assert.True(at + 1 < said.Length, $"\"{word}\" -> \"{said}\" ends on a stress mark.");

        Assert.True(
            Vowels.Contains(said[at + 1], StringComparison.Ordinal),
            $"\"{word}\" -> \"{said}\" marks '{said[at + 1]}', which is not a vowel. "
            + "The dictionary never does this in 274,927 entries.");
    }

    // ---- The reported words ----------------------------------------------------------------

    /// <summary>
    /// The three names from the reported sentence, each with the mark now after its onset instead
    /// of in front of it.
    /// </summary>
    [Theory]
    [InlineData("Deparagon", "dˈɛpæɹæɡɑːn")]
    [InlineData("Kamitra", "kˈæmɪtɹə")]
    [InlineData("Hammel", "hˈæmɛl")]
    public void TheReportedNamesMarkTheirVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And none of them begins with the mark any more, which is the shape the dictionary has no
    /// instance of and Kokoro turned into a vowel.
    /// </summary>
    [Theory]
    [InlineData("Deparagon")]
    [InlineData("Kamitra")]
    [InlineData("Hammel")]
    [InlineData("Dezhra")]
    [InlineData("Shinrarta")]
    [InlineData("Sothis")]
    [InlineData("Colonia")]
    [InlineData("Maia")]
    public void ANameNeverBeginsWithAStressMarkBeforeAConsonant(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        Assert.False(
            said.Length > 1 && said[0] == Mark && !Vowels.Contains(said[1], StringComparison.Ordinal),
            $"\"{word}\" -> \"{said}\" opens on a mark before a consonant.");
    }

    // ---- The property, over a wide spread ----------------------------------------------------

    /// <summary>
    /// Names of every shape the game throws at this — onsets of one, two and three consonants,
    /// vowel-initial words, digraphs, and the glide spellings that are the one real trap.
    /// </summary>
    [Theory]
    [InlineData("Deparagon")]
    [InlineData("Kamitra")]
    [InlineData("Hammel")]
    [InlineData("Terminal")]
    [InlineData("Shinrarta")]
    [InlineData("Dezhra")]
    [InlineData("Achenar")]
    [InlineData("Alioth")]
    [InlineData("Eravate")]
    [InlineData("Ovid")]
    [InlineData("Struthio")]
    [InlineData("Sprigatto")]
    [InlineData("Christo")]
    [InlineData("Europa")]
    [InlineData("Eureka")]
    [InlineData("Unity")]
    [InlineData("Music")]
    [InlineData("Newton")]
    [InlineData("Cutter")]
    [InlineData("Python")]
    [InlineData("Anaconda")]
    [InlineData("Imperial")]

    // The silent-e words (#153). A rule that drops the last syllable changes which syllable is
    // last and whether there are two of them at all, so it reaches everything this file guards.
    [InlineData("Lave")]
    [InlineData("Orbite")]
    [InlineData("Shinrarte")]
    [InlineData("observe")]
    [InlineData("Deciate")]
    public void WhereverTheMarkIsTheNextSoundIsAVowel(string word) => MarksAVowel(word);

    /// <summary>
    /// <b>A vowel-initial word is unaffected</b>, because there the two positions are the same one.
    /// That is not an exception to the rule — it is the 5.7% of dictionary entries that do begin
    /// with the mark, and every one of them has a vowel after it.
    /// </summary>
    [Fact]
    public void AVowelInitialNameStillOpensOnTheMark()
    {
        var said = LetterToSound.Pronounce("Alioth")!;

        Assert.StartsWith("ˈ", said, StringComparison.Ordinal);
        Assert.True(Vowels.Contains(said[1], StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The glide is the trap.</b> <c>eu</c>, <c>ew</c> and a long <c>u</c> all answer with
    /// <c>juː</c>, so marking the front of the vowel string would put the mark back in front of a
    /// consonant for exactly those words — the same fault, reached by a different road. The
    /// dictionary settles it: <c>jˈuːnɪt</c>, <c>mjˈuːzɪk</c>, <c>fjˈuː</c>, <c>kjˈuːt</c>.
    /// </summary>
    [Theory]
    [InlineData("Unity")]
    [InlineData("Music")]
    [InlineData("Euclid")]
    [InlineData("Eucla")]
    public void AGlideBelongsToTheOnsetAndNotTheNucleus(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        MarksAVowel(word);

        // Said the other way round: the mark is never immediately before the glide.
        Assert.DoesNotContain("ˈj", said, StringComparison.Ordinal);
        Assert.DoesNotContain("ˈw", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A single syllable carries no mark at all, which is the existing ruling and is untouched:
    /// what changed is where a mark goes, never whether there is one.
    /// </summary>
    [Theory]
    [InlineData("Kuk")]
    [InlineData("Sol")]
    [InlineData("Bast")]
    public void ASingleSyllableIsStillUnmarked(string word) =>
        Assert.DoesNotContain("ˈ", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>
    /// <b>The same rule for the number words, which is where it was found a second time.</b> Those
    /// are hand-written rather than derived, and eighteen of the thirty marked a consonant —
    /// <c>ˈθəɹti</c>, <c>ˈsɛvən</c>, <c>ˈhʌndɹəd</c>. Fixing <see cref="LetterToSound"/> did not
    /// reach them, and they matter more: d47 says numbers constantly, and a range or a tonnage is
    /// in most of what it reports.
    /// <para>
    /// The set is closed and small, so every entry is checked rather than a sample.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryNumberWordMarksAVowel()
    {
        foreach (var (word, said) in SpokenNumber.Sounds)
        {
            for (var i = said.IndexOfAny(['ˈ', 'ˌ']); i >= 0; i = said.IndexOfAny(['ˈ', 'ˌ'], i + 1))
            {
                Assert.True(
                    i + 1 < said.Length && Vowels.Contains(said[i + 1], StringComparison.Ordinal),
                    $"\"{word}\" -> \"{said}\" marks '{(i + 1 < said.Length ? said[i + 1] : ' ')}', "
                    + "which is not a vowel.");
            }
        }
    }

    /// <summary>
    /// And every number word is in the table, so the assertion above cannot pass by the table having
    /// quietly lost one — the ladder falls through to the rules for anything missing, and the rules
    /// read <c>eighty</c> as <c>eɪɡtaɪ</c>.
    /// </summary>
    [Theory]
    [InlineData("thirty")]
    [InlineData("seven")]
    [InlineData("hundred")]
    [InlineData("thirteen")]
    [InlineData("eleven")]
    public void TheNumberWordsAreStillAllThere(string word) =>
        Assert.True(SpokenNumber.Sounds.ContainsKey(word), $"\"{word}\" is no longer in the table.");

    /// <summary>
    /// <b>Every letter and digit, which is the third place this lived.</b> Fixing the rules did not
    /// reach the hand-written tables, and there turned out to be two of them — the number words and
    /// the spelled letters. <c>w</c> was <c>ˈdʌbəljuː</c> and the spoken digits carried
    /// <c>ˈzɪɹoʊ</c> and <c>ˈsɛvən</c>.
    /// <para>
    /// This is the assertion that makes a fourth place impossible to add quietly: every rung of the
    /// ladder that produces IPA is driven here, so a new literal marking a consonant fails whichever
    /// table somebody puts it in.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(SpeechAccent.American)]
    [InlineData(SpeechAccent.British)]
    public void EveryLetterAndDigitMarksAVowel(SpeechAccent accent)
    {
        foreach (var character in "abcdefghijklmnopqrstuvwxyz0123456789")
        {
            if (SpokenLetters.Say(character, accent) is not { } said)
            {
                continue;
            }

            for (var i = said.IndexOfAny(['ˈ', 'ˌ']); i >= 0; i = said.IndexOfAny(['ˈ', 'ˌ'], i + 1))
            {
                Assert.True(
                    i + 1 < said.Length && Vowels.Contains(said[i + 1], StringComparison.Ordinal),
                    $"'{character}' -> \"{said}\" marks a consonant.");
            }
        }
    }

    /// <summary>
    /// And the whole sentence that was reported, through the ladder, with nothing marking a
    /// consonant anywhere in it.
    /// </summary>
    [Theory]
    [InlineData("JOHN DEPARAGON is in Kamitra, near Hammel Terminal, docked at Hammel Terminal.")]

    // <b>The other reported sentence, and it is this file's business rather than #153's.</b> The
    // build that said it was 0.84.4, which still marked the syllable, and "starport" is not in the
    // dictionary — so the rules answered it, put the mark in front of the "st", and Kokoro rendered
    // the shape it had never been given as a vowel. That vowel landed between "observe" and
    // "starport" and was reported as "observ-eh". The word before it was never at fault.
    [InlineData("Ensure to observe starport protocol during your visit, pilot.")]

    // And a line of decoration, because #153's other half sends words to rungs they were not
    // reaching before: an emphasised word now comes off the rules or the dictionary, and a mark
    // that was never produced cannot have been guarded.
    [InlineData("**Guardian** FSD Booster — engineered, at “Perez Ring”…")]
    public void TheReportedSentenceMarksNoConsonant(string line)
    {
        var said = new Phonemiser().ToPhonemes(line);

        for (var i = said.IndexOf(Mark); i >= 0; i = said.IndexOf(Mark, i + 1))
        {
            Assert.True(
                i + 1 < said.Length && Vowels.Contains(said[i + 1], StringComparison.Ordinal),
                $"\"{said}\" marks a consonant at {i}.");
        }
    }
}
