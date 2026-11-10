using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship's page has a Standard-A rating press that plans its core internals at once (#2289).</summary>
[Collection(nameof(ShipArtCollection))]
public class AStandardARatingPressIsOnTheShipPageTests
{
    private static (Window Window, ItemPage Page, ShipPlanService Ships, string BuildId) Open()
    {
        var root = TestSurface.MemoryFolder("d47-standard-a-tests");
        var checklists = TestSurface.Checklists(root);
        var ships = TestSurface.ShipPlans(checklists, root, () => null);
        var build = ships.Intend("python")!;

        new ThemeManager(Avalonia.Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var nav = new PanelNavigator();
        nav.Register(PanelTab.Assets, new NavCrumb(LoadoutPages.FleetRoot, "Ships"));

        var page = new ItemPage(new ShipsMode(ships, checklists, () => null), nav, build.Id, prompts: null);

        var window = new Window
        {
            Content = page,
            Width = 1100,
            Height = 700,
            Background = (Avalonia.Media.IBrush)Avalonia.Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page, ships, build.Id);
    }

    private static Button Press(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Standard-A rating");

    [AvaloniaFact]
    public void PressingItPlansTheCoreInternalsAndSaysHowMany()
    {
        var (window, page, ships, buildId) = Open();

        Press(page).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(6, ships.Store.Find(buildId)!.Slots.Count);
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "Planned 6 core internals.");

        window.Close();
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void ThePressRendersToACapture()
    {
        using var art = AppLook.HullArt();

        var (window, page, _, _) = Open();

        using (var before = window.CaptureRenderedFrame()!)
        {
            before.SaveCapture("standard-a-before.png");
        }

        Press(page).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        using (var after = window.CaptureRenderedFrame()!)
        {
            after.SaveCapture("standard-a-after.png");
        }

        window.Close();
    }
}
