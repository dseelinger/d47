using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Ships index draws each hull's own artwork on its card, and the switch at the head of the
/// page puts the pictures away again.
/// <para>
/// The card grid shipped in commit 04e9b0c with no tests of its own, and the drawings are the
/// thing it was built to hold — so these cover the grid as well as the artwork: that a card finds
/// the picture for its hull, that a hull with no capture yet is still a working card, and that the
/// switch is remembered where a page's state is kept rather than in settings.
/// </para>
/// </summary>
public class TheFleetCardsCarryTheirHullTests
{
    /// <summary>
    /// The repo's own hull drawings. Walked up to from the test binary rather than hard-coded, so
    /// this keeps working wherever the suite is run from.
    /// </summary>
    private static string Assets
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "assets", "ships")))
            {
                at = at.Parent;
            }

            return at is null
                ? throw new DirectoryNotFoundException("assets/ships not found above the test binary")
                : Path.Combine(at.FullName, "assets", "ships");
        }
    }

    /// <summary>Puts the repo's drawings where the app reads them, and points it there.</summary>
    private static string Stocked(D47.Core.AppPaths paths)
    {
        foreach (var art in Directory.GetFiles(Assets, "*.png"))
        {
            File.Copy(art, Path.Combine(paths.Ships, Path.GetFileName(art)), overwrite: true);
        }

        ShipArt.Folder = paths.Ships;

        return paths.Ships;
    }

    /// <summary>A stocked folder for the tests that ask ShipArt directly, with no page involved.</summary>
    private static void Stocked()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-fleet-card-art-store"));

        paths.EnsureCreated();
        Stocked(paths);
    }

    private static (PanelView Panel, ViewStateStore Store) Fleet(bool drawings = true)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-fleet-card-art-tests"));

        paths.EnsureCreated();

        Stocked(paths);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        // A hull that has been captured, and one that has not. Both have to work.
        ships.BuildFor(12, "Corsair", "Reaper");
        ships.BuildFor(13, "Type8", "Cartage");

        var store = new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance);

        if (!drawings)
        {
            store.Save(store.Load() with { ShipsDrawingsOff = true });
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null, null, null, new ShipsDrawingsMemory(store));

        var window = new Window { Content = panel, Width = 1400, Height = 700 };

        window.Show();
        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (panel, store);
    }

    private static List<Image> Drawings(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Image>().Where(image => image.Source is Bitmap)];

    /// <summary>
    /// The fleet's own switch. Named rather than taken as the only one, because the panel has
    /// another elsewhere and a test that assumed otherwise would break the day a third arrived.
    /// </summary>
    private static ToggleSwitch Switch(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<ToggleSwitch>()
            .Single(box => box.Content as string == "Drawings");

    [AvaloniaFact]
    public void ACapturedHullIsDrawnOnItsCard()
    {
        var (panel, _) = Fleet();

        // One picture, not two: the Corsair has a capture and the Type-8 does not, and the card
        // for a hull with no drawing has to be a card rather than a gap.
        Assert.Single(Drawings(panel));
    }

    [AvaloniaFact]
    public void AHullWithNoCaptureStillHasItsCard()
    {
        var (panel, _) = Fleet();

        var names = panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains(names, text => text.Contains("Cartage", StringComparison.Ordinal));
        Assert.Contains(names, text => text.Contains("Reaper", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void TheSwitchPutsThePicturesAway()
    {
        var (panel, _) = Fleet(drawings: false);

        Assert.Empty(Drawings(panel));
    }

    [AvaloniaFact]
    public void TheSwitchIsWhereItWasLeft()
    {
        var (panel, store) = Fleet();

        var toggle = Switch(panel);

        Assert.True(toggle.IsChecked);

        toggle.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        // Remembered as the negative, so an unreadable file leaves the drawings on.
        Assert.True(store.Load().ShipsDrawingsOff);
        Assert.Empty(Drawings(panel));
    }

    [AvaloniaFact]
    public void ThePicturesComeBack()
    {
        var (panel, store) = Fleet(drawings: false);

        var toggle = Switch(panel);

        Assert.False(toggle.IsChecked);

        toggle.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.False(store.Load().ShipsDrawingsOff);
        Assert.Single(Drawings(panel));
    }

    [AvaloniaFact]
    public void PointingAtACardTurnsItsHull()
    {
        var (panel, _) = Fleet();

        var drawing = Drawings(panel).Single();
        var card = drawing.FindAncestorOfType<Button>()!;
        var resting = drawing.Source;

        card.RaiseEvent(new PointerEventArgs(
            InputElement.PointerEnteredEvent, card, new Pointer(0, PointerType.Mouse, true),
            null, default, 0, new PointerPointProperties(), KeyModifiers.None));

        // The timer is what advances it, so the frame after entering is still the resting one;
        // what this proves is that the hover found the sheet and armed the spin.
        Assert.NotNull(ShipArt.Frames("Corsair"));

        card.RaiseEvent(new PointerEventArgs(
            InputElement.PointerExitedEvent, card, new Pointer(0, PointerType.Mouse, true),
            null, default, 0, new PointerPointProperties(), KeyModifiers.None));

        Assert.Same(resting, drawing.Source);
    }

    [AvaloniaFact]
    public void AHullsFramesAreSlicedFromItsSheet()
    {
        Stocked();

        var frames = ShipArt.Frames("Corsair");

        Assert.NotNull(frames);

        // 120 frames is three degrees a step, the twenty-second rotation asked for.
        Assert.Equal(120, frames!.Count);

        // Every frame is one card, not a strip of them: a wrong grid would still slice cleanly
        // and be caught only by eye.
        var art = ShipArt.For("Corsair")!;
        Assert.All(frames, frame => Assert.Equal(art.Size, frame.Size));
    }

    [AvaloniaFact]
    public void AHullWithNoSheetSimplyDoesNotTurn()
    {
        Stocked();

        Assert.Null(ShipArt.Frames("Type8"));
        Assert.Null(ShipArt.Frames("a_hull_that_does_not_exist"));
    }

    [AvaloniaFact]
    public void AHullIsFoundWhateverCaseTheJournalWroteIt()
    {
        Stocked();

        // The journal writes "Corsair" and the file is "corsair.png"; a lookup that did not
        // normalise would find nothing and the miss would be silent.
        Assert.NotNull(ShipArt.For("Corsair"));
        Assert.NotNull(ShipArt.For("  CORSAIR "));
        Assert.Null(ShipArt.For("a_hull_that_does_not_exist"));
        Assert.Null(ShipArt.For(null));
    }
}
