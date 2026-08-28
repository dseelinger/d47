using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Lore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Remarking on a system worth remarking on (Phase 23).
/// <para>
/// Most of this is about the 24-hour rule, because that is the whole difference between a
/// companion and a tour guide who has forgotten meeting you — and because it is the part that
/// only works if the stamps are absolute. Every time here is stated rather than slept through,
/// which is exactly what makes the rule testable at all.
/// </para>
/// </summary>
public class LoreCalloutTests : IDisposable
{
    /// <summary>Sol, whose address is a shipped row and is Frontier's own number for it.</summary>
    private const long Sol = 10477373803;

    private const long Nowhere = 1234567890123;

    private static readonly DateTimeOffset Start = new(3307, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-lore-tests", Guid.NewGuid().ToString("N"));

    private LoreStore Store() =>
        new(Path.Combine(_folder, "lore.json"), NullLogger<LoreStore>.Instance);

    private LoreVisits Visits() =>
        new(Path.Combine(_folder, "lore-visits.json"), NullLogger<LoreVisits>.Instance);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Jump(long address, string name = "Sol", string kind = "FSDJump") =>
        $$"""
          {"timestamp":"3307-05-01T12:00:00Z","event":"{{kind}}","StarSystem":"{{name}}","SystemAddress":{{address}}}
          """;

    private static CalloutContext At(DateTimeOffset now, bool priming, params string[] events) =>
        new(now, priming, State: null, GameStatus.Unknown, NavRoute.None, [.. events.Select(Parse)]);

    private static string[] Spoken(LoreCallout callout, CalloutContext context) =>
        [.. callout.Examine(context).Select(announcement => announcement.Text)];

    [Fact]
    public void ArrivingInASystemTheTableKnowsSaysWhatItKnows()
    {
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        var said = Spoken(callout, At(Start, priming: false, Jump(Sol)));

        Assert.Single(said);
        Assert.Equal(LoreDirectory.ByAddress(Sol)!.Note, said[0]);
    }

    [Fact]
    public void ArrivingAnywhereElseSaysNothing()
    {
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        Assert.Empty(Spoken(callout, At(Start, priming: false, Jump(Nowhere, "Col 285 Sector AB-C d1-23"))));
    }

    [Fact]
    public void TheSameSystemIsNotRemarkedOnTwiceInsideADay()
    {
        var visits = Visits();
        var callout = new LoreCallout(new LoreBook(Store()), visits);

        Assert.Single(Spoken(callout, At(Start, priming: false, Jump(Sol))));

        // Twenty-three hours later, and still the same day's worth of silence. This is the number
        // the corpus argued for: 30.1% of all jumps re-enter a system visited inside a day.
        Assert.Empty(Spoken(callout, At(Start.AddHours(23), priming: false, Jump(Sol))));
    }

    [Fact]
    public void ADayLaterItIsWorthSayingAgain()
    {
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        Assert.Single(Spoken(callout, At(Start, priming: false, Jump(Sol))));
        Assert.Single(Spoken(callout, At(Start.AddHours(24), priming: false, Jump(Sol))));
    }

    [Fact]
    public void TheQuietPeriodSurvivesARestart()
    {
        // The whole reason the stamps are on disk. Logging off in a system and coming back an
        // hour later should be quiet, and nothing in a fresh process remembers otherwise.
        var visits = Visits();
        var first = new LoreCallout(new LoreBook(Store()), visits);

        Assert.Single(Spoken(first, At(Start, priming: false, Jump(Sol))));
        visits.Save();

        var reopened = Visits();
        reopened.Load();

        var second = new LoreCallout(new LoreBook(Store()), reopened);

        Assert.Empty(Spoken(second, At(Start.AddHours(1), priming: false, Jump(Sol))));
    }

    [Fact]
    public void TheStampsAreAbsoluteRatherThanElapsed()
    {
        // What the replay harness rests on. A stamp written as "so long ago" would make the
        // window mean fourteen minutes at 100x; an instant compared against the tick's own time
        // means the same thing at any speed.
        var visits = Visits();
        visits.Record(Sol, Start);
        visits.Save();

        var reopened = Visits();
        reopened.Load();

        Assert.Equal(Start, reopened.LastSpokenAt(Sol));
    }

    [Fact]
    public void ABacklogAtStartupAnnouncesNothing()
    {
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        Assert.Empty(Spoken(callout, At(Start, priming: true, Jump(Sol))));
    }

    [Fact]
    public void PrimingDoesNotUseUpTheDaysOneRemark()
    {
        // A folded backlog must not leave the system marked as spoken about, or starting d47
        // after an hour of flying would silence the next arrival for a day.
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        Spoken(callout, At(Start, priming: true, Jump(Sol)));

        Assert.Single(Spoken(callout, At(Start.AddMinutes(1), priming: false, Jump(Sol))));
    }

    [Fact]
    public void ACarrierJumpCountsAsArriving()
    {
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        Assert.Single(Spoken(callout, At(Start, priming: false, Jump(Sol, kind: "CarrierJump"))));
    }

    [Fact]
    public void SwitchedOffItSaysNothingAndForgetsNothing()
    {
        var callout = new LoreCallout(new LoreBook(Store()), Visits()) { Remarks = () => LoreRemarks.Off };

        Assert.Empty(Spoken(callout, At(Start, priming: false, Jump(Sol))));

        // And switching it back on does not find the system already used up — a remark that never
        // happened must not have started the clock.
        callout.Remarks = () => LoreRemarks.Remark;

        Assert.Single(Spoken(callout, At(Start.AddMinutes(1), priming: false, Jump(Sol))));
    }

    [Fact]
    public void TheCommandersOwnNoteIsSpokenAsTheirs()
    {
        var book = new LoreBook(Store());
        book.Add(Nowhere, "Kremainn", "The ring here is worth mining.", LoreArrival.Panel, Start);

        var callout = new LoreCallout(book, Visits());

        var said = Spoken(callout, At(Start, priming: false, Jump(Nowhere, "Kremainn")));

        Assert.Equal("You told me: The ring here is worth mining.", Assert.Single(said));
    }

    [Fact]
    public void AShippedFactComesBeforeTheCommandersOwnNoteAboutTheSameSystem()
    {
        var book = new LoreBook(Store());
        book.Add(Sol, "Sol", "Docking permission takes a while here.", LoreArrival.Panel, Start);

        var callout = new LoreCallout(book, Visits());

        var said = Assert.Single(Spoken(callout, At(Start, priming: false, Jump(Sol))));

        Assert.StartsWith(LoreDirectory.ByAddress(Sol)!.Note, said, StringComparison.Ordinal);
        Assert.EndsWith("You told me: Docking permission takes a while here.", said, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAddressTravelsOnTheKeySoABatchOfTwoIsNotOneAnswerTwice()
    {
        var book = new LoreBook(Store());
        book.Add(Nowhere, "Kremainn", "Mining.", LoreArrival.Panel, Start);

        var callout = new LoreCallout(book, Visits());

        var keys = callout
            .Examine(At(Start, priming: false, Jump(Sol), Jump(Nowhere, "Kremainn")))
            .Select(announcement => LoreCallout.AddressOf(announcement.Key))
            .ToArray();

        Assert.Equal([Sol, Nowhere], keys);
    }

    [Fact]
    public void ARemarkNeverInterruptsAnything()
    {
        // It is news about a place that will still be there in a minute. A crash site spoken over
        // the top of an interdiction warning would be the worst possible trade.
        var callout = new LoreCallout(new LoreBook(Store()), Visits());

        var announcement = Assert.Single(callout.Examine(At(Start, priming: false, Jump(Sol))));

        Assert.Equal(CalloutUrgency.Routine, announcement.Urgency);
    }
}
