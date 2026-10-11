using D47.Core.Checklists;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A watch is refused where it cannot mean anything, and a file without one loads as before (#1056).</summary>
public class AWatchIsCheckedBeforeItIsKeptTests
{
    public static TheoryData<ChecklistItemKind, ChecklistWatch, string> Refused => new()
    {
        { ChecklistItemKind.Derived, new ChecklistWatch(ChecklistWatchKind.Arrive, "Sol"), "cannot carry a watch" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Arrive, " "), "without saying what" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Buy, "Tritium") { Quantity = 0 }, "the least is 1" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Dock, "Jameson Memorial") { Quantity = 2 }, "has none" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Arrive, "Sol") { Quantity = 1 }, "has none" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Arrive, "Sol") { Place = "Earth" }, "takes none" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.BuyOrder, "Tritium") { Place = "Sol" }, "takes none" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Arrive, "--"), "without saying what" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Dock, "Jameson Memorial") { Place = " " }, "no name in it" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Arrive, "Sol"), "since when" },
        { ChecklistItemKind.Authored, new ChecklistWatch(ChecklistWatchKind.Buy, "Tritium") { Since = Set, Progress = -1 }, "starts at 0" },
    };

    private static readonly DateTimeOffset Set = DateTimeOffset.Parse("2026-10-07T19:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [MemberData(nameof(Refused))]
    public void AWatchThatCannotMeanAnythingIsRefused(ChecklistItemKind kind, ChecklistWatch watch, string expected)
    {
        var item = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = kind,
            Source = kind == ChecklistItemKind.Derived ? ChecklistSource.EngineeringPlan : ChecklistSource.Commander,
            Intent = kind == ChecklistItemKind.Derived ? new ChecklistIntent(ChecklistIntentKind.Module, "MainEngines") : null,
            Text = "a line",
            Watch = watch,
        };

        Assert.Contains(expected, ChecklistValidation.Problem(item) ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AWatchThatMakesSenseIsKept()
    {
        var item = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = "Buy 5 Hydrogen Fuel",
            Watch = new ChecklistWatch(ChecklistWatchKind.Buy, "Hydrogen Fuel") { Quantity = 5, Place = "Jameson Memorial", Since = Set },
        };

        Assert.Null(ChecklistValidation.Problem(item));
    }

    [Fact]
    public void AChecklistWrittenBeforeWatchesLoadsUnchanged()
    {
        var files = new MemoryFileSystem();
        var store = new ChecklistStore(@"C:\d47\data\checklist.json", files, NullLogger<ChecklistStore>.Instance);

        files.WriteText(
            store.Path,
            """
            {
              "commanders": [
                { "commanderFid": "F1", "items": [
                  { "key": "note-1", "scope": { "group": "universal" },
                    "kind": "authored", "text": "buy limpets", "state": "done" } ] } ]
            }
            """);

        Assert.True(store.Poll());
        Assert.Empty(store.Problems);

        var item = Assert.Single(store.For("F1").Items);
        Assert.Null(item.Watch);
        Assert.Equal(ChecklistState.Done, item.State);
    }

    [Fact]
    public void AWatchSurvivesTheFile()
    {
        var files = new MemoryFileSystem();
        var store = new ChecklistStore(@"C:\d47\data\checklist.json", files, NullLogger<ChecklistStore>.Instance);
        var watch = new ChecklistWatch(ChecklistWatchKind.BuyOrder, "Tritium")
        {
            Quantity = 500,
            Since = DateTimeOffset.Parse("2026-10-07T19:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            Progress = 0,
            CountedTo = DateTimeOffset.Parse("2026-10-07T19:16:28Z", System.Globalization.CultureInfo.InvariantCulture),
        };

        store.Save(
        [
            ChecklistDocument.For("F1") with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = "note-1",
                        Scope = ChecklistScope.Universal,
                        Kind = ChecklistItemKind.Authored,
                        Text = "Put up a tritium order",
                        Watch = watch,
                    },
                ],
            },
        ]);

        Assert.Contains("\"buyOrder\"", files.ReadText(store.Path), StringComparison.Ordinal);
        Assert.Equal(watch, Assert.Single(store.For("F1").Items).Watch);
    }
}
