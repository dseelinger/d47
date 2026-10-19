using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
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

/// <summary>Search › Bodies runs find_body from its form and draws the last answer, however it was asked (#1082).</summary>
public sealed class TheBodiesSearchPageDrawsTheLastAnswerTests
{
    private const string Fid = "F1";

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly BodySearchResult TwoBodies = new(
        "LTT 7786",
        2,
        [
            new BodySummary
            {
                Name = "Shinrarta Dezhra 4 a",
                SystemName = "Shinrarta Dezhra",
                Subtype = "Rocky body",
                Distance = 4.2,
                DistanceToArrival = 1200,
                IsLandable = true,
                Gravity = 0.18,
                Materials = [("Iron", 18.4), ("Polonium", 2.35)],
            },
            new BodySummary
            {
                Name = "Alioth 1",
                SystemName = "Alioth",
                Subtype = "High metal content world",
                Distance = 9.9,
                DistanceToArrival = 40,
                Gravity = 1.4,
                Materials = [("Polonium", 1.1)],
            },
        ]);

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    /// <summary>The service behind the real find_body tool, recording the query it was given.</summary>
    private sealed class Galaxy : IGalaxyService
    {
        public List<BodyQuery> Asked { get; } = [];

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            return Task.FromResult(TwoBodies);
        }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class World
    {
        public World(bool lookups)
        {
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);
            Store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

            Registry = CapabilityRegistry.Build([GalaxyCapability.Create(Galaxy, () => "LTT 7786", Settings, searches: Board)]);
        }

        public SettingsService Settings { get; }

        public SavedSearchStore Store { get; }

        public CapabilityRegistry Registry { get; }

        public GalaxySearchBoard Board { get; } = new();

