using D47.Core.Adventures;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The list's own sentence, on real journals: fires at each place in order, once, and at nothing
/// before the stamp (Phase 47).
/// <para>
/// The fixture is five places one of the corpus Commanders actually went on 21–26 June 2026 — a
/// jump, a scan, a docking, a touchdown and a jump — read off the journals by a scan and written
/// here as the ids the journal carries. <b>Skipped, not failed, where the corpus is not on this
/// disk</b>: the folder is the Commander's own, and the test is about whether the fold behaves on
/// Elite's real output rather than on the fixtures above.
/// </para>
/// </summary>
public class AdventureCorpusTests
{
    private const string Commander = "F735466";

    private static readonly DateTimeOffset Accepted = new(2026, 6, 21, 21, 0, 0, TimeSpan.Zero);

    private static Adventure Fixture(DateTimeOffset acceptedAt) => new()
    {
        Key = "june-2026",
        Name = "June 2026",
        Beats =
        [
            Beat("HIP 9452", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 319698209115, System = "HIP 9452" }, "One."),
            Beat("The Scan", "catalyst", new AdventureTrigger { Kind = TriggerKind.Scan, SystemAddress = 1183162733242, BodyId = 2, System = "Arietis Sector HR-W c1-4", Body = "Arietis Sector HR-W c1-4 A" }, "Two."),
            Beat("Ore Terminal", "midpoint", new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 3221227776, System = "HIP 9452", Station = "Ore Terminal" }, "Three."),
            Beat("HR 3230 3 a a", "all is lost", new AdventureTrigger { Kind = TriggerKind.Land, SystemAddress = 182359951707, BodyId = 20, System = "HR 3230", Body = "HR 3230 3 a a" }, "Four."),
            Beat("Wregoe SH-X b42-0", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 675517965681, System = "Wregoe SH-X b42-0" }, "Five."),
        ],
        AcceptedAt = acceptedAt,
    };

    private static string? Corpus()
    {
        var folder = JournalFolder.DefaultPath();

        return Directory.Exists(folder)
               && Directory.EnumerateFiles(folder, JournalFolder.FilePattern).Any(file =>
                   string.CompareOrdinal(Path.GetFileName(file), "Journal.2026-06-21") >= 0
                   && string.CompareOrdinal(Path.GetFileName(file), "Journal.2026-06-27") < 0)
            ? folder
            : null;
    }

    private static AdventureBook Book(Adventure adventure)
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-adventure-corpus", Guid.NewGuid().ToString("N"));
        var store = new AdventureStore(Path.Combine(folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write(Commander, adventure);
        return book;
    }

    [Fact]
    public void TheCatchUpFiresEveryBeatInOrderOnTheRealJournals()
    {
        if (Corpus() is not { } folder)
        {
            return;
        }

        var book = Book(Fixture(Accepted));

        book.CatchUp(AdventureBook.FilesToWalk(folder, Accepted));

        var standing = book.Standing(Commander, "june-2026")!;

        Assert.True(standing.IsDone, standing.Describe(Accepted.AddDays(10)));

        // The scan beat fires at 21:29:26 and not at 21:29:39, and the thirteen seconds are #77
        // priced on the Commander's own journal. They dropped out of supercruise at the body at
        // :26 and read the nav beacon at :39; the widened trigger takes the arrival. It is the
        // same visit to the same body — what the widening buys is that a body already scanned,
        // which writes no second Scan at all, stops being a beat the story can never leave.
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 21, 21, 18, 55, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 21, 21, 29, 26, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 21, 21, 39, 23, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 23, 12, 34, 35, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 26, 13, 44, 7, TimeSpan.Zero),
            ],
            standing.Fired);
    }

    [Fact]
    public void NothingBeforeTheStampCountsOnTheRealJournals()
    {
        if (Corpus() is not { } folder)
        {
            return;
        }

        // Accepted after the whole sequence: the same five places, flown last week, count for nothing.
        var after = new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero);
        var book = Book(Fixture(after));

        book.CatchUp(AdventureBook.FilesToWalk(folder, after));

        var standing = book.Standing(Commander, "june-2026")!;

        Assert.Empty(standing.Fired);
        Assert.Equal("HIP 9452", standing.Place());
    }

    [Fact]
    public void TheWalkIsBoundedToTheFilesThatCanMatter()
    {
        if (Corpus() is not { } folder)
        {
            return;
        }

        var all = Directory.EnumerateFiles(folder, JournalFolder.FilePattern).Count();
        var walked = AdventureBook.FilesToWalk(folder, Accepted).Count;

        Assert.True(walked < all, $"{walked} of {all}");
        Assert.True(AdventureBook.FilesToWalk(folder, null).Count == 0);
    }
}
