using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The checklist in the order the Commander cares about (list.md Phase 42).
/// <para>
/// Two levels, and the tests divide the same way: between projects the order is the Commander's
/// and stored, within one it is derived on every read — what can be done now, in the ship being
/// flown, where they are standing — and nothing below the rank is ever written down.
/// </para>
/// </summary>
public class ChecklistOrderingTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-ordering-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>Flying the Krait as ship 12, standing in Sol.</summary>
    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"3311-04-08T18:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"3311-04-08T18:00:01Z","event":"Loadout","Ship":"krait_mkii","ShipID":12,"ShipName":"Nightjar","Modules":[]}""",
                     """{"timestamp":"3311-04-08T18:00:02Z","event":"Location","StarSystem":"Sol","Docked":false}""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistDocument Notes(params (ChecklistScope Scope, string Text)[] lines)
    {
        var document = ChecklistDocument.For("F1", "Jameson");

        foreach (var (scope, text) in lines)
        {
            document = document.AddNote(scope, text).Document;
        }

        return document;
    }

    private static ChecklistItem Derived(
        ChecklistScope scope, string key, string text, ChecklistState state) => new()
    {
        Key = key,
        Scope = scope,
        Kind = ChecklistItemKind.Derived,
        Text = text,
        Source = ChecklistSource.EngineeringPlan,
        Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Dirty Drive Tuning",
            Grade = 5,
        },
        State = state,
    };

    /// <summary>
    /// The complaint the phase answers: 600 lines of chronological sediment, where the ship being
    /// sat in was buried under whatever week its plan happened to arrive in.
    /// </summary>
    [Fact]
    public void TheShipBeingFlownFloatsAboveTheSediment()
    {
        var document = Notes(
            (ChecklistScope.Universal, "ask Jim about the Krait build"),
            (ChecklistScope.System("Lave"), "look at the market"),
            (ChecklistScope.Ship(12), "fix the thrusters"));

        var arranged = ChecklistOrdering.Arrange(document, State());

        Assert.Equal("fix the thrusters", arranged[0].Text);
    }

    [Fact]
    public void TheSystemUnderfootFloatsTheSameWay()
    {
        var document = Notes(
            (ChecklistScope.Universal, "ask Jim about the Krait build"),
            (ChecklistScope.System("Sol"), "deliver the steel"),
            (ChecklistScope.System("Lave"), "look at the market"));

        var arranged = ChecklistOrdering.Arrange(document, State());

        Assert.Equal("deliver the steel", arranged[0].Text);
    }

    /// <summary>
    /// With no state and nothing ranked there is no signal at all, and the honest reading is the
    /// file's own order rather than one d47 invented.
    /// </summary>
    [Fact]
    public void WithNoSignalTheFileOrderStands()
    {
        var document = Notes(
            (ChecklistScope.Universal, "first"),
            (ChecklistScope.System("Lave"), "second"),
            (ChecklistScope.Universal, "third"));

        var arranged = ChecklistOrdering.Arrange(document, state: null);

        Assert.Equal(["first", "third", "second"], arranged.Select(item => item.Text));
    }

    /// <summary>
    /// Within a project the derivation is blocked-versus-actionable: a grade no rank can roll is
    /// not the next thing to do however early its line was written.
    /// </summary>
    [Fact]
    public void BlockedAndStaleSinkWithinAProject()
    {
        var scope = ChecklistScope.Ship(12);

        var document = ChecklistDocument.For("F1") with
        {
            Items =
            [
                Derived(scope, "a", "a blocked grade", ChecklistState.Blocked),
                Derived(scope, "b", "open work", ChecklistState.Open),
                Derived(scope, "c", "a stale hull", ChecklistState.Stale),
                Derived(scope, "d", "more open work", ChecklistState.Open),
            ],
        };

        var arranged = ChecklistOrdering.Arrange(document, State());

        Assert.Equal(
            ["open work", "more open work", "a blocked grade", "a stale hull"],
            arranged.Select(item => item.Text));
    }

    /// <summary>
    /// The stored order is the tiebreak throughout, which is what keeps the Phase 25 movers
    /// meaning something: two open lines in one project stay in the order the Commander's own
    /// hand put them in.
    /// </summary>
    [Fact]
    public void TheCommandersHandOrderIsTheTiebreak()
    {
        var scope = ChecklistScope.Ship(12);

        var document = Notes((scope, "first errand"), (scope, "second errand"));
        var moved = document.Move(document.Items[1].Id, ChecklistMove.Top).Document;

        var arranged = ChecklistOrdering.Arrange(moved, State());

        Assert.Equal(["second errand", "first errand"], arranged.Select(item => item.Text));
    }

    /// <summary>
    /// The Commander's rank outranks the derivation — a rank the reading could overrule whenever
    /// they fly somewhere is a control that lies. Here the ship being flown is present and a
    /// ranked system still leads.
    /// </summary>
    [Fact]
    public void ARankedProjectOutranksHereAndNow()
    {
        var document = Notes(
            (ChecklistScope.Ship(12), "fix the thrusters"),
            (ChecklistScope.System("Lave"), "look at the market"));

        var state = State();
        var ranked = ChecklistOrdering.Rank(
            document, state, ChecklistScope.System("Lave"), ChecklistMove.Top);

        Assert.True(ranked.Changed);

        var arranged = ChecklistOrdering.Arrange(ranked.Document, state);

        Assert.Equal("look at the market", arranged[0].Text);
    }

    /// <summary>
    /// The Phase 42 ruling, decided rather than drifted into: a project the Commander has never
    /// ranked sorts below every one they have — ranking one project means "above everything I
    /// have not spoken for", or ranking means nothing on a list this size. The first rank writes
    /// the whole sequence down, so only a project arriving <em>after</em> it is unranked.
    /// </summary>
    [Fact]
    public void AProjectNeverRankedFallsBelowEveryRankedOne()
    {
        var document = Notes(
            (ChecklistScope.Universal, "ask Jim about the Krait build"),
            (ChecklistScope.System("Lave"), "look at the market"));

        var state = State();
        var ranked = ChecklistOrdering.Rank(
            document, state, ChecklistScope.System("Lave"), ChecklistMove.Top).Document;

        // A new project for the very ship being flown — the strongest here-and-now signal there
        // is — still lands below the two the Commander has an order for.
        var grown = ranked.AddNote(ChecklistScope.Ship(12), "fix the thrusters").Document;

        var arranged = ChecklistOrdering.Arrange(grown, state);

        Assert.Equal(
            ["look at the market", "ask Jim about the Krait build", "fix the thrusters"],
            arranged.Select(item => item.Text));
    }

    [Fact]
    public void RankRefusesWhatItCannotDo()
    {
        var one = Notes((ChecklistScope.Universal, "the only line"));

        Assert.False(ChecklistOrdering.Rank(
            one, null, ChecklistScope.Universal, ChecklistMove.Top).Changed);

        Assert.False(ChecklistOrdering.Rank(
            one, null, ChecklistScope.System("Nowhere"), ChecklistMove.Top).Changed);

        var two = Notes(
            (ChecklistScope.Universal, "a line"),
            (ChecklistScope.System("Lave"), "another"));

        var atTop = ChecklistOrdering.Rank(
            two, null, ChecklistScope.Universal, ChecklistMove.Top);

        Assert.False(atTop.Changed);
        Assert.Contains("already at the top", atTop.Report, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rank is the stored half and it has to survive a restart — "authored the way item text
    /// is authored" means the same file, the same store, the same round trip.
    /// </summary>
    [Fact]
    public void TheProjectOrderSurvivesARestart()
    {
        var path = Path.Combine(_folder, "checklist.json");

        var document = Notes(
            (ChecklistScope.Universal, "a line"),
            (ChecklistScope.System("Lave"), "another"));

        var ranked = ChecklistOrdering.Rank(
            document, null, ChecklistScope.System("Lave"), ChecklistMove.Top).Document;

        new ChecklistStore(path, NullLogger<ChecklistStore>.Instance).Save([ranked]);

        var reopened = new ChecklistStore(path, NullLogger<ChecklistStore>.Instance);
        reopened.Poll();

        Assert.Equal(ranked.ProjectOrder, reopened.For("F1").ProjectOrder);
    }

    /// <summary>
    /// The spoken route: a phrase names a project by the word the list shows for it, and two
    /// possible answers refuse rather than guess — acting on the wrong list is worse than asking.
    /// </summary>
    [Fact]
    public void ASpokenPhraseFindsTheProjectByItsWord()
    {
        var state = State();
        var checklists = TestSurface.EmptyChecklists(_folder, () => state);

        checklists.AddNote(ChecklistScope.System("Lave"), "look at the market");
        checklists.AddNote(ChecklistScope.System("Sol"), "deliver the steel");

        var change = checklists.Rank("lave", ChecklistMove.Top);

        Assert.True(change.Changed);
        Assert.Contains("Lave", change.Report, StringComparison.Ordinal);

        Assert.False(checklists.Rank("somewhere else", ChecklistMove.Top).Changed);
    }
}
