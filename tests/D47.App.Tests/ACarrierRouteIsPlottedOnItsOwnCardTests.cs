using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Navigation › Plan's Carrier Route card plots the Commander's own carrier through
/// <c>plot_carrier_route</c>, and Fleet › Carrier opens it with From set (#637).
/// </summary>
[Trait("Category", "Integration")]
public class ACarrierRouteIsPlottedOnItsOwnCardTests
{
    private const string CarrierSystem = "Sol";

    /// <summary>Answers every carrier plot with the recorded Sol, Colonia, Sol trip.</summary>
    private sealed class RecordedCarrierRoutes : IRouteService
    {
        public CarrierRouteQuery? Last { get; private set; }

        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<ExobiologyRoute?>(null);

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken)
        {
            Last = query;

            using var document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "spansh-fleetcarrier-route-sol-colonia-sol.json")));

            return Task.FromResult<CarrierRoute?>(new CarrierRoute([.. document.RootElement
                .GetProperty("result")
                .GetProperty("jumps")
                .EnumerateArray()
                .Select(jump => new CarrierWaypoint(
                    jump.GetProperty("name").GetString()!,
                    jump.GetProperty("distance").GetDouble(),
                    jump.GetProperty("distance_to_destination").GetDouble(),
                    jump.GetProperty("fuel_in_tank").GetInt32(),
                    jump.GetProperty("fuel_used").GetInt32(),
                    jump.GetProperty("must_restock").GetInt32() != 0,
                    jump.GetProperty("restock_amount").GetInt32(),
                    jump.GetProperty("tritium_in_market").GetInt32(),
                    jump.GetProperty("has_icy_ring").GetBoolean(),
                    jump.GetProperty("is_system_pristine").GetBoolean(),
                    jump.GetProperty("is_desired_destination").GetInt32() != 0))]));
        }
    }

    private const string Stats =
        """{"timestamp":"2026-09-05T12:00:00Z","event":"CarrierStats","CarrierID":3700000000,"Callsign":"K7Q-B4X","Name":"Sacred Fire","CarrierType":"FleetCarrier","FuelLevel":1000,"JumpRangeCurr":500.0,"SpaceUsage":{"TotalCapacity":25000,"Crew":0,"Cargo":0,"CargoSpaceReserved":0,"ShipPacks":0,"ModulePacks":0,"FreeSpace":25000},"Crew":[]}""";

    private const string Bought =
        """{"timestamp":"2026-09-05T12:00:00Z","event":"CarrierBuy","CarrierID":3700000000,"BoughtAtMarket":128666762,"Location":"Sol","SystemAddress":10477373803,"Price":4875000000,"Variant":"CarrierDockB","Callsign":"K7Q-B4X"}""";

    private const string Location =
        """{"timestamp":"2026-09-05T12:01:00Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":3700000000,"StarSystem":"Sol","SystemAddress":10477373803,"BodyID":0}""";

    private sealed record Surface(Window Window, PanelView Panel, RecordedCarrierRoutes Routes, RoutePlanBook Plans);

    private static Surface Open(bool managementRead = true, bool loadout = false)
    {
        var folder = TempFolders.Create("d47-carrier-route");
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", D47.Core.Configuration.SettingsCaller.Panel);

        var gameState = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-09-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     """{"timestamp":"2026-09-01T00:00:01Z","event":"FSDJump","StarSystem":"Alpha Centauri"}""",
                     managementRead ? Stats : Bought,
                     Location,
                 ])
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var routes = new RecordedCarrierRoutes();
        var plans = new RoutePlanBook(Path.Combine(folder, "route-plans.json"), new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
            [RouteCapability.Create(routes, null, () => gameState.Active, settings, plans)]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (loadout)
        {
            var checklists = new ChecklistService(
                new ChecklistStore(Path.Combine(folder, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(folder, "checklist-proposals.json"),
                    new MemoryFileSystem(),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => null);

            var ships = new ShipPlanService(
                new ShipBuildStore(Path.Combine(folder, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
                checklists,
                () => null);

            panel.EnableLoadout(ships, checklists, () => gameState.Active);
        }

        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
            () => gameState.Active?.Location.StarSystem,
            registry,
            plans,
            () => true,
            Commander: () => gameState.Active));

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, routes, plans);
    }

    private static IEnumerable<string> TextOf(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Inlines is { Count: > 0 } inlines
                ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
                : block.Text ?? string.Empty)
            .Where(text => text.Length > 0);

    /// <summary>The last box with this name: the Carrier Route card is the last card on Plan.</summary>
    private static TextBox Box(PanelView panel, string name) =>
        panel.GetVisualDescendants()
            .OfType<TextBox>()
            .Last(box => AutomationProperties.GetName(box) == name);

    /// <summary>The Plot button on the card under the CARRIER ROUTE heading.</summary>
    private static Button CarrierPlot(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Content as string == "Plot")
            .Last();

    /// <summary>Scrolls Plan down to the foot of the Carrier Route card, so a capture shows it.</summary>
    private static void ShowCard(PanelView panel)
    {
        CarrierPlot(panel).BringIntoView();
        Dispatcher.UIThread.RunJobs();

        if (panel.GetVisualDescendants().OfType<D47.App.Controls.StatusLine>().Last() is { IsVisible: true } status)
        {
            status.BringIntoView();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void PlottingFromTheCardOpensTheRouteLabelledAsTheCarriers()
    {
        var surface = Open();

        Assert.Contains("CARRIER ROUTE", TextOf(surface.Panel));
        ShowCard(surface.Panel);
        Save(surface.Window, "carrier-route-card-before.png");

        Box(surface.Panel, "To, required").Text = "Colonia";
        Press(CarrierPlot(surface.Panel));

        Assert.Equal(["Colonia", CarrierSystem], surface.Routes.Last?.Destinations);
        Assert.Equal(CarrierSystem, surface.Routes.Last?.Source);
        Assert.Equal("Sol to Colonia and back", surface.Panel.Nav.Trail[^1].Word);

        var text = TextOf(surface.Panel).ToList();

        Assert.Contains(text, line => line.StartsWith("Carrier route, Sol to Colonia and back: 90 jumps, 6,084 t of tritium", StringComparison.Ordinal));
        Assert.Contains(text, line => line.Contains("Tank and hold read from carrier management", StringComparison.Ordinal));
        Assert.Contains(text, line => line.StartsWith("RESTOCK ", StringComparison.Ordinal));
        Assert.Contains("PRISTINE ICY RING", text);

        Save(surface.Window, "carrier-route-card-after.png");
    }

    [AvaloniaFact]
    public void ACarrierWithNoManagementReadingIsRefusedOnTheCard()
    {
        var surface = Open(managementRead: false);

        Box(surface.Panel, "To, required").Text = "Colonia";
        Press(CarrierPlot(surface.Panel));

        Assert.Null(surface.Routes.Last);
        Assert.Null(surface.Plans.Last(RoutePlanKind.Carrier));
        Assert.Contains(
            surface.Panel.GetVisualDescendants().OfType<D47.App.Controls.StatusLine>(),
            status => status.Text == "Open carrier management once so I can read the hold.");

        ShowCard(surface.Panel);
        Save(surface.Window, "carrier-route-card-refused.png");
    }

    [AvaloniaFact]
    public void TheCarrierPagesActionLandsOnTheCardWithFromFilled()
    {
        var surface = Open(loadout: true);

        surface.Panel.Tab = PanelTab.Assets;
        surface.Panel.Nav.SelectRoot(LoadoutPages.CarrierRoot);
        Dispatcher.UIThread.RunJobs();

        Save(surface.Window, "carrier-page-plan-action.png");

        var action = surface.Panel.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Plan a carrier route");

        Press(action);

        Assert.Equal(PanelTab.Navigation, surface.Panel.Tab);
        Assert.Equal(RoutingPages.PlanRoot, surface.Panel.Nav.RootKeyOf(PanelTab.Navigation));
        Assert.Equal(CarrierSystem, Box(surface.Panel, "From, optional, filled from your carrier").Text);

        Save(surface.Window, "carrier-route-card-from-carrier-page.png");
    }
}
