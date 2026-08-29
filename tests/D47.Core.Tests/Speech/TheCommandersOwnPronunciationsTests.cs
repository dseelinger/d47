using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The correction file, asked for on 2026-08-28 (#150): <em>"We'll need to come up with a way to
/// update Kokoro pronunciations without recompiling."</em>
/// <para>
/// <b>Every other road to a wrong word ended in a release.</b> The ladder's rungs are a shipped
/// dictionary and code, so a name the voice got wrong cost a patch to correct — and the 410 game
/// words the local voice knew would fall through the dictionary are exactly the vocabulary
/// Frontier adds to and the community argues about. This is the escape valve, and
/// <see href="https://github.com/dseelinger/d47/issues/153">#153</see> is why it was worth building
/// early: while a class of rules bug exists, one file fixes any single word it touches.
/// </para>
/// <para>
/// Driven through the real file rather than an in-memory stand-in, because <em>hot-reloaded</em> is
/// half the feature and a stand-in cannot be saved.
/// </para>
/// </summary>
public class TheCommandersOwnPronunciationsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("d47-pronunciations").FullName;

    private string File => Path.Combine(_folder, PronunciationOverrides.FileName);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>The two entries the shipped dictionary would otherwise answer, so the rung order is testable.</summary>
    private sealed class Shipped : IPronunciationDictionary
    {
        private readonly Dictionary<string, string> _words = new(StringComparer.OrdinalIgnoreCase)
        {
            ["observe"] = "əbzˈɜːv",
            ["male"] = "mˈeɪl",
            ["female"] = "fˈiːmeɪl",
            ["and"] = "ænd",
        };

        public string? Lookup(string word) => _words.GetValueOrDefault(word);
    }

    private void Write(string json) =>
        System.IO.File.WriteAllText(File, json);

    private Phonemiser Ladder(
        IReadOnlySet<char>? speakable = null, Action<string>? complain = null) =>
        new(new Shipped(), new PronunciationOverrides(File, speakable, complain));

    // ---- The two ways to write one -------------------------------------------------------

    /// <summary>
    /// <b>A respelling goes through the ladder</b>, which is what makes the easy case writable by
    /// anybody: no IPA, no symbols, just the word spelled the way it sounds.
    /// </summary>
    [Fact]
    public void ARespellingIsRunDownTheLadder()
    {
        Write("""{ "Deciat": "dessy at" }""");

        Assert.Equal(
            new Phonemiser().ToPhonemes("dessy at"),
            Ladder().ToPhonemes("Deciat"));
    }

    /// <summary>
    /// <b>And raw IPA goes straight to the tokenizer</b>, for the Commander who wants exact control
    /// — including of the stress, which a respelling cannot express because the ladder is
    /// case-blind.
    /// </summary>
    [Fact]
    public void IpaIsTakenExactlyAsWritten()
    {
        Write("""{ "Dezhra": "ipa:ʃɪnˈɹɑːɹtə" }""");

        Assert.Equal("ʃɪnˈɹɑːɹtə", Ladder().ToPhonemes("Dezhra"));
    }

    /// <summary>The marker is a marker rather than a magic word, so its case does not matter.</summary>
    [Fact]
    public void TheIpaMarkerIsCaseInsensitive()
    {
        Write("""{ "Dezhra": "IPA: ˈdɛʒɹə" }""");

        Assert.Equal("ˈdɛʒɹə", Ladder().ToPhonemes("Dezhra"));
    }

    // ---- Where it sits on the ladder -----------------------------------------------------

    /// <summary>
    /// <b>It wins over the dictionary and loses to nothing.</b> The dictionary holds
    /// <c>observe</c>, and the whole point of this layer is that a Commander's answer outranks
    /// every answer d47 shipped with.
    /// </summary>
    [Fact]
    public void AnOverrideBeatsTheDictionary()
    {
        Write("""{ "observe": "ipa:ɑːbzˈɜːv" }""");

        Assert.Equal("ɑːbzˈɜːv", Ladder().ToPhonemes("observe"));
    }

    /// <summary>And over the rules, which is the rung a game word usually lands on.</summary>
    [Fact]
    public void AnOverrideBeatsTheRules()
    {
        Write("""{ "Kamitra": "ipa:kəmˈiːtɹə" }""");

        Assert.Equal("kəmˈiːtɹə", Ladder().ToPhonemes("Kamitra"));
        Assert.NotEqual("kəmˈiːtɹə", new Phonemiser().ToPhonemes("Kamitra"));
    }

    /// <summary>
    /// <b>Case-insensitive, because a system name is written however it was typed</b> — the journal
    /// shouts some of them and the model title-cases others.
    /// </summary>
    [Theory]
    [InlineData("DEZHRA")]
    [InlineData("dezhra")]
    [InlineData("Dezhra")]
    public void TheKeyIsMatchedWhateverTheCase(string written)
    {
        Write("""{ "Dezhra": "ipa:ˈdɛʒɹə" }""");

        Assert.Equal("ˈdɛʒɹə", Ladder().ToPhonemes(written));
    }

    /// <summary>
    /// <b>Whole words, so an entry cannot capture a substring.</b> That is
    /// <see href="https://github.com/dseelinger/d47/issues/146">#146</see>'s lesson applied before
    /// it could be learned twice: an override for <em>male</em> must not reach inside
    /// <em>female</em>, and one for <em>observe</em> must not reach inside <em>observed</em>.
    /// </summary>
    [Fact]
    public void AnEntryNeverCapturesASubstring()
    {
        Write("""{ "male": "ipa:mˈɑːl", "observe": "ipa:ɑːbzˈɜːv" }""");

        var ladder = Ladder();

        Assert.Equal("fˈiːmeɪl", ladder.ToPhonemes("female"));
        Assert.DoesNotContain("ɑːbzˈɜːv", ladder.ToPhonemes("observed"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A key may be more than one word</b>, which is what the issue's own example needs:
    /// <em>Shinrarta Dezhra</em> is one name and is said as one. Matched longest first, so a
    /// two-word entry beats a one-word entry that starts the same way.
    /// </summary>
    [Fact]
    public void APhraseIsMatchedAsOneName()
    {
        Write("""
        {
          "Shinrarta Dezhra": "ipa:ʃɪnˈɹɑːɹtə ˈdɛʒɹə",
          "Shinrarta": "ipa:ʃɪnˈɹɑːɹtə"
        }
        """);

        var ladder = Ladder();

        Assert.Equal("ʃɪnˈɹɑːɹtə ˈdɛʒɹə", ladder.ToPhonemes("Shinrarta Dezhra"));
        Assert.Equal("ʃɪnˈɹɑːɹtə", ladder.ToPhonemes("Shinrarta"));
    }

    /// <summary>
    /// And the punctuation the words were wearing survives, because a name at the end of a sentence
    /// still ends the sentence.
    /// </summary>
    [Fact]
    public void ThePunctuationAfterAMatchedNameIsKept()
    {
        Write("""{ "Dezhra": "ipa:ˈdɛʒɹə" }""");

        Assert.Equal("ˈdɛʒɹə,", Ladder().ToPhonemes("**Dezhra**,"));
    }

    // ---- Living with it ------------------------------------------------------------------

    /// <summary>
    /// <b>Hot-reloaded, which is the whole feature.</b> "Without recompiling" is really "without
    /// leaving the game": edit, save, say the word again, hear the difference.
    /// </summary>
    [Fact]
    public void AnEditIsLiveOnTheNextThingSaid()
    {
        var ladder = Ladder();

        Write("""{ "Dezhra": "ipa:ˈdɛʒɹə" }""");
        Assert.Equal("ˈdɛʒɹə", ladder.ToPhonemes("Dezhra"));

        Touch("""{ "Dezhra": "ipa:dɛzˈɹɑː" }""");
        Assert.Equal("dɛzˈɹɑː", ladder.ToPhonemes("Dezhra"));
    }

    /// <summary>
    /// <b>Deleting the file restores shipped behaviour exactly</b>, which is why nothing writes one
    /// back: a file d47 recreated on the next start would make this untrue.
    /// </summary>
    [Fact]
    public void DeletingTheFileRestoresTheShippedLadder()
    {
        var ladder = Ladder();

        Write("""{ "Dezhra": "ipa:dɛzˈɹɑː" }""");
        Assert.Equal("dɛzˈɹɑː", ladder.ToPhonemes("Dezhra"));

        System.IO.File.Delete(File);

        Assert.Equal(new Phonemiser(new Shipped()).ToPhonemes("Dezhra"), ladder.ToPhonemes("Dezhra"));
    }

    /// <summary>And an absent file is the default, which is no behaviour at all.</summary>
    [Fact]
    public void NoFileIsNoChange() =>
        Assert.Equal(
            new Phonemiser(new Shipped()).ToPhonemes("Shinrarta Dezhra"),
            Ladder().ToPhonemes("Shinrarta Dezhra"));

    // ---- When it is written wrong --------------------------------------------------------

    /// <summary>
    /// <b>A bad entry degrades to the ladder and is named once.</b> Never a crash, never silence,
    /// and never a per-utterance nag — the file is stamped before it is read, so a Commander who
    /// leaves a broken entry in place hears about it when they save and not on every line
    /// afterwards.
    /// </summary>
    [Fact]
    public void ABadEntryFallsThroughAndIsNamedOnce()
    {
        var complaints = new List<string>();

        Write("""
        {
          "empty": "",
          "silent": "   ",
          "marker only": "ipa:",
          "no sound in it": "!!!",
          "Dezhra": "ipa:ˈdɛʒɹə"
        }
        """);

        var ladder = Ladder(complain: complaints.Add);

        // The good entry still works, which is the point of degrading per entry rather than per
        // file: one typo must not throw away the other nine corrections.
        Assert.Equal("ˈdɛʒɹə", ladder.ToPhonemes("Dezhra"));

        // And each bad one is said once, by name, however many times d47 speaks afterwards.
        Assert.Equal(4, complaints.Count);
        Assert.All(
            new[] { "empty", "silent", "marker only", "no sound in it" },
            name => Assert.Contains(complaints, said => said.Contains(name, StringComparison.Ordinal)));

        ladder.ToPhonemes("Dezhra");
        ladder.ToPhonemes("Dezhra");

        Assert.Equal(4, complaints.Count);
    }

    /// <summary>
    /// <b>IPA this voice cannot say is refused</b>, and that is what makes "unparseable" a real
    /// check rather than a guess: a symbol with no token is dropped on the way to the model, so an
    /// entry made of them is silence — and an override that silences a word is worse than the wrong
    /// word it was correcting.
    /// </summary>
    [Fact]
    public void IpaTheVoiceCannotSayIsRefused()
    {
        var complaints = new List<string>();

        Write("""{ "Dezhra": "ipa:d3zhr@" }""");

        var ladder = Ladder("ˈdɛʒɹə".ToHashSet(), complaints.Add);

        Assert.Equal(new Phonemiser(new Shipped()).ToPhonemes("Dezhra"), ladder.ToPhonemes("Dezhra"));
        Assert.Contains(complaints, said => said.Contains('3'));
    }

    /// <summary>
    /// <b>A file that cannot be parsed at all leaves the last good entries standing.</b> A
    /// half-written file is what a save looks like from here, and throwing a Commander's
    /// corrections away because they were mid-keystroke would be worse than being one edit behind.
    /// </summary>
    [Fact]
    public void AHalfWrittenFileDoesNotThrowTheGoodEntriesAway()
    {
        var complaints = new List<string>();
        var ladder = Ladder(complain: complaints.Add);

        Write("""{ "Dezhra": "ipa:dɛzˈɹɑː" }""");
        Assert.Equal("dɛzˈɹɑː", ladder.ToPhonemes("Dezhra"));

        Touch("""{ "Dezhra": "ipa:dɛ""");

        Assert.Equal("dɛzˈɹɑː", ladder.ToPhonemes("Dezhra"));
        Assert.Single(complaints);
    }

    /// <summary>
    /// Comments and a trailing comma are allowed, because this is a file a person types into and a
    /// parser refusing a trailing comma is not a thing to make somebody debug by ear.
    /// </summary>
    [Fact]
    public void ItReadsWhatAPersonWouldActuallyType()
    {
        Write("""
        {
          // Frontier say it dezh-rah.
          "Dezhra": "ipa:ˈdɛʒɹə",
        }
        """);

        Assert.Equal("ˈdɛʒɹə", Ladder().ToPhonemes("Dezhra"));
    }

    /// <summary>
    /// A respelling that names the word it is correcting does not go round for ever: the
    /// respelling is run down the rest of the ladder with this layer switched off.
    /// </summary>
    [Fact]
    public void ARespellingThatNamesItselfTerminates()
    {
        Write("""{ "Kuk": "Kuk" }""");

        Assert.Equal(new Phonemiser().ToPhonemes("Kuk"), Ladder().ToPhonemes("Kuk"));
    }

    /// <summary>
    /// A rewrite the file system might stamp within the same tick, made visible. The length and the
    /// write time are what the reload watches, and a same-length rewrite inside one file-time tick
    /// is the one edit that could be missed — so the tests that depend on seeing a change use this
    /// rather than trusting the clock.
    /// </summary>
    private void Touch(string json)
    {
        Write(json);
        System.IO.File.SetLastWriteTimeUtc(File, DateTime.UtcNow.AddSeconds(1));
    }
}
