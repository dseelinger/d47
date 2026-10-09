using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>The checklist read as named lists rather than one stream of lines (#829).</summary>
[Trait("Category", "Integration")]
public class ChecklistsGroupIntoNamedListsTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-lists-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static CommanderGameState Flying(int shipId)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"3311-04-08T18:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"3311-04-08T18:00:01Z","event":"Loadout","Ship":"krait_mkii","ShipID":{{shipId}},"ShipName":"Nightjar","Modules":[]}""",
                     """{"timestamp":"3311-04-08T18:00:02Z","event":"Location","StarSystem":"Sol","Docked":false}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private ChecklistService Service(CommanderGameState state, Func<ChecklistDocument, ChecklistDocument> seed)
    {
        Directory.CreateDirectory(_folder);

        var store = new ChecklistStore(
            Path.Combine(_folder, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance);

        store.Apply("F1", "Jameson", document => new ChecklistChange(seed(document), true, string.Empty));

        return new ChecklistService(
            store,
            new ChecklistProposalStore(
                Path.Combine(_folder, "proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => state);
    }

    private static ChecklistDocument With(
        ChecklistDocument document, ChecklistSource source, ChecklistScope scope, string key, ChecklistState state = ChecklistState.Open) =>
        document with
        {
            Items =
            [
                .. document.Items,
                new ChecklistItem
                {
                    Key = key,
                    Scope = scope,
                    Kind = source == ChecklistSource.Commander ? ChecklistItemKind.Authored : ChecklistItemKind.Derived,
                    Text = key,
                    Source = source,
                    Intent = source == ChecklistSource.Commander
                        ? null
                        : new ChecklistIntent(ChecklistIntentKind.EngineerPrerequisite, key),
                    State = state,
                },
            ],
        };

    private static ChecklistDocument Everything(ChecklistDocument document)
    {
        document = document.AddNote(ChecklistScope.Ship(3), "ship three").Document;
        document = document.AddNote(ChecklistScope.Ship(12), "ship twelve").Document;
        document = document.AddNote(ChecklistScope.System("Sol"), "deliver steel").Document;
        document = document.AddNote(ChecklistScope.Universal, "buy limpets").Document;
        document = With(document, ChecklistSource.EngineerPrerequisite, ChecklistScope.Universal, "invite");

        return With(document, ChecklistSource.Mission, ChecklistScope.Universal, "haul");
    }

    [Fact]
    public void AMissionLineIsInNoList()
    {
        var service = Service(Flying(12), Everything);

        var lists = service.Lists();

        Assert.Equal(5, lists.Count);
        Assert.DoesNotContain(lists.SelectMany(service.Lines), line => line.Source == ChecklistSource.Mission);
    }

    [Fact]
    public void TheShipBeingFlownIsFirstUnderShipsAndIsHere()
    {
        var service = Service(Flying(12), Everything);

        var ships = service.Lists().Where(list => list.Group == ChecklistListGroup.Ships).ToList();

        Assert.Equal(ChecklistScope.Ship(12).Key, ships[0].Scope.Key);
        Assert.True(ships[0].IsHere);
        Assert.False(ships[1].IsHere);
    }

    [Fact]
    public void AListWhoseLinesAreAllDoneMovesToNothingOpen()
    {
        var service = Service(Flying(12), document =>
        {
            document = Everything(document);

            return With(document, ChecklistSource.Commander, ChecklistScope.System("Lave"), "sold", ChecklistState.Done);
        });

        var lists = service.Lists();

        Assert.Equal(ChecklistListGroup.NothingOpen, lists[^1].Group);
        Assert.Equal("Lave", lists[^1].Name);
        Assert.Equal(1, lists[^1].Done);
    }

    [Fact]
    public void DeletingOneListsDoneLinesLeavesTheOthersAlone()
    {
        var service = Service(Flying(12), document =>
        {
            document = With(document, ChecklistSource.Commander, ChecklistScope.Ship(3), "a", ChecklistState.Done);

            return With(document, ChecklistSource.Commander, ChecklistScope.Ship(12), "b", ChecklistState.Done);
        });

        var three = service.Lists().Single(list => list.Scope.Key == "3");

        service.DeleteCompleted(three);

        var left = service.Lists().Single();
        Assert.Equal("12", left.Scope.Key);
        Assert.Equal(1, left.Done);
    }

    [Fact]
    public void EngineerUnlocksCannotBeAddedToButAShipCan()
    {
        var service = Service(Flying(12), Everything);
        var lists = service.Lists();

        Assert.False(service.CanAdd(lists.Single(list => list.IsEngineerUnlocks)));
        Assert.True(service.CanAdd(lists.First(list => list.Group == ChecklistListGroup.Ships)));
    }
}
