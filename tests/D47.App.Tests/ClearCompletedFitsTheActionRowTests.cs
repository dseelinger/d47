using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>An owned ship's page draws its presses whole, Clear completed among them, on the desktop window and the headset panel; the mini surface draws none.</summary>
[Trait("Category", "Integration")]
[Collection(nameof(ShipArtCollection))]
public class ClearCompletedFitsTheActionRowTests
{
    private const string Loadout =
        """{"timestamp":"2026-08-18T09:10:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Star Hopper","Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,"Engineer":"Felicity Farseer","EngineerID":300100}},{"Slot":"PowerPlant","Item":"int_powerplant_size7_class5","On":true}]}""";

    [AvaloniaTheory]
    [InlineData("window", 1400, false)]
    [InlineData("window", 1400, true)]
    [InlineData("window", 1000, true)]
    [InlineData("window", 700, true)]
    [InlineData("headset", 1400, true)]
    [InlineData("mini", 512, true)]
    public void EveryPressIsWhole(string surface, int width, bool met)
    {
        using var look = AppLook.Put();
        using var kit = AppLook.ControlKit();

        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-clear-completed-captures"));

        paths.EnsureCreated();

        ShipArt.Files = new DiskFileSystem();
        ShipArt.Folder = paths.Ships;
        ShipArt.Shipped = Path.Combine(AppContext.BaseDirectory, "ships");

        var game = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     Loadout,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            game.Apply(parsed!);
        }

        var checklists = TestSurface.Checklists(paths.Data, () => game.Active);
        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => game.Active);
        var build = ships.BuildFor(12, "python", "Star Hopper");

        ships.Plan(build.Id, met ? new SlotPlan("MainEngines", "Dirty Drive Tuning", 5) : new SlotPlan("MainEngines", "Clean Drive Tuning", 5));
        ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", "Increased Range", 5));

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (surface == "headset")
        {
            panel.Classes.Add("headset");
        }

        panel.EnableLoadout(ships, checklists, () => game.Active, null);

        var window = new Window
        {
            Content = panel,
            Width = width,
            Height = surface == "mini" ? 280 : 900,
            Background = (Avalonia.Media.IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        panel.Tab = PanelTab.Assets;

        if (surface == "mini")
        {
            panel.Mode = PanelMode.Mini;
        }

        Dispatcher.UIThread.RunJobs();

        panel.Nav.Drill(new NavCrumb(LoadoutPages.ShipPrefix + build.Id, "Star Hopper") { Level = LoadoutPages.ShipPrefix });
        Dispatcher.UIThread.RunJobs();

        var presses = panel.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Content is "Put this build on my checklist" or "Standard-A rating" or "Copy as SLEF"
                or "Clear completed" or "Clear this plan")
            .ToList();

        Assert.Equal(surface == "mini" ? 0 : met ? 5 : 4, presses.Count);

        var page = panel.GetVisualDescendants().OfType<ItemPage>().Single();

        if (surface == "mini")
        {
            Assert.Contains(
                page.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Increased Range G5");
        }

        foreach (var press in presses)
        {
            var right = press.TranslatePoint(new Point(press.Bounds.Width, 0), page)!.Value.X;

            Assert.True(right <= page.Bounds.Width, $"{press.Content} ends at {right}, past the page's {page.Bounds.Width}.");
        }

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture($"clear-completed-{surface}-{width}{(met ? "" : "-none-met")}.png");
    }
}
