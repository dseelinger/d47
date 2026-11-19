using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>The local voices say the name Canonn as "cannon", with or without a pronunciations file.</summary>
public class CanonnIsSaidAsCannonTests
{
    private const string Pronunciations = "C:/d47-test/canonn/pronunciations.json";

    /// <summary>The shipped dictionary's entry for "cannon"; it has none for "canonn".</summary>
    private sealed class Dictionary : IPronunciationDictionary
    {
        public string? Lookup(string word) =>
            string.Equals(word, "cannon", StringComparison.OrdinalIgnoreCase) ? "kˈænən" : null;
    }

    [Fact]
    public void KokoroSaysCanonnAsCannonWithNoCorrections()
    {
        var phonemiser = new Phonemiser(new Dictionary(), new PronunciationOverrides(new MemoryFileSystem(), Pronunciations));

        Assert.Equal("kˈænən", phonemiser.ToPhonemes("Canonn"));
    }

    [Fact]
    public void KokoroSaysCanonnAsCannonBesideTheCommandersOwnEntries()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Pronunciations, """{ "Deciat": "ipa:ˈdɛsiæt" }""");

        var phonemiser = new Phonemiser(new Dictionary(), new PronunciationOverrides(files, Pronunciations));

        Assert.Equal("kˈænən.", phonemiser.ToPhonemes("Canonn."));
    }

    [Fact]
    public void TheCommandersOwnEntryForCanonnWins()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Pronunciations, """{ "Canonn": "ipa:kəˈnɒn" }""");

        var phonemiser = new Phonemiser(new Dictionary(), new PronunciationOverrides(files, Pronunciations));

        Assert.Equal("kəˈnɒn", phonemiser.ToPhonemes("Canonn"));
    }

    [Fact]
    public void ChatterboxIsGivenTheRespelling()
    {
        var overrides = new PronunciationOverrides(new MemoryFileSystem(), Pronunciations);
        overrides.Refresh();

        Assert.Equal("Sent to cannon.", Respelling.Apply("Sent to Canonn.", overrides, _ => { }));
    }
}
