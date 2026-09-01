using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Hear it wrong, ask, retry, remember
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
/// <para>
/// <b>The reported failure, 2026-08-27:</b> <i>"How far are we from Eurebia?"</i> answered with
/// <i>"I don't have a system called Eurebia on record, Commander. Could be a misspelling — worth
/// double-checking the name."</i> That is not a fixed string, it is the model narrating a bare
/// nothing politely — and a polite dead end is still a dead end. The Commander has to notice the
/// mishearing, work out the spelling, and say the whole question again.
/// </para>
/// <para>
/// <b>The correction is remembered against the word, not the answer</b>, which is the Commander's
/// own instruction and the part that carries the design: an alias held against the <em>system</em>
/// fixes one question, and one held against the <em>token</em> fixes every sentence that token ever
/// appears in.
/// </para>
/// </summary>
public class AMisheardNameAsksAndIsRememberedTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-heard").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private HeardNamesStore Store() =>
        new(Path.Combine(_root, "heard-names.json"), NullLogger<HeardNamesStore>.Instance);

    private static readonly DateTimeOffset At = new(2026, 8, 27, 20, 0, 0, TimeSpan.Zero);

    /// <summary>The Commander's own catalogue, as their journals would have built it.</summary>
    private static SpokenNames Visited(params string[] names) =>
        SpokenNames.Empty.With(names);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// <b>The reported case, end to end.</b> The Commander's journals hold <i>Eurybia</i> — they
    /// have been there — so what they said is recoverable from their own history, and the failing
    /// lookup asks instead of apologising.
    /// </summary>
    [Fact]
    public void AMisheardSystemIsOfferedBackFromWhereTheCommanderHasBeen()
    {
        var visited = Visited("Eurybia", "Shinrarta Dezhra", "Sol");

        var near = visited.Near("Eurebia");

        Assert.Equal("Eurybia", Assert.Single(near));

        var asked = MishearingWatch.Ask("system", "Eurebia", near, firstTime: true);

        Assert.Contains("Did you mean Eurybia?", asked, StringComparison.Ordinal);
        Assert.Contains("run it again", asked, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Edit distance is the wrong model for a transcriber, and this is the case that proves
    /// it.</b> <c>"Dessy at"</c> for <i>Deciat</i> is four edits and near in sound — the rung
    /// <c>Catalogue.Near</c> already had returns nothing at all, and the sound-alike rung returns
    /// the right answer. Measured over this Commander's real 15,216-name catalogue before it was
    /// built.
    /// </summary>
    [Fact]
    public void ASoundAlikeIsFoundWhereAnEditDistanceCannotReach()
    {
        var visited = Visited("Deciat", "Eurybia", "Jameson Memorial");

        Assert.Empty(Catalogue.Near(["Deciat", "Eurybia", "Jameson Memorial"], "Dessy at"));

        Assert.Equal("Deciat", Assert.Single(visited.Near("Dessy at")));
    }

    /// <summary>
    /// <b>The whole point of holding it against the token.</b> Teach it about <i>Eurebia</i> from
    /// one question, and a completely different sentence containing that word is put right too —
    /// which is the Commander's own example, in their own words.
    /// </summary>
    [Fact]
    public void ACorrectionLearnedFromOneQuestionFixesAnother()
    {
        var learned = SoundsLike.Empty.Learn("Eurebia", "Eurybia", At);

        Assert.Equal(
            "how far is Eurybia",
            learned.Apply("how far is Eurebia"));

        // The reported follow-on: a faction name that merely contains the word.
        Assert.Equal(
            "who runs the Eurybia Blue Mafia",
            learned.Apply("who runs the Eurebia Blue Mafia"));

        // And punctuation and case are left exactly as they were — only the matched word changes.
        Assert.Equal(
            "Eurybia, then. How far?",
            learned.Apply("Eurebia, then. How far?"));
    }

    /// <summary>
    /// <b>Whole words only.</b> Substring replacement would turn an alias into a rewrite of every
    /// word containing it, and a rewrite nobody can see is the failure this store exists to avoid.
    /// </summary>
    [Fact]
    public void OnlyAWholeWordIsRewritten()
    {
        var learned = SoundsLike.Empty.Learn("Sola", "Sol", At);

        Assert.Equal("the Solar system is Solaris", learned.Apply("the Solar system is Solaris"));
        Assert.Equal("Sol then", learned.Apply("Sola then"));
    }

    /// <summary>
    /// <b>Never alias a word that already means something</b>, which is the rule that keeps this
    /// safe: <c>Eurebia</c> is capturable precisely because it is not a word. A place the
    /// Commander has met, a phrase d47's own routing answers to, and anything too short to be an
    /// invention are all refused.
    /// </summary>
    [Theory]
    [InlineData("Eurebia", "Eurybia", true)]
    [InlineData("Deciat", "Eurybia", false)]      // a system they have been to
    [InlineData("cancel", "Eurybia", false)]      // a phrase the router answers to
    [InlineData("Sol", "Eurybia", false)]         // three letters: an English word hides there
    [InlineData("two words", "Eurybia", false)]   // a phrase is a rewrite rule, not an alias
    [InlineData("Eurybia", "Eurybia", false)]     // a self-alias does nothing, forever
    public void AWordThatAlreadyMeansSomethingIsNeverAliased(string heard, string meant, bool allowed)
    {
        var visited = Visited("Deciat", "Eurybia");

        Assert.Equal(
            allowed,
            SoundsLike.MayLearn(
                heard,
                meant,
                visited.Knows,
                word => string.Equals(word, "cancel", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// <b>Learned only on confirmation, and only from a name that actually resolved.</b> A near
    /// miss d47 offered is a guess; a lookup that came back with an answer is the Commander having
    /// steered it there.
    /// </summary>
    [Fact]
    public void TheCorrectionIsLearnedFromWhatResolvedRatherThanFromWhatWasOffered()
    {
        var watch = new MishearingWatch();

        Assert.True(watch.Rejected("Eurebia"));
        Assert.True(watch.Waiting);

        var learned = watch.Confirmed("Eurybia");

        Assert.Equal(("Eurebia", "Eurybia"), learned);
        Assert.False(watch.Waiting);

        // And nothing is outstanding afterwards, so the next successful lookup cannot pick up a
        // correction that belongs to an exchange that is over.
        Assert.Null(watch.Confirmed("Sol"));
    }

    /// <summary>
    /// <b>One retry, then it asks rather than looping.</b> A correction is itself spoken and can
    /// itself be misheard — two people who cannot hear each other repeat themselves indefinitely.
    /// </summary>
    [Fact]
    public void ASecondFailureAsksForTheLettersRatherThanOfferingAnotherList()
    {
        var watch = new MishearingWatch();

        Assert.True(watch.Rejected("Eurebia"));
        Assert.False(watch.Rejected("Yourebia"));

        // Nothing is held after the second one, so a later success cannot attach itself to it.
        Assert.False(watch.Waiting);
        Assert.Null(watch.Confirmed("Eurybia"));

        var asked = MishearingWatch.Ask("system", "Yourebia", ["Eurybia"], firstTime: false);

        Assert.Contains("Spell it out", asked, StringComparison.Ordinal);
        Assert.DoesNotContain("Did you mean", asked, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing is learned from anything another player wrote.</b> The catalogue reads named
    /// place fields rather than scraping the event, so a chat message naming a system does not put
    /// its sender, or its words, into the list d47 matches against.
    /// </summary>
    [Fact]
    public void NothingIsLearnedFromWhatAnotherPlayerWrote()
    {
        var names = SpokenNames.Empty
            .Apply(Event(
                """
                {"timestamp":"2026-08-27T20:00:00Z","event":"ReceiveText","From":"Cmdr Hostile",
                 "Message":"go to Deciat now","Channel":"player"}
                """))
            .Apply(Event(
                """
                {"timestamp":"2026-08-27T20:01:00Z","event":"FSDJump","StarSystem":"Eurybia",
                 "SystemFaction":{"Name":"Eurybia Blue Mafia"}}
                """));

        // The place Elite wrote, and the faction holding it.
        Assert.Contains("Eurybia", names.Names);
        Assert.Contains("Eurybia Blue Mafia", names.Names);

        // And nothing at all from the message: not the sender, not the words, not the system it
        // named — that one is somebody else's claim about where the Commander should go.
        Assert.DoesNotContain("Cmdr Hostile", names.Names);
        Assert.DoesNotContain("go to Deciat now", names.Names);
        Assert.DoesNotContain("Deciat", names.Names);
    }

    /// <summary>
    /// <b>Local, per Commander, readable and clearable.</b> Two Commanders share one journal
    /// folder and neither may be handed the other's corrections.
    /// </summary>
    [Fact]
    public void TheStoreIsPerCommanderAndCanBeReadAndCleared()
    {
        var store = Store();

        store.RememberNames(
            new Dictionary<string, SpokenNames>(StringComparer.Ordinal)
            {
                ["F1"] = Visited("Eurybia"),
                ["F2"] = Visited("Deciat"),
            },
            At);

        Assert.True(store.Learn("F1", "Eurebia", "Eurybia", At, _ => false));

        var reading = Store();
        reading.Load();

        Assert.Contains("\"Eurebia\" → Eurybia", reading.AliasesFor("F1").Summarise(), StringComparison.Ordinal);
        Assert.False(reading.AliasesFor("F2").IsKnown);

        // Neither was handed the other's names either.
        Assert.True(reading.NamesFor("F1").Knows("Eurybia"));
        Assert.False(reading.NamesFor("F1").Knows("Deciat"));

        reading.ForgetCorrections("F1", At);

        var afterwards = Store();
        afterwards.Load();

        Assert.False(afterwards.AliasesFor("F1").IsKnown);

        // Clearing the corrections leaves the names alone: those are not a claim about anything.
        Assert.True(afterwards.NamesFor("F1").Knows("Eurybia"));
    }

    /// <summary>
    /// <b>The store refuses what the rules refuse</b>, rather than trusting the caller to have
    /// asked. A word the Commander has met is not a mishearing, whoever hands it over.
    /// </summary>
    [Fact]
    public void TheStoreItselfRefusesToAliasAKnownName()
    {
        var store = Store();

        store.RememberNames(
            new Dictionary<string, SpokenNames>(StringComparer.Ordinal) { ["F1"] = Visited("Deciat") },
            At);

        Assert.False(store.Learn("F1", "Deciat", "Eurybia", At, _ => false));
        Assert.False(store.AliasesFor("F1").IsKnown);
    }

    /// <summary>
    /// The catalogue is mined from the Commander's own journals, and it is what makes the
    /// recovery deep enough to be worth having on the first run.
    /// </summary>
    [Fact]
    public void TheCatalogueIsMinedFromTheCommandersOwnJournals()
    {
        var journal = Path.Combine(_root, "Journal.2026-08-27T190000.01.log");

        File.WriteAllLines(journal,
        [
            """{"timestamp":"2026-08-27T19:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-27T19:05:00Z","event":"FSDJump","StarSystem":"Eurybia","SystemFaction":{"Name":"Eurybia Blue Mafia"}}""",
            """{"timestamp":"2026-08-27T19:20:00Z","event":"Docked","StarSystem":"Eurybia","StationName":"Ray Gateway"}""",
        ]);

        var mined = SpokenNameMiner.FromHistory([journal], NullLogger.Instance);

        var names = mined["F1"];

        Assert.True(names.Knows("Eurybia"));
        Assert.True(names.Knows("Ray Gateway"));
        Assert.True(names.Knows("Eurybia Blue Mafia"));

        // And the mishearing that started all this is recoverable from it.
        Assert.Equal("Eurybia", names.Near("Eurebia").First());
    }
}
