using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The store follows <see cref="D47.Core.Actions.MacroStore"/> deliberately (list.md Phase 17):
/// a file under <c>data/</c>, polled on its write time so a hand edit is live with no restart,
/// and <b>problems reported rather than items silently dropped</b>.
/// </summary>
public class ChecklistStoreTests
{
    private static ChecklistStore Store(TempInstall install) =>
        new(Path.Combine(install.Paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance);

    [Fact]
    public void AHandEditIsLiveWithNoRestart()
    {
        using var install = new TempInstall();
        var store = Store(install);

        File.WriteAllText(
            store.Path,
            """
            {
              "commanders": [
                { "commanderFid": "F1", "items": [
                  { "key": "note-1", "scope": { "group": "universal" },
                    "kind": "authored", "text": "buy limpets" } ] } ]
            }
            """);

        Assert.True(store.Poll());
        Assert.Equal("buy limpets", store.For("F1").Items.Single().Text);
    }

    [Fact]
    public void ABadLineIsReportedAndTheRestOfTheFileStillLoads()
    {
        using var install = new TempInstall();
        var store = Store(install);

        File.WriteAllText(
            store.Path,
            """
            {
              "commanders": [
                { "commanderFid": "F1", "items": [
                  { "key": "", "scope": { "group": "universal" }, "kind": "authored", "text": "nameless" },
                  { "key": "note-2", "scope": { "group": "universal" },
                    "kind": "authored", "text": "buy limpets" } ] } ]
            }
            """);

        store.Poll();

        // One typo must not cost somebody their other forty items, and the one that was refused
        // has to be sayable rather than silently gone.
        Assert.Single(store.For("F1").Items);
        Assert.Single(store.Problems);
        Assert.Contains("no key", store.Problems[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableFileIsOneProblemRatherThanAThrow()
    {
        using var install = new TempInstall();
        var store = Store(install);

        File.WriteAllText(store.Path, "{ this is not json");

        store.Poll();

        Assert.Empty(store.Documents);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void TheCommanderKeyIsInsideTheDocumentAndNeverInThePath()
    {
        using var install = new TempInstall();
        var store = Store(install);

        // The Frontier id comes out of the journal and journal content is untrusted input, so
        // turning it into a filename would buy a path-traversal surface for an organisational
        // convenience. One file, whoever is flying.
        store.Apply("F1", "Jameson", document => document.AddNote(ChecklistScope.Universal, "one"));
        store.Apply("F2", "Hicks", document => document.AddNote(ChecklistScope.Universal, "two"));

        Assert.Equal(2, store.Documents.Count);
        Assert.Single(Directory.GetFiles(install.Paths.Data, "checklist*.json"));
        Assert.Equal("one", store.For("F1").Items.Single().Text);
        Assert.Equal("two", store.For("F2").Items.Single().Text);
    }

    [Fact]
    public void AChangeRaisesTheEventThePanelFollows()
    {
        using var install = new TempInstall();
        var store = Store(install);

        var raised = 0;
        store.Changed += () => raised++;

        store.Apply("F1", "Jameson", document => document.AddNote(ChecklistScope.Universal, "buy limpets"));

        Assert.True(raised > 0);
    }

    [Fact]
    public void AComputedTickGoingBackwardsIsSaidOnce()
    {
        using var install = new TempInstall();
        var gameState = new GameStateStore();
        var checklists = TestSurface.Checklists(install.Paths, gameState);

        void Apply(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        Apply("""{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""");

        Apply(
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
              "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true,
                "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0} } ] }
            """);

        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        checklists.List.Save(
        [
            ChecklistDocument.For("F1", "Jameson") with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = ChecklistKeys.For(intent),
                        Scope = ChecklistScope.Ship(12),
                        Kind = ChecklistItemKind.Derived,
                        Source = ChecklistSource.EngineeringPlan,
                        Text = "Grade 5 dirty drives",
                        Intent = intent,
                        Hull = "krait_mkii",
                    },
                ],
            },
        ]);

        checklists.Poll();

        // One way of saying it, and the shorter one (asked for 2026-08-23). The verdict already
        // names the module and says what happened to it, so quoting the plan line in front of it
        // said the same thing twice — and the ship it ended with is the one the Commander is
        // sitting in.
        var done = Assert.Single(checklists.Drain());

        Assert.Equal("5A Thrusters is at grade 5 and finished.", done.Text);
        Assert.DoesNotContain("krait", done.Text, StringComparison.OrdinalIgnoreCase);

        // Nothing changed, so nothing is said a second time.
        checklists.Poll();
        Assert.Empty(checklists.Drain());

        // Now the world moves under a plan that did not change: the module is sold.
        Apply(
            """
            { "timestamp":"2026-08-16T11:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
              "Modules":[] }
            """);

        checklists.Poll();

        var undone = Assert.Single(checklists.Drain());
        Assert.Contains("no longer done", undone.Text, StringComparison.Ordinal);

        // And once only. A computed tick going backwards is information, not a nag.
        checklists.Poll();
        Assert.Empty(checklists.Drain());
    }

    /// <summary>
    /// Reported 2026-08-23 as a stream of "X is done" for work finished while d47 was not running.
    /// Priming already folds the backlog silently, but that rule was attached to the tick rather
    /// than to the document — so a file rewritten under a running d47 was re-read mid-session and
    /// every disagreement between what it stored and what the game says was read out as news. The
    /// hand edit the store exists to support is the same path as a restored backup or a data folder
    /// refreshed from another install.
    /// </summary>
    [Fact]
    public void ADocumentThatArrivedFromOutsideIsFoldedWithoutAnnouncingIt()
    {
        using var install = new TempInstall();
        var gameState = new GameStateStore();
        var checklists = TestSurface.Checklists(install.Paths, gameState);

        void Apply(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        Apply("""{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""");

        Apply(
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
              "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true,
                "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0} } ] }
            """);

        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        // Written through a second store over the same file, which is what a hand edit, a restored
        // backup or another process looks like from here: the running service did not write it, so
        // its stamp moves and the whole document is new to it.
        Store(install).Save(
        [
            ChecklistDocument.For("F1", "Jameson") with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = ChecklistKeys.For(intent),
                        Scope = ChecklistScope.Ship(12),
                        Kind = ChecklistItemKind.Derived,
                        Source = ChecklistSource.EngineeringPlan,
                        Text = "Grade 5 dirty drives",
                        Intent = intent,
                        Hull = "krait_mkii",
                    },
                ],
            },
        ]);

        checklists.Poll();

        // The roll was finished before this document arrived. Mark it done, say nothing.
        Assert.Empty(checklists.Drain());
        Assert.Equal(ChecklistState.Done, checklists.List.For("F1").Items.Single().State);

        // And the silence is for the arrival, not for the session: the next thing the world does
        // is still news.
        Apply(
            """
            { "timestamp":"2026-08-16T11:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
              "Modules":[] }
            """);

        checklists.Poll();

        var undone = Assert.Single(checklists.Drain());
        Assert.Contains("no longer done", undone.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PrimingFoldsTheBacklogWithoutAnnouncingAnyOfIt()
    {
        using var install = new TempInstall();
        var gameState = new GameStateStore();
        var checklists = TestSurface.Checklists(install.Paths, gameState);

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """
                     { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
                       "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true,
                         "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0} } ] }
                     """,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        checklists.List.Save(
        [
            ChecklistDocument.For("F1", "Jameson") with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = ChecklistKeys.For(intent),
                        Scope = ChecklistScope.Ship(12),
                        Kind = ChecklistItemKind.Derived,
                        Source = ChecklistSource.EngineeringPlan,
                        Text = "Grade 5 dirty drives",
                        Intent = intent,
                        Hull = "krait_mkii",
                    },
                ],
            },
        ]);

        checklists.Poll(announce: false);

        // Folded, silently. Starting d47 after Elite must behave like starting it before.
        Assert.Empty(checklists.Drain());
        Assert.True(checklists.Document.Items.Single().IsComplete);
    }

    [Fact]
    public void ALineTakenBeforeAnyCommanderWasKnownIsAdoptedRatherThanLost()
    {
        using var install = new TempInstall();
        var gameState = new GameStateStore();
        var checklists = TestSurface.Checklists(install.Paths, gameState);

        // d47 can be running before Elite is, and the Frontier id only exists once a journal has
        // been read.
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            NullLogger.Instance,
            out var parsed));

        gameState.Apply(parsed!);
        checklists.Poll();

        Assert.Equal("buy limpets", checklists.Document.Items.Single().Text);
        Assert.Equal("F1", checklists.Document.CommanderFid);
    }

    [Fact]
    public void TheFilterRowIsAProjectionRatherThanAList()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.System("Sol"), "ask Jim about the Krait build");

        var filters = checklists.Filters();

        // Everything actually on the list, and nothing that is not. A fourth kind of plan turns
        // up here without anybody remembering to add it.
        Assert.Contains("authored", filters);

        // The Commander's word, not the enum's (remediation.md 10, item 16). The group is still
        // ChecklistGroup.Universal and still stored as "universal"; this row is what they read.
        Assert.Contains("custom", filters);
        Assert.Contains("system", filters);
        Assert.Contains("open", filters);
        Assert.DoesNotContain("derived", filters);

        // **No Ship row, ever** (the Commander's ruling, 2026-08-20). Nearly everything derived is
        // ship-scoped, so it narrows almost nothing while reading like a real choice. The scope
        // itself is untouched — it is what makes a ship's plan follow that ship through a swap.
        checklists.AddNote(ChecklistScope.Ship(12), "swap the shield booster");

        Assert.DoesNotContain("ship", checklists.Filters());

        // And every row says which question it answers, rather than the four axes arriving as one
        // flat list of enum spellings.
        Assert.All(checklists.FilterAxes(), filter => Assert.NotEmpty(filter.Heading));
        Assert.DoesNotContain(checklists.FilterAxes(), filter => filter.Word == filter.Key);
    }
}
