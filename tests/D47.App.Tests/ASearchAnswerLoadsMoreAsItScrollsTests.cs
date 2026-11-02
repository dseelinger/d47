using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A search answer on the panel fetches its next page as it is scrolled to its end (#1991).</summary>
public sealed class ASearchAnswerLoadsMoreAsItScrollsTests
{
    private const long Ltt7786 = 633608311522;

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

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

    /// <summary>A search_systems that answers pages of <see cref="PageSize"/> from <see cref="Total"/> rows, as the capability posts them.</summary>
    private sealed class World
    {
        public World()
        {
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

            Registry = CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = "galaxy-probe",
                    Group = "Foundation",
                    Name = "Galaxy probe",
                    Summary = "search_systems, paged.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "search_systems",
                            Description = "Answers a page at a time.",
                            Parameters = GalaxyCapability.Create(null, () => null, Settings).Tools.Single(tool => tool.Name == "search_systems").Parameters,
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

        public string Prefix { get; set; } = "Paged";

        /// <summary>The page that answers with an error.</summary>
        public int? FailPage { get; set; }

        /// <summary>Holds the next later page until set; it then answers whatever was asked, as a slow service does.</summary>
        public TaskCompletionSource? Hold { get; set; }

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 },
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(Registry, Board, null, () => "F1", () => []),
        };

        /// <summary>The page as the capability posts it: <c>page</c> left out of the arguments, and a later page extends.</summary>
        public GalaxySearchPosting Posting(IReadOnlyDictionary<string, string> asked, int page)
        {
            var first = ((page - 1) * PageSize) + 1;
            var count = Math.Clamp(Total - first + 1, 0, PageSize);
            var rows = Enumerable.Range(first, count)
                .Select(n => new SystemSummary { Name = $"{Prefix} {n}", Distance = n })
                .ToList();

            var search = new Dictionary<string, string>(asked, StringComparer.Ordinal);
            search.Remove("page");

            return new GalaxySearchPosting(
                GalaxySearchKind.Systems,
                search,
                "LTT 7786",
                new GalaxySearchResult("LTT 7786", Total, rows),
                "answered",
                AskedAt) { Pages = page, More = page * PageSize < Total };
        }

        private async Task<ToolResult> AnswerAsync(ToolArguments arguments, CancellationToken cancellationToken)
        {
            var asked = new Dictionary<string, string>(arguments.Values, StringComparer.Ordinal);
            Asked.Add(asked);

            var page = asked.TryGetValue("page", out var typed) ? int.Parse(typed, CultureInfo.InvariantCulture) : 1;
            var posting = Posting(asked, page);

            if (page > 1 && Hold is { } hold)
            {
                Hold = null;
                await hold.Task.ConfigureAwait(true);
            }

            if (FailPage == page)
            {
                return ToolResult.Error("The galaxy search could not be reached.");
            }

            if (page > 1)
            {
                Board.Extend(posting);
            }
            else
            {
                Board.Post(posting);
            }

            return ToolResult.Ok("answered");
        }
    }

    private static (PanelView Panel, Window Window) Shown(World world)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(SystemSearchPage.RootKey);
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

    private static SystemSearchPage Page(Control root) => root.GetVisualDescendants().OfType<SystemSearchPage>().Single();

    private static void Run(Control root)
    {
        Page(root).Run(new Dictionary<string, string>());
        Settle();
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static ItemsControl RowList(Control root) =>
        root.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName);

    private static ScrollViewer Scroller(Control root) => RowList(root).FindAncestorOfType<ScrollViewer>()!;

    private static List<string> Names(Control root) =>
        [.. RowList(root).Items.OfType<SystemSummary>().Select(system => system.Name)];

    private static List<string?> PagesAsked(World world) => [.. world.Asked.Select(asked => asked.GetValueOrDefault("page"))];

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Concat(text.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? []))];

    private static List<Control> DrawnRows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(GalaxySearchPage.RowClass))];

    [AvaloniaFact]
    public void ScrollingToTheEndAsksForPageTwoAndKeepsTheScrollPosition()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        Run(panel);

        Assert.Equal([null], PagesAsked(world));
        Assert.Equal(25, Names(panel).Count);
        Assert.Contains("25 OF 60 SYSTEMS", TextOf(panel));

        var list = RowList(panel);
        var scroll = Scroller(panel);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, $"page 1 fills {scroll.Extent.Height} of {scroll.Viewport.Height}");

        scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
        Settle();
        var offset = scroll.Offset;

        Assert.Equal([null, "2"], PagesAsked(world));
        Assert.Equal(new Dictionary<string, string> { ["page"] = "2" }, world.Asked[1]);
        Assert.Equal([.. Enumerable.Range(1, 50).Select(n => $"Paged {n}")], Names(panel));
        Assert.Contains("50 OF 60 SYSTEMS", TextOf(panel));
        Assert.Equal(offset, scroll.Offset);
        Assert.True(offset.Y > 0);

        Page(panel).Tick();
        Settle();

        Assert.Same(list, RowList(panel));
        Assert.Equal(offset, scroll.Offset);
        Assert.Equal(50, Names(panel).Count);

        window.Close();
    }

    [AvaloniaFact]
    public void AShortAnswerWithMoreFetchesTheNextPageUnscrolled()
    {
        var world = new World { PageSize = 3, Total = 6 };
        var (panel, window) = Shown(world);

        Run(panel);

        Assert.Equal([null, "2"], PagesAsked(world));
        Assert.Equal(["Paged 1", "Paged 2", "Paged 3", "Paged 4", "Paged 5", "Paged 6"], Names(panel));
        Assert.Equal(6, DrawnRows(panel).Count);
        Assert.Contains("6 SYSTEMS", TextOf(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedPageEndsTheTableWithRetryAndKeepsTheRows()
    {
        var world = new World { PageSize = 3, Total = 6, FailPage = 2 };
        var (panel, window) = Shown(world);

        Run(panel);

        Assert.Equal([null, "2"], PagesAsked(world));
        Assert.Equal(3, DrawnRows(panel).Count);
        Assert.Contains("The galaxy search could not be reached.", TextOf(panel));
        Assert.Contains("3 OF 6 SYSTEMS", TextOf(panel));

        world.FailPage = null;
        Press(Named(panel, GalaxySearchPage.RetryPageName));

        Assert.Equal([null, "2", "2"], PagesAsked(world));
        Assert.Equal(6, DrawnRows(panel).Count);
        Assert.DoesNotContain("The galaxy search could not be reached.", TextOf(panel));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), button => button.Name == GalaxySearchPage.RetryPageName);

        window.Close();
    }

    [AvaloniaFact]
    public void ASortPressedWhilePageTwoLoadsLeavesOnlyTheSortedAnswer()
    {
        var hold = new TaskCompletionSource();
        var world = new World { PageSize = 3, Total = 6, Hold = hold };
        var (panel, window) = Shown(world);

        Run(panel);

        Assert.Equal([null, "2"], PagesAsked(world));
        Assert.Contains("ASKING THE GALAXY SEARCH", TextOf(panel));

        world.Total = 3;
        world.Prefix = "Sorted";
        Press(Named(panel, "SystemHead.Population"));

        hold.SetResult();
        Settle();

        Assert.Equal("population", world.Asked[^1]["order_by"]);
        Assert.Equal(["Sorted 1", "Sorted 2", "Sorted 3"], Names(panel));
        Assert.Equal(3, RowList(panel).ItemCount);
        Assert.Contains("3 SYSTEMS", TextOf(panel));
        Assert.DoesNotContain("ASKING THE GALAXY SEARCH", TextOf(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheFormSendsNoLimitButAnswersAVoiceLimitPageByPage()
    {
        var world = new World { PageSize = 3, Total = 6 };
        world.Board.Post(world.Posting(new Dictionary<string, string> { ["limit"] = "3" }, 1));
        var (panel, window) = Shown(world);

        Assert.Equal([new Dictionary<string, string> { ["limit"] = "3", ["page"] = "2" }], world.Asked);
        Assert.Equal(6, Names(panel).Count);

        Press(Named(panel, GalaxySearchPage.RefreshName));

        Assert.False(world.Asked[1].ContainsKey("limit"));

        window.Close();
    }

    [AvaloniaFact]
    public void AHistoryEntryDoesNotPage()
    {
        var world = new World { PageSize = 3, Total = 6 };
        var (panel, window) = Shown(world);
        var posting = world.Posting(new Dictionary<string, string>(), 1);

        Page(panel).ShowEntry(SearchHistoryEntry.Of("search_systems", posting.Arguments, posting.Reference, null, posting.Result, posting.AskedAt));
        Settle();

        Assert.Empty(world.Asked);
        Assert.Equal(3, DrawnRows(panel).Count);

        window.Close();
    }

    [AvaloniaFact]
    public unsafe void TheHeadsetAnswerPagesTheSameWay()
    {
        var world = new World { PageSize = 3, Total = 6 };
        world.Settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        world.Board.Post(world.Posting(new Dictionary<string, string>(), 1));

        var headset = new VrPanelSurface(new PanelViewModel(), world.Settings, _ => null, starSystem: world.Surface);
        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(headset)!;

        headset.Nav.Select(PanelTab.Search);
        headset.Nav.SelectRoot(SystemSearchPage.RootKey);

        var (width, height) = headset.Size;
        var buffer = new byte[width * height * 4];

        fixed (byte* pixels = buffer)
        {
            for (var i = 0; i < 3; i++)
            {
                headset.Draw((IntPtr)pixels, width * 4);
                Settle();
                Page(view).Tick();
                Settle();
            }
        }

        Assert.Equal([new Dictionary<string, string> { ["page"] = "2" }], world.Asked);
        Assert.Equal(6, DrawnRows(view).Count);
    }
}
