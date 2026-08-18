using D47.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Memory;

/// <summary>
/// The store that survives (list.md Phase 31, "A memory is written down, never inferred into
/// being").
/// <para>
/// Most of this is about labels rather than about storage, because the labels are the phase. A file
/// that keeps sentences is easy; a file that cannot lose track of <em>who said each one</em> is the
/// thing worth asserting, and it is the thing a later edit could quietly break.
/// </para>
/// </summary>
public class MemoryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private const string Cmdr = "F1234567";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-memory-tests", Guid.NewGuid().ToString("N"));

    private string StorePath => Path.Combine(_folder, "memories.json");

    private MemoryStore Store() => new(StorePath, NullLogger<MemoryStore>.Instance);

    private MemoryBook Book(MemoryStore? store = null, MemorySituation? situation = null) =>
        new(store ?? Store(), () => Cmdr, () => situation ?? MemorySituation.Unknown);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void Write(string json)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, json);
    }

    [Fact]
    public void AFactTypedOnThePanelSurvivesARestartAndIsStillTheCommandersWord()
    {
        Book().Remember("I fly a Krait Phantom for exploration.", MemoryArrival.Panel, Now);

        // A second store over the same file is a restart. This is the whole reason the store
        // exists: before it, d47's recall was a field in AppHost.
        var reopened = Store();
        reopened.Poll();

        var entry = Assert.Single(reopened.For(Cmdr));

        Assert.Equal(MemoryTier.Stated, entry.Tier);
        Assert.Equal(MemoryArrival.Panel, entry.Arrival);
        Assert.StartsWith("You told me:", entry.Spoken(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The security property of the phase, and the reason <c>remember_about_me</c> has no tier
    /// parameter: a model that could file its own entry as the Commander's word is a model a
    /// hostile in-game message could tell to.
    /// </summary>
    [Fact]
    public void AFactTheModelWroteIsAnInferenceWhateverTheTurnLookedLike()
    {
        var entry = Book().Remember("The Commander told me they hate mining.", MemoryArrival.Model, Now);

        Assert.Equal(MemoryTier.Inferred, entry.Tier);
        Assert.Equal(MemoryArrival.Model, entry.Arrival);

        // Never "you told me", however confidently the sentence itself claims it.
        Assert.DoesNotContain("You told me", entry.Spoken(), StringComparison.Ordinal);
        Assert.Contains("nothing has checked it", entry.Spoken(), StringComparison.Ordinal);
    }

    [Fact]
    public void NothingPromotesAnInferenceToTheCommandersWord()
    {
        var store = Store();
        var book = Book(store);

        var inferred = book.Remember("They prefer selling data at Jameson.", MemoryArrival.Model, Now);

        // Said again, by the same route, a week later. Two arrivals of the same claim is exactly
        // the shape of "a later turn appeared to confirm it", and it changes nothing.
        book.Remember("They prefer selling data at Jameson.", MemoryArrival.Model, Now.AddDays(7));

        Assert.All(store.For(Cmdr), entry => Assert.Equal(MemoryTier.Inferred, entry.Tier));
        Assert.Equal(MemoryTier.Inferred, inferred.Tier);
    }

    [Fact]
    public void TwoCommandersDoNotSeeEachOthersFacts()
    {
        var store = Store();

        store.Write("F1", new MemoryEntry("told-1", "Mine.") { Tier = MemoryTier.Stated });
        store.Write("F2", new MemoryEntry("told-1", "Theirs.") { Tier = MemoryTier.Stated });

        Assert.Equal("Mine.", Assert.Single(store.For("F1")).Fact);
        Assert.Equal("Theirs.", Assert.Single(store.For("F2")).Fact);

        // And the whole file is still readable as one thing, because that is what the panel shows
        // and what emptying covers.
        Assert.Equal(2, store.Everything().Count);
    }

    [Fact]
    public void AHandEditIsPickedUpWithoutARestartAndAnUnlabelledLineIsTheCommandersWord()
    {
        var store = Store();
        store.Poll();

        Write("""
              {"commanders":[{"frontierId":"F1234567","memories":[
                {"key":"told-9","fact":"I always run with a fuel scoop."}
              ]}]}
              """);

        Assert.True(store.Poll());

        var entry = Assert.Single(store.For(Cmdr));

        Assert.Equal("told-9", entry.Key);
        Assert.Equal(MemoryTier.Stated, entry.Tier);
    }

    [Fact]
    public void ALineThatCannotBeReadIsReportedAndTheRestStillLoads()
    {
        var store = Store();

        Write("""
              {"commanders":[{"frontierId":"F1234567","memories":[
                {"key":"told-1","fact":"   "},
                {"key":"told-2","fact":"This one is fine."}
              ]}]}
              """);

        store.Poll();

        Assert.Equal("This one is fine.", Assert.Single(store.For(Cmdr)).Fact);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void TwoEntriesSharingAKeyAreNotSilentlyMerged()
    {
        var store = Store();

        Write("""
              {"commanders":[{"frontierId":"F1234567","memories":[
                {"key":"told-1","fact":"First."},
                {"key":"told-1","fact":"Second."}
              ]}]}
              """);

        store.Poll();

        Assert.Equal("First.", Assert.Single(store.For(Cmdr)).Fact);
        Assert.Contains("share this key", Assert.Single(store.Problems).Why, StringComparison.Ordinal);
    }

    [Fact]
    public void AFactTooLongToSayIsTruncatedRatherThanRefused()
    {
        var entry = Book().Remember(new string('x', MemoryBook.MaxFact + 200), MemoryArrival.Model, Now);

        Assert.True(entry.Fact.Length <= MemoryBook.MaxFact + 1);
        Assert.EndsWith("…", entry.Fact, StringComparison.Ordinal);
    }

    [Fact]
    public void AFactIsTaggedWithWhatWasHappeningWhenItArrived()
    {
        var situation = new MemorySituation("Deciat", "Krait_MkII", D47.Core.Callouts.AmbientSituation.Docked);

        var entry = Book(situation: situation).Remember("Farseer is the one I use.", MemoryArrival.Panel, Now);

        Assert.Equal(["system:deciat", "ship:krait_mkii", "doing:docked"], entry.About);
    }

    [Fact]
    public void ExpiryRemovesWhatIsPastItAndSaysWhatWent()
    {
        var store = Store();
        var book = Book(store);

        book.Remember("Old news.", MemoryArrival.Panel, Now.AddDays(-200));
        book.Remember("Still current.", MemoryArrival.Panel, Now.AddDays(-10));

        var gone = book.Expire(Now, TimeSpan.FromDays(90));

        Assert.Equal("Old news.", Assert.Single(gone).Fact);
        Assert.Equal("Still current.", Assert.Single(store.For(Cmdr)).Fact);
    }

    [Fact]
    public void NeverIsARealSettingAndRemovesNothing()
    {
        var store = Store();
        var book = Book(store);

        book.Remember("From years ago.", MemoryArrival.Panel, Now.AddYears(-3));

        Assert.Empty(book.Expire(Now, TimeSpan.Zero));
        Assert.Single(store.For(Cmdr));
    }

    /// <summary>
    /// A hand-written line is the likeliest thing to have no stamp on it, and deleting the
    /// Commander's own words because they did not type a date is the worst reading of an ambiguous
    /// file.
    /// </summary>
    [Fact]
    public void AnEntryWithNoStampIsNeverExpired()
    {
        var store = Store();

        Write("""
              {"commanders":[{"frontierId":"F1234567","memories":[
                {"key":"told-1","fact":"No date on this one."}
              ]}]}
              """);

        store.Poll();

        Assert.Empty(store.Expire(Now, TimeSpan.FromDays(1)));
        Assert.Single(store.For(Cmdr));
    }

    [Fact]
    public void EmptyingCoversEveryCommanderInTheFile()
    {
        var store = Store();

        store.Write("F1", new MemoryEntry("told-1", "Mine.") { Tier = MemoryTier.Stated });
        store.Write("F2", new MemoryEntry("told-1", "The other character's.") { Tier = MemoryTier.Stated });

        // "Forget me" is not "forget the character I happen to be logged in as". A per-character
        // erase that left three others on disk would be a privacy control that reads as one and
        // is not.
        Assert.Equal(2, store.Empty());
        Assert.Empty(store.Everything());

        var reopened = Store();
        reopened.Poll();
        Assert.Empty(reopened.Everything());
    }

    [Fact]
    public void ForgettingOneEntryLeavesTheRest()
    {
        var store = Store();
        var book = Book(store);

        var first = book.Remember("Keep.", MemoryArrival.Panel, Now);
        var second = book.Remember("Drop.", MemoryArrival.Panel, Now);

        Assert.True(book.Forget(second.Key));
        Assert.False(book.Forget("nothing-like-this"));
        Assert.Equal(first.Key, Assert.Single(store.For(Cmdr)).Key);
    }

    [Fact]
    public void KeysAreLowestUnusedRatherThanRandom()
    {
        // Core owns no randomness and reads no clock, so a key has to be a function of what is
        // already there — the same rule authored checklist lines and ship build ids follow.
        var book = Book();

        Assert.Equal("told-1", book.Remember("One.", MemoryArrival.Panel, Now).Key);
        Assert.Equal("told-2", book.Remember("Two.", MemoryArrival.Panel, Now).Key);
        Assert.Equal("noted-1", book.Remember("Three.", MemoryArrival.Model, Now).Key);
    }
}
