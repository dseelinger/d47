using D47.Core.Checklists;
using D47.Core.Goals;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Watches;
using D47.Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Web;

/// <summary>A Commander folded from the web fixture, and stores in memory that hold records for them and for another Commander.</summary>
internal sealed class SnapshotBench
{
    public const string Fid = "F9900417";

    public const string OtherFid = "F9900999";

    public static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    public SnapshotBench()
    {
        State = new CommanderGameState(new CommanderIdentity(Fid, "Ada Quillon"));

        foreach (var line in EmbeddedFixture.Lines("web.Journal.2026-11-12T090000.01.log"))
        {
            State.Apply(Event(line));
        }

        Checklists = new ChecklistStore(@"M:\data\checklist.json", _files, NullLogger<ChecklistStore>.Instance);
        GoalStore = new GoalStore(@"M:\data\goals.json", _files, NullLogger<GoalStore>.Instance);
        Goals = new GoalBook(GoalStore, () => Fid, () => State);
        Watches = new WatchStore(@"M:\data\watches.json", _files, NullLogger<WatchStore>.Instance);
        RoutePlans = new RoutePlanBook(@"M:\data\route-plans.json", _files, NullLogger<RoutePlanBook>.Instance);
    }

    public CommanderGameState State { get; }

    public ChecklistStore Checklists { get; }

    public GoalStore GoalStore { get; }

    public GoalBook Goals { get; }

    public WatchStore Watches { get; }

    public RoutePlanBook RoutePlans { get; }

    public WebSnapshotStores Stores => new()
    {
        Checklists = Checklists,
        Goals = Goals,
        Watches = Watches,
        RoutePlans = RoutePlans,
    };

    public static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    public WebSnapshot Build(string theme = "elite", GuiColourMatrix? hud = null) =>
        WebSnapshotBuilder.Build(State, Stores, theme, hud, TimeZoneInfo.Utc, Now);

    /// <summary>Writes a note and a ship line for <paramref name="fid"/>.</summary>
    public void Note(string fid, string name, string note, string shipLine)
    {
        Checklists.Apply(fid, name, document => new ChecklistChange(
            document with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = "note:" + note,
                        Scope = ChecklistScope.Universal,
                        Kind = ChecklistItemKind.Authored,
                        Text = note,
                    },
                    new ChecklistItem
                    {
                        Key = "ship:" + shipLine,
                        Scope = ChecklistScope.Ship(7),
                        Kind = ChecklistItemKind.Authored,
                        Text = shipLine,
                        State = ChecklistState.Done,
                    },
                ],
            },
            Changed: true,
            "written"));
    }

    /// <summary>A stock watch that has not fired and a next-system watch that has.</summary>
    public void Watch()
    {
        Assert.Null(Watches.Add(new Watch(
            new Guid("6c1d6f53-2f39-4d36-9a62-1f1b2a9a0001"),
            "Gold at Jameson",
            WatchKind.Stock,
            MarketId: 128666762,
            Station: "Jameson Memorial",
            System: "Shinrarta Dezhra",
            Commodity: "Gold",
            Comparison: StockComparison.Above,
            Threshold: 500)));

        Assert.Null(Watches.Add(new Watch(
            new Guid("6c1d6f53-2f39-4d36-9a62-1f1b2a9a0002"),
            "Encoded trader",
            WatchKind.NextSystem,
            Service: "Material Trader",
            ServiceKind: "Encoded",
            MinPad: PadSize.Large,
            FiredSystem: "Deciat",
            FiredAt: new DateTimeOffset(2026, 11, 11, 20, 31, 0, TimeSpan.Zero))));
    }

    /// <summary>A neutron plot for <paramref name="fid"/>.</summary>
    public void Plot(string fid, string destination) => RoutePlans.Record(
        new PlottedRoute(
            "Shinrarta Dezhra",
            destination,
            812.4,
            9,
            [
                new RouteWaypoint("Wregoe AC-D d12-0", 1, 640.2, IsNeutron: true),
                new RouteWaypoint(destination, 8, 0, IsNeutron: false),
            ]),
        "Shinrarta Dezhra to " + destination,
        new DateTimeOffset(2026, 11, 12, 9, 30, 0, TimeSpan.Zero),
        fid);
}
