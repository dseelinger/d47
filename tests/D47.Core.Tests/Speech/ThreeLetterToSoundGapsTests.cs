using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Three gaps in the letter-to-sound rules, found next door to #153/#150 and fixed together
/// (<a href="https://github.com/dseelinger/d47/issues/179">#179</a>).
/// <para>
/// They are one file's business and they are met by the same reader, which is why they are one
/// issue: a soft <c>g</c> that vanished, a coda English cannot make, and a schwa on the wrong side
/// of an <c>l</c>. None of them is reachable for a word the dictionary holds — these rules only
/// answer for invented names and for the Commander's own respellings — which is what sets their
/// severity, and why the second one bites first: <em>tah</em> and <em>rah</em> are exactly how a
/// person writes a syllable into the pronunciations file
/// (<a href="https://github.com/dseelinger/d47/issues/150">#150</a>).
/// </para>
/// </summary>
public class ThreeLetterToSoundGapsTests
{
    // ---- 1. nge keeps its soft g --------------------------------------------------------------

    /// <summary>
    /// <b><c>change</c> was <c>tʃæŋ</c>.</b> The coda parses — <c>nge</c> is one — but there was no
    /// spelling for it, so the letters matched singly, <c>ng</c> answered first and the /dʒ/ went
    /// out with the <c>e</c> nothing said.
    /// </summary>
    [Theory]
    [InlineData("change", "tʃeɪndʒ")]
    [InlineData("range", "ɹeɪndʒ")]
    [InlineData("strange", "stɹeɪndʒ")]
    [InlineData("hinge", "hɪndʒ")]
    [InlineData("fringe", "fɹɪndʒ")]
    [InlineData("binge", "bɪndʒ")]
    [InlineData("plunge", "plʌndʒ")]
    public void AnNgeKeepsItsSoftG(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// Said the other way round, because this is the defect rather than the reading: the affricate
    /// is there at all, and the velar nasal that used to eat it is not.
    /// </summary>
    [Theory]
    [InlineData("change")]
    [InlineData("hinge")]
    [InlineData("Deciange")]
    public void TheAffricateIsNoLongerSwallowedByTheNasal(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        Assert.Contains("ndʒ", said, StringComparison.Ordinal);
        Assert.DoesNotContain("ŋ", said, StringComparison.Ordinal);
    }

    // ---- The ruling #179 asked for, and what settles it ---------------------------------------

    /// <summary>
    /// <b>The vowel-length ruling, measured rather than judged.</b> #179 left <c>nge</c> open
    /// because the examples pull both ways — <em>change</em> lengthens, <em>hinge</em> does not —
    /// and asked for a decision. It was taken by counting the shipped dictionary on 2026-08-29:
    /// after a single <c>a</c>, <c>nge</c> is long in <b>25 of 32</b> entries, and after every
    /// other single vowel it is short in <b>57 of 58</b>.
    /// <para>
    /// So the rule is the vowel, and only the vowel. The seven exceptions under <c>a</c> are French
    /// loans — <em>blancmange</em>, <em>melange</em>, <em>vendange</em>, <em>fontange</em> — plus
    /// <em>orange</em>, <em>flange</em> and <em>phalange</em>, and every one of them is a word the
    /// dictionary holds, so these rules never answer for any of them.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("change", "eɪ")]
    [InlineData("range", "eɪ")]
    [InlineData("arrange", "eɪ")]
    [InlineData("hinge", "ɪ")]
    [InlineData("fringe", "ɪ")]
    [InlineData("plunge", "ʌ")]
    [InlineData("binge", "ɪ")]
    public void OnlyAnAIsLengthenedBeforeNge(string word, string vowel) =>
        Assert.Contains(vowel + "ndʒ", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>
    /// <b><c>ze</c> lengthens: 1,227 of 1,236 dictionary entries.</b> <em>maze</em>, <em>size</em>,
    /// <em>doze</em>, <em>prize</em> — the count is as near exceptionless as this file gets.
    /// </summary>
    [Theory]
    [InlineData("maze", "meɪz")]
    [InlineData("size", "saɪz")]
    [InlineData("doze", "doʊz")]
    [InlineData("prize", "pɹaɪz")]
    public void AZeLengthensTheVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b><c>dge</c> never does: 74 of 76 short.</b> Which is the whole reason the spelling exists
    /// — <c>dge</c> is how English writes a short vowel in front of /dʒ/, and a rule that lengthened
    /// it would be undoing the one thing the letters are there to say.
    /// </summary>
    [Theory]
    [InlineData("badge", "bædʒ")]
    [InlineData("bridge", "bɹɪdʒ")]
    [InlineData("judge", "dʒʌdʒ")]
    [InlineData("hedge", "hɛdʒ")]
    public void ADgeNeverLengthensTheVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>A digraph needs no row and is deliberately not asked about</b>: <c>au</c>, <c>ou</c> and
    /// <c>ee</c> already carry their own length, which is why these come out right without being
    /// special cases — and they were the whole of the apparent counter-evidence in the count.
    /// </summary>
    [Theory]
    [InlineData("lounge", "aʊndʒ")]
    [InlineData("chaunge", "ɔːndʒ")]
    public void ADigraphSaysItsOwnLength(string word, string expected) =>
        Assert.Contains(expected, LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    // ---- 2. A lone coda h is not voiced -------------------------------------------------------

    /// <summary>
    /// <b><c>tah</c> was <c>tæh</c>, and English has no coda /h/ anywhere.</b> This is the one of
    /// the three that bites first, because <em>tah</em> and <em>rah</em> are how a person writes a
    /// syllable into the pronunciations file — so anybody using #150 meets it on their first entry.
    /// </summary>
    [Theory]
    [InlineData("tah", "tæ")]
    [InlineData("rah", "ɹæ")]
    [InlineData("kah", "kæ")]
    public void ALoneCodaHIsDropped(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>Only a lone one.</b> <c>gh</c>, <c>sh</c>, <c>th</c>, <c>ch</c> and <c>zh</c> are codas
    /// that end in the letter and are not the sound, and dropping the h out of those would break
    /// far more than it fixed — <c>Dezhra</c> is the most-spoken system name in the game.
    /// </summary>
    [Theory]
    [InlineData("Dezhra", "ʒ")]
    [InlineData("bath", "θ")]
    [InlineData("dish", "ʃ")]
    [InlineData("rich", "tʃ")]
    public void ACodaThatMerelyEndsInHKeepsItsSound(string word, string sound) =>
        Assert.Contains(sound, LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>Said as the property: no reading these rules produce contains a /h/ in a coda.</summary>
    [Theory]
    [InlineData("tah")]
    [InlineData("rah")]
    [InlineData("Shinrartah")]
    [InlineData("Kamitrah")]
    public void NoReadingEndsOnAnH(string word) =>
        Assert.DoesNotContain("h", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    // ---- 3. -le and -re are syllabic ----------------------------------------------------------

    /// <summary>
    /// <b><c>table</c> was <c>tˈæblə</c>.</b> The parse hands back <em>ta.ble</em>, the <c>l</c> is
    /// that syllable's onset, and the reduction rule then voiced the <c>e</c> after it. English says
    /// the schwa first and the consonant second, and the <c>e</c> — like a silent one — lengthens
    /// what it left behind, which is the other half of why <em>table</em> is not <em>tabble</em>.
    /// </summary>
    [Theory]
    [InlineData("table", "tˈeɪbəl")]
    [InlineData("maple", "mˈeɪpəl")]
    [InlineData("noble", "nˈoʊbəl")]
    [InlineData("cycle", "sˈaɪkəl")]
    [InlineData("acre", "ˈeɪkəɹ")]
    [InlineData("ogre", "ˈoʊɡəɹ")]
    public void ASyllabicLOrRPutsItsSchwaFirst(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>And two sounds in front of it keep the vowel short</b>, which is the same rule the silent
    /// <c>e</c> already follows and the reason <c>dge</c> exists — <em>apple</em> and <em>little</em>
    /// are not <em>ape-le</em> and <em>lite-le</em>.
    /// </summary>
    [Theory]
    [InlineData("apple", "ˈæpəl")]
    [InlineData("little", "lˈɪtəl")]
    [InlineData("castle", "kˈæstəl")]
    [InlineData("gentle", "dʒˈɛntəl")]
    public void TwoSoundsInFrontOfItKeepTheVowelShort(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>The boundary against the silent e, from the other side.</b> <c>-ale</c> and <c>-ole</c>
    /// hand their <c>e</c> back bare and are the silent-e rule's (#153); <c>-ble</c> and <c>-cre</c>
    /// hand it back with an onset and are this one's. Both readings are right and they are not the
    /// same rule, which is what this pins.
    /// </summary>
    [Theory]
    [InlineData("Lave", "leɪv")]
    [InlineData("hole", "hoʊl")]
    [InlineData("male", "meɪl")]
    public void AnEHandedBackBareIsStillTheSilentERule(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    // ---- And the guard the whole file answers to ----------------------------------------------

    /// <summary>
    /// <b>Every reading here goes through the stress-mark guard</b>, which is the rule for anything
    /// that produces IPA: the theory list in
    /// <see cref="TheStressMarkGoesBeforeTheVowelTests"/> is extended rather than a second guard
    /// written beside it. Restated here on the words this file added, because a rule that moves a
    /// schwa in front of a consonant is exactly the shape of change that put a mark in front of one.
    /// </summary>
    [Theory]
    [InlineData("table")]
    [InlineData("acre")]
    [InlineData("gentle")]
    [InlineData("change")]
    [InlineData("tah")]
    public void TheseReadingsMarkAVowel(string word)
    {
        var said = LetterToSound.Pronounce(word)!;
        var at = said.IndexOf('ˈ', StringComparison.Ordinal);

        if (at < 0)
        {
            return;
        }

        Assert.True(
            at + 1 < said.Length
            && "æɛɪɑʌɔəieaouɜ".Contains(said[at + 1], StringComparison.Ordinal),
            $"\"{word}\" -> \"{said}\" marks a consonant.");
    }
}
