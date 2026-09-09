using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>Where the stress mark sits inside a syllable against the local voice shipped in 0.84.0.</summary>
public class TheStressMarkGoesBeforeTheVowelTests
{
    private const char Mark = 'ˈ';

    /// <summary>The vowel sounds these rules can produce, from <c>Short</c> and <c>Long</c>.</summary>
    private const string Vowels = "æɛɪɑʌɔəieaouɜ";

    /// <summary>The property, stated once: wherever the mark appears, the next sound is a vowel.</summary>
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
    /// The three names from the reported sentence, each with the mark now after its onset instead of in
    /// front of it.
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

 // The silent-e words.
    [InlineData("Lave")]
    [InlineData("Orbite")]
    [InlineData("Shinrarte")]
    [InlineData("observe")]
    [InlineData("Deciate")]

    // The three gaps of #179, for the same reason.
    [InlineData("table")]
    [InlineData("acre")]
    [InlineData("gentle")]
    [InlineData("circle")]
    [InlineData("Kamitrable")]
    [InlineData("change")]
    [InlineData("Deciange")]
    [InlineData("Shinrartah")]
    [InlineData("tah")]

    // The four gaps of #184, for the same reason again.
    [InlineData("light")]
    [InlineData("lighter")]
    [InlineData("thought")]
    [InlineData("lightweight")]
    [InlineData("single")]
    [InlineData("angle")]
    [InlineData("Kamitrangle")]
    [InlineData("uncle")]
    [InlineData("muscle")]
    [InlineData("centre")]
    [InlineData("tickle")]
    [InlineData("Shinrancle")]
    public void WhereverTheMarkIsTheNextSoundIsAVowel(string word) => MarksAVowel(word);

    /// <summary>A vowel-initial word is unaffected, because there the two positions are the same one.</summary>
    [Fact]
    public void AVowelInitialNameStillOpensOnTheMark()
    {
        var said = LetterToSound.Pronounce("Alioth")!;

        Assert.StartsWith("ˈ", said, StringComparison.Ordinal);
        Assert.True(Vowels.Contains(said[1], StringComparison.Ordinal));
    }

    /// <summary>The glide is the trap.</summary>
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
    /// A single syllable carries no mark at all, which is the existing ruling and is untouched: what
    /// changed is where a mark goes, never whether there is one.
    /// </summary>
    [Theory]
    [InlineData("Kuk")]
    [InlineData("Sol")]
    [InlineData("Bast")]
    public void ASingleSyllableIsStillUnmarked(string word) =>
        Assert.DoesNotContain("ˈ", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>The same rule for the number words, which is where it was found a second time.</summary>
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
    /// silently lost one — the ladder falls through to the rules for anything missing, and the rules
    /// read <c>eighty</c> as <c>eɪɡtaɪ</c>.
    /// </summary>
    [Theory]
    [InlineData("thirty")]
    [InlineData("seven")]
    [InlineData("hundred")]
    [InlineData("thirteen")]
    [InlineData("eleven")]

    // "point" joined them with #177, and it is in the table for the same reason the rest are: this is the
    // fallback for a build whose dictionary never downloaded, and a decimal is said as often as any number
    // word above it.
    [InlineData("point")]

    // And the scale words joined them with #184, which ruled that a measured quantity takes the full reading
    // — so "thousand" is now a word this rung produces, and d47 reports credits in billions.
    [InlineData("thousand")]
    [InlineData("million")]
    [InlineData("billion")]
    [InlineData("trillion")]
    public void TheNumberWordsAreStillAllThere(string word) =>
        Assert.True(SpokenNumber.Sounds.ContainsKey(word), $"\"{word}\" is no longer in the table.");

    /// <summary>Every letter and digit, which is the third place this lived.</summary>
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
    /// And the whole sentence that was reported, through the ladder, with nothing marking a consonant
    /// anywhere in it.
    /// </summary>
    [Theory]
    [InlineData("JOHN DEPARAGON is in Kamitra, near Hammel Terminal, docked at Hammel Terminal.")]

    // The other reported sentence, and it is this file's business rather than #153's. The build that
    // said it was 0.84.4, which still marked the syllable, and "starport" is not in the dictionary — so the
    // rules answered it, put the mark in front of the "st", and Kokoro rendered the shape it had never been
    // given as a vowel.
    [InlineData("Ensure to observe starport protocol during your visit, pilot.")]

    // And a line of decoration, because #153's other half sends words to rungs they were not reaching before:
    // an emphasised word now comes off the rules or the dictionary, and a mark that was never produced cannot
    // have been guarded.
    [InlineData("**Guardian** FSD Booster — engineered, at “Perez Ring”…")]

 // And the decimals, which send a shape to the number rung instead.
    [InlineData("Perez Ring, LHS 2637 — 5.79 ly, 395 ls out, large pad.")]
    [InlineData("1 ly, 1.5 ly, 0.5, .79 and 128.5 tonnes.")]

 // And the grouped numbers: "6,680" comes off the number rung.
    [InlineData("That will be 6,680 credits for 1,234 tonnes.")]
    [InlineData("1,234.5 ly out, 9,876,543,210 credits, and 12,345 ls.")]

    // And #184's, which reaches this file twice over: the scale words are marks the number rung had never
    // produced, and "light", "single" and "uncle" are marks the rules had never produced because those words
    // were spelled or read wrong.
    [InlineData("Deciat is 1234.5 light years out, and that is a single jump.")]
    [InlineData("The uncle at that angle thought 9,876,543,210 credits was a tight squeeze.")]
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

    /// <summary>And the Commander's own corrections go through the same guard.</summary>
    [Fact]
    public void ARespelledOverrideMarksAVowel()
    {
        var folder = Directory.CreateTempSubdirectory("d47-marks").FullName;

        try
        {
            var file = Path.Combine(folder, PronunciationOverrides.FileName);

            File.WriteAllText(file, """{ "Deciat": "desh ee at", "Kuk": "kook" }""");

            var said = new Phonemiser(null, new PronunciationOverrides(file))
                .ToPhonemes("Deciat and Kuk");

            for (var i = said.IndexOf(Mark); i >= 0; i = said.IndexOf(Mark, i + 1))
            {
                Assert.True(
                    i + 1 < said.Length && Vowels.Contains(said[i + 1], StringComparison.Ordinal),
                    $"\"{said}\" marks a consonant at {i}.");
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
