using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Outfitting and Signals search answers fetch their next page as they are scrolled to their end (#1992).</summary>
public sealed class TheOutfittingAndSignalsAnswersLoadMoreAsTheyScrollTests
{
    private const long Ltt7786 = 633608311522;

    private static readonly DateTimeOffset AskedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    /// <summary>A probe of one search tool that answers pages of <see cref="PageSize"/> from <see cref="Total"/> rows.</summary>
    private sealed class World
    {
        private readonly GalaxySearchKind _kind;

        public World(GalaxySearchKind kind)
        {
            _kind = kind;
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

            var name = kind == GalaxySearchKind.Signals ? "find_signal" : "search_outfitting";

            Registry = CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = "galaxy-probe",
                    Group = "Foundation",
                    Name = "Galaxy probe",
                    Summary = name + ", paged.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = name,
                            Description = "Answers a page at a time.",
                            Parameters = GalaxyCapability.Create(null, () => null, Settings).Tools.Single(tool => tool.Name == name).Parameters,
                            Protected = true,
                            Handler = AnswerAsync,
                        },
                    ],
                },
            ]);
        }

        public SettingsService Settings { get; }

        public CapabilityRegistry Registry { get; }

        public GalaxySearchBoard Board { get; } = new();

        public List<Dictionary<string, string>> Asked { get; } = [];

        public int PageSize { get; set; } = 25;

        public int Total { get; set; } = 60;

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 },
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(Registry, Board, null, () => "F1", () => []),
        };

        private Task<ToolResult> AnswerAsync(ToolArguments arguments, CancellationToken cancellationToken)
        {
            var asked = new Dictionary<string, string>(arguments.Values, StringComparer.Ordinal);
            Asked.Add(new Dictionary<string, string>(asked, StringComparer.Ordinal));

            var page = asked.TryGetValue("page", out var typed) ? int.Parse(typed, CultureInfo.InvariantCulture) : 1;
            var first = ((page - 1) * PageSize) + 1;
            var numbers = Enumerable.Range(first, Math.Clamp(Total - first + 1, 0, PageSize)).ToList();

            asked.Remove("page");

            object result = _kind == GalaxySearchKind.Signals
                ? new SignalSearchResult("LTT 7786", Total, [.. numbers.Select(n => new SignalFinding("Nav Beacon", $"Beacon {n}", n, AskedAt, false, n))])
                : new StationSearchResult("LTT 7786", Total, [.. numbers.Select(n => new StationSummary { Name = $"Station {n}", SystemName = "LTT 7786", MarketId = n })]);

            var posting = new GalaxySearchPosting(_kind, asked, "LTT 7786", result, "answered", AskedAt)
            {
                Pages = page,
                More = page * PageSize < Total,
            };

            if (page > 1)
            {
                Board.Extend(posting);
            }
            else
            {
                Board.Post(posting);
            }

            return Task.FromResult(ToolResult.Ok("answered"));
        }
    }

    private static (PanelView Panel, Window Window) Shown(World world, string rootKey)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(rootKey);
        Settle();

        return (panel, window);
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ItemsControl RowList(Control root) =>
        root.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName);

    private static ScrollViewer Scroller(Control root) => RowList(root).FindAncestorOfType<ScrollViewer>()!;

    private static void ScrollToEnd(Control root)
    {
        var scroll = Scroller(root);
        scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
        Settle();
    }

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Concat(text.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? []))];

    [AvaloniaFact]
    public void ASignalsAnswerOfSixtyScrolledToItsEndTwiceShowsAllSixty()
    {
        var world = new World(GalaxySearchKind.Signals);
        var (panel, window) = Shown(world, SignalSearchPage.RootKey);

        panel.GetVisualDescendants().OfType<SignalSearchPage>().Single().Run(new Dictionary<string, string> { ["signal"] = "Nav beacon" });
        Settle();

        Assert.Equal(25, RowList(panel).ItemCount);
        Assert.Contains("25 OF 60 SIGNALS", TextOf(panel));

        ScrollToEnd(panel);
        Assert.Equal(50, RowList(panel).ItemCount);

        ScrollToEnd(panel);
        Assert.Equal(60, RowList(panel).ItemCount);
        Assert.Contains("60 SIGNALS", TextOf(panel));
        Assert.Equal(["2", "3"], world.Asked.Skip(1).Select(asked => asked["page"]));

        window.Close();
    }

    [AvaloniaFact]
    public void AnOutfittingAnswerWithMoreHoldsBothPagesOnceScrolled()
    {
        var world = new World(GalaxySearchKind.Outfitting) { PageSize = 3, Total = 6 };
        var (panel, window) = Shown(world, OutfittingSearchPage.RootKey);

        panel.GetVisualDescendants().OfType<OutfittingSearchPage>().Single().Run(new Dictionary<string, string> { ["items"] = "6A Fuel Scoop" });
        Settle();

        Assert.Equal(6, RowList(panel).ItemCount);
        Assert.Equal(["2"], world.Asked.Skip(1).Select(asked => asked["page"]));
        Assert.Contains("6 STATIONS", TextOf(panel));

        window.Close();
    }
}