        public Galaxy Galaxy { get; } = new();

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null),
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(Registry, Board, Store, () => Fid, () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        };
    }

    private static GalaxySearchPosting Posting(IReadOnlyDictionary<string, string> arguments, BodySearchResult result) =>
        new(
            GalaxySearchKind.Bodies,
            new Dictionary<string, string>(arguments, StringComparer.Ordinal),
            result.Reference,
            result,
            "Bodies matched.",
            AskedAt);

    private static (PanelView Panel, Window Window) Shown(World world, bool headset = false)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Bind(Window.BackgroundProperty, Avalonia.Application.Current!.Resources.GetResourceObservable(D47.App.Theming.ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(BodySearchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static BodySearchPage Page(Control root) => root.GetVisualDescendants().OfType<BodySearchPage>().Single();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static List<Control> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(GalaxySearchPage.RowClass))];

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheHeadsetDrawsTheAnswerViewAndTheFormViewFullWidth()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["landable"] = "true" }, TwoBodies));
        var (panel, window) = Shown(world, headset: true);
        var page = Page(panel);

        Assert.True(page.ShowsAnswer);
        Assert.Equal(2, Rows(panel).Count(row => row.IsEffectivelyVisible));
        Assert.True(Named(panel, GalaxySearchPage.EditName).IsEffectivelyVisible);
        Assert.False(Named(panel, GalaxySearchPage.RunName).IsEffectivelyVisible);

        Press(Named(panel, GalaxySearchPage.EditName));

        Assert.False(page.ShowsAnswer);
        Assert.True(Named(panel, GalaxySearchPage.RunName).IsEffectivelyVisible);
        Assert.Equal("true", page.FormArguments()["landable"]);
        Assert.DoesNotContain(Rows(panel), row => row.IsEffectivelyVisible);

        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.True(page.ShowsAnswer);
        Assert.Single(world.Galaxy.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void SearchOffersBodiesAfterStations()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Assert.Equal(
            [StarSystemPage.RootKey, SystemSearchPage.RootKey, StationSearchPage.RootKey, OutfittingSearchPage.RootKey, BodySearchPage.RootKey, SignalSearchPage.RootKey],
            panel.Nav.Roots(PanelTab.Search).Select(root => root.Key));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheShareColumnAsksForTheRichestAndTheShareColumnReadsPolonium()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("material", "Polonium");
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.False(Assert.Single(world.Galaxy.Asked).OrderByMaterial);

        Press(Named(panel, "BodyHead.Polonium"));

        var asked = world.Galaxy.Asked[^1];
        Assert.Equal("Polonium", asked.Material);
        Assert.True(asked.OrderByMaterial);

        var arguments = page.FormArguments();
        Assert.Equal("Polonium", arguments["material"]);
        Assert.False(arguments.ContainsKey("order_by"));

        var rows = Rows(panel);
        Assert.Equal(2, rows.Count);
        Assert.Contains("2.35%", TextOf(rows[0]));
        Assert.Contains("1.10%", TextOf(rows[1]));
        Assert.Contains(TextOf(panel), text => text.StartsWith("POLONIUM ▼", StringComparison.Ordinal));
        Assert.Contains(TextOf(panel), text => text.StartsWith("The richest in Polonium among the nearest 50", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ANearestSearchDrawsNoShareColumnAndNoRichestLine()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["landable"] = "true" }, TwoBodies));
        var (panel, window) = Shown(world);

        Assert.Equal(2, Rows(panel).Count);
        Assert.DoesNotContain(TextOf(panel), text => text.StartsWith("The richest", StringComparison.Ordinal));
        Assert.DoesNotContain("2.35%", TextOf(panel));
        Assert.Contains("0.18 g", TextOf(Rows(panel)[0]));

        window.Close();
    }

    [AvaloniaFact]
    public void AMisspelledVolcanismShowsTheToolsSentence()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Type("volcanism", "water geyser");
        Press(Named(panel, GalaxySearchPage.RunName));

        var sentence = Catalogue.Unknown("volcanism", "water geyser", Catalogue.Near(BodyCatalogue.Volcanism, "water geyser"));

        Assert.StartsWith("I don't know a volcanism called 'water geyser'. Did you mean ", sentence, StringComparison.Ordinal);

        Assert.Equal(GalaxySearchView.Refused, page.View);
        Assert.Equal(sentence, page.Sentence);
        Assert.Contains(sentence, TextOf(panel));
        Assert.Empty(world.Galaxy.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AVoicePostingRedrawsAnOpenPage()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Assert.Empty(Rows(panel));

        await Task.Run(() => world.Board.Post(Posting(new Dictionary<string, string> { ["material"] = "Polonium", ["order_by"] = "material", ["volcanism"] = "Water Geysers" }, TwoBodies)), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        var page = Page(panel);
        Assert.Equal(2, Rows(panel).Count);
        Assert.Equal("Polonium", page.FormArguments()["material"]);
        Assert.DoesNotContain("order_by", page.FormArguments().Keys);
        Assert.Equal("Water Geysers", page.FormArguments()["volcanism"]);

        window.Close();
    }

    [AvaloniaFact]
    public void SavedSaysThereAreNoQuickSearchesForBodies()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Assert.Contains("No quick searches for bodies.", TextOf(panel));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains(GalaxySearchPage.SavedClass));

        window.Close();
    }

    [AvaloniaFact]
    public void TheFormSendsEveryParameterTheToolTakes()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Type("near", "Sol");
        page.Type("volcanism", "Water Geysers");
        page.Type("atmosphere", "No atmosphere");
        page.Type("signal_count", "2");
        page.Type("hotspot_count", "3");
        page.Type("max_arrival_distance", "500");
        page.Type("gravity from", "0.1");
        page.Type("temperature to", "300");

        foreach (var name in (string[])["landable", "terraformable", "tidally_locked"])
        {
            page.TickFlag(name, true);
        }

        foreach (var name in (string[])["body_type", "material", "signal", "hotspot", "ring_type", "reserve_level", "power", "power_state"])
        {
            page.Pick(name, name switch
            {
                "body_type" => BodyCatalogue.Subtypes[0],
                "material" => BodyCatalogue.SurfaceMaterials[0],
                "signal" => BodyCatalogue.Signals[0],
                "hotspot" => BodyCatalogue.RingSignals[0],
                "ring_type" => BodyCatalogue.RingTypes[0],
                "reserve_level" => BodyCatalogue.ReserveLevels[0],
                _ => GalaxyFilters.Find(name)!.Choices[0],
            });
        }

        var amounts = page.GetVisualDescendants().OfType<Amount>().ToList();
        amounts[0].Value = 80;
        page.Limit = 8;

        var tool = GalaxyCapability.Create(null, () => null, world.Settings).Tools.Single(t => t.Name == "find_body");

        Assert.Equal(
            tool.Parameters.Select(parameter => parameter.Name).Except(["order_by", "order"]).Order(StringComparer.Ordinal),
            page.FormArguments().Keys.Order(StringComparer.Ordinal));

        Press(Named(panel, GalaxySearchPage.ClearName));

        Assert.Empty(page.FormArguments());

        window.Close();
    }

    [AvaloniaFact]
    public void EveryPressableControlIsAtLeastFortyFourTall()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["material"] = "Polonium", ["order_by"] = "material" }, TwoBodies));
        var (panel, window) = Shown(world);
        var page = Page(panel);

        var pressable = page.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control is Button or CheckBox or TextBox)
            .Where(control => !control.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.ScrollBar>().Any())
            .ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, control => Assert.True(
            control.Bounds.Height >= 44 - 0.5,
            $"{control.GetType().Name} '{control.Name ?? (control as ContentControl)?.Content?.ToString()}' is {control.Bounds.Height} tall"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheBodiesPageIsCaptured()
    {
        using var look = AppLook.Put();

        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["material"] = "Polonium", ["order_by"] = "material" }, TwoBodies));
        var (_, window) = Shown(world);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("search-bodies.png");

        window.Close();
    }
}
