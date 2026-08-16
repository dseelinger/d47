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
        Assert.Contains(checklists.Drain(), news => news.Text.Contains("is done", StringComparison.Ordinal));

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
        Assert.Contains("universal", filters);
        Assert.Contains("system", filters);
        Assert.Contains("open", filters);
        Assert.DoesNotContain("derived", filters);
    }
}
