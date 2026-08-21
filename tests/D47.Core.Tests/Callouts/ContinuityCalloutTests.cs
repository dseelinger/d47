using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The opening line of a session (list.md Phase 31, "Picking up where you left off").
/// <para>
/// The item the other three exist for, and the assertions divide the same way the item does: it says
/// something useful when there is something to say, and it says <b>nothing at all</b> otherwise. The
/// second half is the one worth guarding — a "welcome back" line with no memory behind it is exactly
/// the manufactured continuity the checklist rules out.
/// </para>
/// </summary>
public class ContinuityCalloutTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(3311, 4, 8, 19, 0, 0, TimeSpan.Zero);

    private const string Cmdr = "F1";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-continuity-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private MemoryBook Book()
    {
        var store = new MemoryStore(
            Path.Combine(_folder, "memories.json"), NullLogger<MemoryStore>.Instance);

        return new MemoryBook(store, () => Cmdr, () => MemorySituation.Unknown);
    }

    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"3311-04-08T18:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>
    /// Farseer unlocked at rank 1, which is what turns a plan into a <em>shortfall</em> rather than a
    /// gate: a grade the Commander's rank cannot reach is not a thing gathering fixes, so without
    /// this the plan reports nothing to be short of.
    /// </summary>
    private const string FarseerAtRankOne =
        """
        {"timestamp":"3311-04-08T18:01:00Z","event":"EngineerProgress",
         "Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":1}]}
        """;

    private ContinuityCallout Callout(MemoryBook book) =>
        new(book, TestSurface.EmptyChecklists(_folder));

    /// <summary>
    /// The same callout over a checklist that actually holds a plan, so the top of the list has
    /// something on it. Accepted rather than proposed: a proposal is not the Commander's list
    /// yet, and the line is about what they were doing.
    /// </summary>
    private ContinuityCallout WithAPlan(MemoryBook book, CommanderGameState state)
    {
        var checklists = TestSurface.EmptyChecklists(_folder, () => state);

        var scope = D47.Core.Checklists.ChecklistScope.Ship(12);

        var items = D47.Core.Checklists.EngineeringPlan.Items(scope, "krait_mkii",
            [new D47.Core.Checklists.BuildRequest("MainEngines", "Dirty Drive Tuning", 1, "Felicity Farseer")]);

        checklists.ProposePlan(scope, D47.Core.Checklists.ChecklistSource.EngineeringPlan, items, []);
        checklists.Accept();

        return new ContinuityCallout(book, checklists);
    }

    /// <summary>Where the Commander was, as the observer would have written it.</summary>
    private static void Seen(MemoryBook book, DateTimeOffset at) =>
        book.Observe(MemoryObserver.WhereKey, "you were last aboard in Deciat, docked at Farseer Inc.", at);

    private static CalloutContext At(DateTimeOffset now, CommanderGameState? state = null, bool priming = false) =>
        new(now, priming, state, GameStatus.Unknown, NavRoute.None, []);

    [Fact]
    public void AFirstRunSaysNothingAtAll()
    {
        var callout = Callout(Book());

        Assert.Null(callout.Compose(Now, State()));
    }

    [Fact]
    public void ComingBackAfterAWeekSaysHowLongAndWhereYouWere()
    {
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var line = Callout(book).Compose(Now, State())!;

        Assert.StartsWith("It has been 7 days.", line, StringComparison.Ordinal);
        Assert.Contains("You were last aboard in Deciat, docked at Farseer Inc.", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reconnecting after a crash is not a Commander coming back, and telling them it has been four
    /// minutes is a clock rather than continuity.
    /// </summary>
    [Fact]
    public void ComingBackAfterFourMinutesSaysNothing()
    {
        var book = Book();
        Seen(book, Now.AddMinutes(-4));

        Assert.Null(Callout(book).Compose(Now, State()));
    }

    [Fact]
    public void AMonthsGapIsSaidInWordsRatherThanInDays()
    {
        var book = Book();
        Seen(book, Now.AddDays(-32));

        Assert.Contains("It has been a month.", Callout(book).Compose(Now, State())!, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidWhileTheJournalBacklogIsStillBeingFolded()
    {
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var callout = Callout(book);

        // Priming is the replay of everything before this session. A line about picking up where
        // you left off, produced from the middle of that replay, would be about the wrong moment.
        Assert.Empty(callout.Examine(At(Now, State(), priming: true)));
    }

    [Fact]
    public void TheLineWaitsForTheSettleWindowAndThenIsSaidExactlyOnce()
    {
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var callout = Callout(book);

        // The first live tick only starts the clock: the backlog has just been folded and
        // Status.json may not have been read yet.
        Assert.Empty(callout.Examine(At(Now, State())));
        Assert.Empty(callout.Examine(At(Now.Add(callout.Settle / 2), State())));

        Assert.Single(callout.Examine(At(Now.Add(callout.Settle), State())));

        // And never again for the life of the process, whatever else happens.
        Assert.Empty(callout.Examine(At(Now.Add(callout.Settle).AddHours(3), State())));
    }

    [Fact]
    public void TheLineStandsDownForAnythingThatFiresOnAnEvent()
    {
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var callout = Callout(book);
        Assert.Empty(callout.Examine(At(Now, State())));

        var announcement = Assert.Single(callout.Examine(At(Now.Add(callout.Settle), State())));

        Assert.Equal(CalloutUrgency.Routine, announcement.Urgency);
        Assert.Equal(ContinuityCallout.Key, announcement.Key);
    }

    /// <summary>
    /// It is d47 talking, so it belongs on the conversation page as well as being heard — the
    /// correction remediation.md records for the Phase 8 family, inherited here rather than
    /// rediscovered.
    /// </summary>
    [Fact]
    public void TheLineReachesTheConversationPage()
    {
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var callout = Callout(book);
        Assert.Empty(callout.Examine(At(Now, State())));

        var announcement = Assert.Single(callout.Examine(At(Now.Add(callout.Settle), State())));

        Assert.Equal(announcement.Text, announcement.ConversationLine);
    }

    [Fact]
    public void ItIsSaidInCharacterWithTheFactsHandedOverAlreadyWritten()
    {
        var announcement = new Announcement(ContinuityCallout.Key, "It has been 7 days. Top of your list: buy limpets.");

        var brief = FlavourBriefs.For(announcement, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.True(brief.NeedsPersona);

        // No game state. The line is about what was true before this session, and handing over
        // where they are now is how a model comes to write about the wrong one.
        Assert.False(brief.NeedsGameState);

        Assert.Contains(announcement.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("add no facts of your own", brief.Instruction, StringComparison.Ordinal);
    }

    /// <summary>
    /// The checklist's half of the line since Phase 42: the top of the list, in the order the
    /// Commander cares about. A few items rather than the list — the page holds the rest, and
    /// this is a sentence somebody hears while they are still putting a headset on.
    /// </summary>
    [Fact]
    public void APlanPutsTheTopOfTheListInTheLine()
    {
        var state = State(FarseerAtRankOne);
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var line = WithAPlan(book, state).Compose(Now, state)!;

        Assert.Contains("Top of your list:", line, StringComparison.Ordinal);
        Assert.Contains("Dirty Drive Tuning", line, StringComparison.Ordinal);

        // Said whole, ship and all — there is no heading around a spoken sentence, and the hull
        // is what the plan stored for exactly this.
        Assert.Contains("Krait", line, StringComparison.Ordinal);
    }

    [Fact]
    public void APlanOnItsOwnIsWorthSayingEvenWithNothingRemembered()
    {
        // The clauses are independent. A Commander with a plan and an empty store still gets the
        // useful half rather than silence.
        var state = State(FarseerAtRankOne);

        Assert.Contains(
            "Top of your list:", WithAPlan(Book(), state).Compose(Now, state)!, StringComparison.Ordinal);
    }

    [Fact]
    public void PersonalityOffSaysItPlainly()
    {
        var announcement = new Announcement(ContinuityCallout.Key, "It has been 7 days.");

        Assert.Null(FlavourBriefs.For(announcement, personalityEnabled: false));
    }
    /// <summary>
    /// The line no longer holds an unlock opinion of its own (list.md Phase 42). Its
    /// Gap → Short → Here-or-Unlock precedence existed because the checklist had no order to
    /// read from; now it has one, an unlock is said only when an unlock item is genuinely near
    /// the top of the Commander's own list — through the list read, not through a clause.
    /// </summary>
    [Fact]
    public void TheLineReadsTheListRatherThanCountingUnlockSteps()
    {
        var book = Book();
        Seen(book, Now.AddDays(-7));

        var state = State(FarseerAtRankOne);

        var line = WithAPlan(book, state).Compose(Now, state)!;

        Assert.Contains("Top of your list:", line, StringComparison.Ordinal);

        // Whatever is on the list, the sentence never counts unlock steps at a Commander — that
        // was the "fact about engineers" the 2026-08-21 report asked to be rid of.
        Assert.DoesNotContain("unlock step away", line, StringComparison.Ordinal);
        Assert.DoesNotContain(" steps out", line, StringComparison.Ordinal);
    }

}
