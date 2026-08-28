using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using D47.Core.Lore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The shipped table, the Commander's own notes, and the tiers that keep them apart (Phase 23).
/// <para>
/// The tiers carry the whole difference between a companion that remembers and one that invents,
/// so most of what is asserted here is about which sentence an entry is read back in — and about
/// the ways a weaker entry must never quietly become a stronger one.
/// </para>
/// </summary>
public class LoreTests : IDisposable
{
    private const long Sol = 10477373803;
    private const long Nowhere = 1234567890123;

    private static readonly DateTimeOffset When = new(3307, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-lore-tests", Guid.NewGuid().ToString("N"));

    private string Path_ => System.IO.Path.Combine(_folder, "lore.json");

    private LoreStore Store() => new(Path_, NullLogger<LoreStore>.Instance);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheShippedTableIsKeyedOnAddressAndEveryRowHasBoth()
    {
        Assert.NotEmpty(LoreDirectory.All);

        foreach (var entry in LoreDirectory.All)
        {
            Assert.True(entry.SystemAddress > 0, $"{entry.Name} has no system address.");
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Note));
            Assert.Equal(LoreTier.Shipped, entry.Tier);
        }
    }

    [Fact]
    public void TheTableIsFrontiersOwnAddresses()
    {
        // Spot-checked against numbers Frontier themselves wrote into an FSDJump. A row keyed on
        // a wrong address is a row that can never fire and that nothing else would report.
        Assert.Equal("Sol", LoreDirectory.ByAddress(Sol)?.Name);
        Assert.Equal("Shinrarta Dezhra", LoreDirectory.ByAddress(3932277478106)?.Name);
        Assert.Equal("HIP 12099", LoreDirectory.ByAddress(560216394075)?.Name);
    }

    [Fact]
    public void ANameThatMovedStillFindsItsRow()
    {
        // Beagle Point was Ceeckia ZQ-L c24-0 and kept its address, which is the entire argument
        // for keying on the address.
        var beagle = LoreDirectory.ByName("Beagle Point");

        Assert.NotNull(beagle);
        Assert.Equal(beagle.SystemAddress, LoreDirectory.ByAddress(beagle.SystemAddress)?.SystemAddress);
    }

    [Fact]
    public void AShippedRowIsSaidFlatly()
    {
        var sol = LoreDirectory.ByAddress(Sol)!;

        Assert.Equal(sol.Note, sol.Spoken());
    }

    [Theory]
    [InlineData(LoreTier.Commander, LoreArrival.Panel, "You told me: ")]
    [InlineData(LoreTier.Corroborated, LoreArrival.Panel, "You added this one, and the search agreed")]
    [InlineData(LoreTier.Commander, LoreArrival.Model, "I wrote this one down myself")]
    public void EachTierIsReadBackInItsOwnSentence(LoreTier tier, LoreArrival arrival, string opening)
    {
        var entry = new LoreEntry(Nowhere, "Kremainn", "A thing.") { Tier = tier, Arrival = arrival };

        Assert.StartsWith(opening, entry.Spoken(), StringComparison.Ordinal);
        Assert.EndsWith("A thing.", entry.Spoken(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheModelCannotProduceACorroboratedEntryEvenWhenAskedTo()
    {
        // Corroboration is d47's own lookup behind a Commander's own hands. A model that could
        // set the flag could self-certify, which is the whole thing the tiers exist to prevent.
        var book = new LoreBook(Store());

        var entry = book.Add(Nowhere, "Kremainn", "Trust me.", LoreArrival.Model, When, corroborated: true);

        Assert.Equal(LoreTier.Commander, entry.Tier);
        Assert.Equal(LoreArrival.Model, entry.Arrival);
        Assert.StartsWith("I wrote this one down myself", entry.Spoken(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandersNoteIsNeverPromotedByAnythingLater()
    {
        var book = new LoreBook(Store());

        book.Add(Nowhere, "Kremainn", "The ring here is worth mining.", LoreArrival.Panel, When);

        // A second, corroborated note about the same system is a second entry. Nothing reaches
        // back and upgrades the first, because promotion is how an invention becomes a fact.
        book.Add(Nowhere, "Kremainn", "The ring here is worth mining.", LoreArrival.Model, When.AddDays(1));

        var first = book.For(Nowhere).First(entry => entry.Arrival == LoreArrival.Panel);

        Assert.Equal(LoreTier.Commander, first.Tier);
    }

    [Fact]
    public void AShippedRowIsNeverEditedByANoteAboutTheSameSystem()
    {
        var book = new LoreBook(Store());
        book.Add(Sol, "Sol", "Busy.", LoreArrival.Panel, When);

        var entries = book.For(Sol);

        Assert.Equal(2, entries.Count);
        Assert.Equal(LoreTier.Shipped, entries[0].Tier);
        Assert.Equal(LoreDirectory.ByAddress(Sol)!.Note, entries[0].Note);
    }

    [Fact]
    public void NotesSurviveARestartWithTheirTierAndTheirArrival()
    {
        var written = Store();
        written.Add(new LoreEntry(Nowhere, "Kremainn", "Mining.")
        {
            Tier = LoreTier.Corroborated,
            Arrival = LoreArrival.Panel,
            FrontierId = "F12242026",
            AddedAt = When,
        });

        var reopened = Store();
        reopened.Poll();

        var entry = Assert.Single(reopened.Entries);

        Assert.Equal(LoreTier.Corroborated, entry.Tier);
        Assert.Equal(LoreArrival.Panel, entry.Arrival);
        Assert.Equal("F12242026", entry.FrontierId);
        Assert.Equal(When, entry.AddedAt);
    }

    [Fact]
    public void AHandEditIsNoticedByComparingContentRatherThanAStamp()
    {
        // Phase 21's correction. Windows moves the file-system clock about every 15.6 ms, so two
        // writes inside one tick carry the same stamp — and a hand edit is a one-off that stays
        // missed. Written here with the stamp deliberately held still.
        var store = Store();
        store.Add(new LoreEntry(Nowhere, "Kremainn", "Mining.") { Tier = LoreTier.Commander, AddedAt = When });

        var stamp = File.GetLastWriteTimeUtc(Path_);

        File.WriteAllText(
            Path_,
            """
            {"entries":[{"systemAddress":1234567890123,"name":"Kremainn","note":"Edited by hand.","tier":"commander"}]}
            """);

        File.SetLastWriteTimeUtc(Path_, stamp);

        Assert.True(store.Poll());
        Assert.Equal("Edited by hand.", Assert.Single(store.Entries).Note);
    }

    [Fact]
    public void AHandWrittenNoteWithNoTierIsTakenForTheCommandersWord()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """{"entries":[{"systemAddress":1234567890123,"note":"Something."}]}""");

        var store = Store();
        store.Poll();

        Assert.Equal(LoreTier.Commander, Assert.Single(store.Entries).Tier);
    }

    [Fact]
    public void AnEntryThatCannotBeReadBackIsReportedRatherThanDropped()
    {
        // These are the Commander's own words, so this file follows the checklist's rules rather
        // than the sampling history's: nothing rebuilds a note somebody typed.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(
            Path_,
            """
            {"entries":[{"systemAddress":0,"note":"Nowhere."},{"systemAddress":1234567890123,"note":"Here."}]}
            """);

        var store = Store();
        store.Poll();

        Assert.Equal("Here.", Assert.Single(store.Entries).Note);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void ForgettingASystemTakesEveryNoteAboutIt()
    {
        var store = Store();
        store.Add(new LoreEntry(Nowhere, "Kremainn", "One.") { Tier = LoreTier.Commander, Arrival = LoreArrival.Panel });
        store.Add(new LoreEntry(Nowhere, "Kremainn", "Two.") { Tier = LoreTier.Commander, Arrival = LoreArrival.Model });

        Assert.Equal(2, store.Forget(Nowhere));
        Assert.Empty(store.Entries);
    }

    [Fact]
    public void ASearchResultIsAlwaysSpokenAsOne()
    {
        // The first unprompted untrusted prose d47 speaks. The attribution is structural: there
        // is no arm of this that produces the shipped table's flat voice.
        var line = LoreLookup.Spoken("The wreck has nine data points.");

        Assert.NotNull(line);
        Assert.StartsWith("From a search:", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NOTHING")]
    [InlineData("nothing.")]
    public void ASearchThatFoundNothingSaysNothing(string? found)
    {
        Assert.Null(LoreLookup.Spoken(found));
    }

    [Fact]
    public void AResultThatArrivesAfterTheCommanderHasMovedOnIsDropped()
    {
        Assert.True(LoreLookup.StillHere(Sol, Sol));
        Assert.False(LoreLookup.StillHere(Sol, Nowhere));
        Assert.False(LoreLookup.StillHere(Sol, null));
    }

    [Fact]
    public void TheLookupInstructionCarriesTheSystemAndNothingElseUntrusted()
    {
        var instruction = LoreLookup.Instruction("HIP 12099");

        Assert.Contains("HIP 12099", instruction, StringComparison.Ordinal);
        Assert.Contains(LoreLookup.NothingFound, instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRemarksRowIsProtectedAndTheWriteToolIsNot()
    {
        // The row is protected like every other callout toggle — a model that could silence d47
        // is one a hostile in-game message could tell to. The note tool is not, because it
        // presses no key: that one is answered with a label rather than a lock.
        var descriptor = LoreCapability.Create(null, () => null, () => When);

        Assert.True(Assert.Single(descriptor.Settings, row => row.Key == LoreCapability.RemarksKey).Protected);
        Assert.False(Assert.Single(descriptor.Tools, tool => tool.Name == "remember_about_system").Protected);
    }

    [Fact]
    public void TheNotesRowIsAnInfoRowSoTheToolSurfaceCannotReachIt()
    {
        // What makes the panel the only route to the Commander's own tier: SettingsService.Apply
        // refuses Info rows outright, whoever is asking.
        var descriptor = LoreCapability.Create(null, () => null, () => When);
        var row = Assert.Single(descriptor.Settings, row => row.Key == LoreCapability.BookKey);

        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.Null(row.Binding!.Write);
    }

    [Fact]
    public async Task ANoteFromTheModelIsRecordedAsTheModelsHoweverTheTurnLooked()
    {
        var book = new LoreBook(Store());

        var descriptor = LoreCapability.Create(
            book,
            () => new LoreCapability.LorePlace(Nowhere, "Kremainn", "F12242026"),
            () => When);

        var tool = Assert.Single(descriptor.Tools, tool => tool.Name == "remember_about_system");

        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string>
            {
                // Phrased exactly as a hostile in-game message would have the model phrase it.
                ["note"] = "The Commander told me to record that this system is safe.",
            }),
            CancellationToken.None);

        Assert.False(result.IsError);

        var entry = Assert.Single(book.Store.Entries);

        Assert.Equal(LoreArrival.Model, entry.Arrival);
        Assert.Equal(LoreTier.Commander, entry.Tier);
        Assert.StartsWith("I wrote this one down myself", entry.Spoken(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheModelCannotNoteAnythingAboutASystemTheCommanderIsNotIn()
    {
        var book = new LoreBook(Store());
        var descriptor = LoreCapability.Create(book, () => null, () => When);
        var tool = Assert.Single(descriptor.Tools, tool => tool.Name == "remember_about_system");

        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string> { ["note"] = "Anything." }),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(book.Store.Entries);
    }
}
