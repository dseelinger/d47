using System.Collections.Concurrent;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Assets › Materials names the nearest material trader of each kind from the galaxy search (#2736).</summary>
public sealed class TheMaterialsPagesNameTheNearestTraderTests
{
    private const string Here = "Sol";

    private const string PermitSystem = "Sirius";

    private sealed class Galaxy : StubGalaxyService
    {
        private readonly ConcurrentDictionary<string, int> _calls = new(StringComparer.Ordinal);

        public Dictionary<string, StationSummary[]> Traders { get; } = new(StringComparer.Ordinal)
        {
            ["Raw"] = [Trader("Raw Near", 1, 12.4)],
            ["Manufactured"] = [Trader("Made Near", 2, 30.1)],
            ["Encoded"] = [Trader("Coded Near", 3, 1040.8)],
        };

        public int Calls(string kind) => _calls.GetValueOrDefault(kind);

        public int AllCalls => _calls.Values.Sum();

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            var kind = query.TraderType ?? "none";
            _calls.AddOrUpdate(kind, 1, (_, n) => n + 1);

            var stations = Traders.GetValueOrDefault(kind) ?? [];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, stations.Length, stations));
        }
    }

    private static StationSummary Trader(string system, long address, double distance) =>
        new() { Name = system + " Port", SystemName = system, SystemAddress = address, Distance = distance };

    private static (PanelView Panel, Window Window) Open(Galaxy? galaxy, Func<long, VisitState>? visit = null)
    {
        var root = TestSurface.MemoryFolder("d47-materials-trader-tests");
        var store = new GameStateStore();

        void Apply(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply($$"""{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"{{Here}}","SystemAddress":10477373803,"StarPos":[0,0,0],"Docked":false}""");

        CommanderGameState? State() => store.Active;

        var (checklists, _, _, ships, kit, _) = PlanBench.Over(root, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(
            ships, checklists, State, kit,
            galaxy: galaxy is null ? null : () => galaxy,
            visited: (_, address) => visit?.Invoke(address) ?? VisitState.NotListed);

        var window = new Window { Content = panel, Width = 1280, Height = 1200 };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.Show(PanelTabNames.AssetsRoot);
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Nav.Show(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static MaterialsPage Page(PanelView panel) => panel.GetVisualDescendants().OfType<MaterialsPage>().Single();

    private static void Select(PanelView panel, string view)
    {
        Page(panel).Select(view);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Text(PanelView panel) =>
        [.. Page(panel).GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>Lets the searches on the pool post their answers back.</summary>
    private static void Settle(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);

        while (!done() && DateTime.UtcNow < until)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(done());
    }

    [AvaloniaFact]
    public void NeededByPlansNamesAllThreeRawNamesItsOwnAndGuardianNone()
    {
        var galaxy = new Galaxy();
        var (panel, window) = Open(galaxy);

        Settle(() => Text(panel) is var shown && shown.Contains("RAW NEAR") && shown.Contains("MADE NEAR") && shown.Contains("CODED NEAR"));
        Assert.Contains("RAW TRADER", Text(panel));
        Assert.Contains("MANUFACTURED TRADER", Text(panel));
        Assert.Contains("ENCODED TRADER", Text(panel));
        Assert.Contains("12.4 LY", Text(panel));
        Assert.Contains("1,040.8 LY", Text(panel));

        Select(panel, "raw");
        Assert.Contains("NEAREST TRADER", Text(panel));
        Assert.Contains("RAW NEAR", Text(panel));
        Assert.DoesNotContain("MADE NEAR", Text(panel));
        Assert.DoesNotContain("CODED NEAR", Text(panel));
        Assert.DoesNotContain("RAW TRADER", Text(panel));

        Select(panel, "guardian");
        Assert.DoesNotContain("NEAREST TRADER", Text(panel));
        Assert.DoesNotContain("RAW TRADER", Text(panel));
        Assert.DoesNotContain("RAW NEAR", Text(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheFarmingFilterMovesTheTraderBetweenTheLineAndTheSlot()
    {
        var galaxy = new Galaxy();
        var (panel, window) = Open(galaxy);
        Select(panel, MaterialsPage.FarmingView);

        Settle(() => Text(panel).Contains("CODED NEAR"));
        Assert.Contains("RAW TRADER", Text(panel));

        Page(panel).Filter(MaterialsPage.FarmingFilters.ToList().IndexOf("Encoded"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("NEAREST TRADER", Text(panel));
        Assert.Contains("CODED NEAR", Text(panel));
        Assert.DoesNotContain("RAW NEAR", Text(panel));
        Assert.DoesNotContain("RAW TRADER", Text(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void NoTraderWithinReachSaysSoWithItsSentenceAsTheName()
    {
        var galaxy = new Galaxy();
        galaxy.Traders["Raw"] = [];
        var (panel, window) = Open(galaxy);
        Select(panel, "raw");

        Settle(() => Text(panel).Contains(MaterialsPage.NoneWithinWord));

        var state = Page(panel).GetVisualDescendants().OfType<Border>()
            .Single(border => AutomationProperties.GetName(border) == "No raw material trader within 500 light years.");
        Assert.Equal(MaterialsPage.NoneWithinWord, Assert.IsType<TextBlock>(state.Child).Text);

        window.Close();
    }

    [AvaloniaFact]
    public void RedrawsSendNoSearchAndOneAnswerServesEveryView()
    {
        var galaxy = new Galaxy();
        var (panel, window) = Open(galaxy);

        Settle(() => Text(panel).Contains("CODED NEAR"));
        Assert.Equal(1, galaxy.Calls("Raw"));

        Page(panel).Refresh();
        Page(panel).Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, galaxy.AllCalls);

        Select(panel, "raw");
        Page(panel).Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("RAW NEAR", Text(panel));
        Assert.Equal(1, galaxy.Calls("Raw"));
        Assert.Equal(3, galaxy.AllCalls);

        window.Close();
    }

    [AvaloniaFact]
    public void APermitSystemIsSkippedUntilTheVisitedListShowsIt()
    {
        Assert.True(PermitSystemTable.Locked(PermitSystem));
        Assert.False(StarterSystemTable.Contains(PermitSystem));
        Assert.True(PermitSystemTable.ClosedTo(PermitSystem, RankState.Empty, () => false));

        var galaxy = new Galaxy();
        galaxy.Traders["Raw"] = [Trader(PermitSystem, 100, 8.6), Trader("Open Near", 200, 12.4)];

        var (panel, window) = Open(galaxy, address => address == 100 ? VisitState.NotListed : VisitState.Unreadable);
        Select(panel, "raw");
        Settle(() => Text(panel).Contains("OPEN NEAR"));
        Assert.DoesNotContain(PermitSystem.ToUpperInvariant(), Text(panel));
        window.Close();

        (panel, window) = Open(galaxy, address => address == 100 ? VisitState.Visited : VisitState.NotListed);
        Select(panel, "raw");
        Settle(() => Text(panel).Contains(PermitSystem.ToUpperInvariant()));
        Assert.DoesNotContain("OPEN NEAR", Text(panel));
        window.Close();
    }

    [AvaloniaFact]
    public void WithNoGalaxySearchNoTraderIsDrawnAndTheHeadSaysWhy()
    {
        var (panel, window) = Open(galaxy: null);

        Assert.Contains(MaterialsPage.NoTraderSearch(all: true), Text(panel));
        Assert.DoesNotContain("RAW TRADER", Text(panel));

        Select(panel, "raw");
        Assert.Contains(MaterialsPage.NoTraderSearch(all: false), Text(panel));
        Assert.DoesNotContain("NEAREST TRADER", Text(panel));

        window.Close();
    }
}
