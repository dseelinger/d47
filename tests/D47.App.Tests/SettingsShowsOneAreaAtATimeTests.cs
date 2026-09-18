using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The settings page draws one area at a time (#220): the nav lists every area, but only the selected
/// one's places, and the scroller holds that area's own title, sentence and cards.
/// </summary>
public sealed class SettingsShowsOneAreaAtATimeTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static int AreaIndex(string title) => SettingsLayout.Areas.ToList().FindIndex(a => a.Title == title);

    private static List<Border> Cards(SettingsView view) =>
        [.. ((StackPanel)view.FindControl<Control>("Cards")!).Children.OfType<Border>()];

    private static string? Title(Border card) =>
        card.GetVisualDescendants().OfType<TextBlock>().Skip(1).First().Text;

    [AvaloniaFact]
    public void NoCardFromAnotherAreaIsVisibleWithScreensSelected()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.SelectArea(AreaIndex("Screens"));
        Jobs();

        var screens = SettingsLayout.Areas.Single(a => a.Title == "Screens");
        var otherTitles = SettingsLayout.Areas
            .Where(a => a.Title != "Screens")
            .SelectMany(a => a.Places)
            .Select(p => p.Title)
            .ToHashSet();

        Assert.Equal(screens.Places.Select(p => p.Title), Cards(host.View).Select(Title));
        Assert.DoesNotContain(Cards(host.View), card => Title(card) is { } title && otherTitles.Contains(title));

        host.Close();
    }

    /// <summary>Reveal selects the area before scrolling to the place inside it.</summary>
    [AvaloniaFact]
    public void RevealSwitchesAwayFromScreensToTheAreaThatHoldsTheCapability()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.SelectArea(AreaIndex("Screens"));
        Jobs();

        host.View.Reveal(ListeningCapability.Id);
        Jobs();

        Assert.Equal("voice-input", host.View.SectionIds[host.View.ActiveSection]);
        Assert.Contains(Cards(host.View), card => Title(card) == "Voice Input");
        Assert.DoesNotContain(Cards(host.View), card => Title(card) == "Overlay");

        host.Close();
    }

    /// <summary>
    /// A query narrows every area at once and names each one above its own matches; clearing it goes back
    /// to whichever area was selected before the search started.
    /// </summary>
    [AvaloniaFact]
    public void SearchingDrawsEveryAreasMatchesAndClearingReturnsToScreens()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.SelectArea(AreaIndex("Screens"));
        Jobs();

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

        box.Text = "Attempts";
        Jobs();

        Assert.Contains(Cards(host.View), card => Title(card) == "When a turn fails");

        box.Text = string.Empty;
        Jobs();

        Assert.Equal(
            SettingsLayout.Areas.Single(a => a.Title == "Screens").Places.Select(p => p.Title),
            Cards(host.View).Select(Title));

        host.Close();
    }

    /// <summary>A remembered place opens the area holding it (#268, extended by #220).</summary>
    [AvaloniaFact]
    public void ARememberedPlaceOpensTheAreaThatHoldsIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        viewState.Save(viewState.Load() with { SettingsSection = "turn-fails" });

        var host = SettingsHost.Open(settings, viewState, paths);
        Jobs();

        Assert.Equal("turn-fails", host.View.SectionIds[host.View.ActiveSection]);
        Assert.Contains(Cards(host.View), card => Title(card) == "When a turn fails");
        Assert.DoesNotContain(Cards(host.View), card => Title(card) == "Voice Input");

        host.Close();
    }

    /// <summary>Below the width the nav collapses at, a dropdown of area titles takes its place.</summary>
    [AvaloniaFact]
    public void ANarrowViewShowsTheAreaDropdownAndChoosingAnAreaShowsIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 700, height: 700);
        var view = host.View;

        var nav = view.FindControl<Control>("Nav")!;

        // Built in code rather than declared in the view's XAML, so it is not in its name scope.
        var dropdown = view.GetVisualDescendants().OfType<D47.App.Controls.Stepper>()
            .Single(combo => combo.Name == "AreaDropdown");

        Assert.False(nav.IsVisible, "the nav is expected to collapse at this width");
        Assert.True(dropdown.IsVisible, "the area dropdown is expected to take its place");

        var index = SettingsLayout.Areas.ToList().FindIndex(a => a.Title == "The ship's AI");

        var next = dropdown.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == "Next");

        // A press at a time, so the choice reaches the page through the stepper's own event rather than
        // by setting its value directly (#274).
        for (var tries = 0; dropdown.SelectedIndex != index && tries < dropdown.ItemsSource.Count; tries++)
        {
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Jobs();

        Assert.Equal(
            SettingsLayout.Areas[index].Places.Select(p => p.Title),
            Cards(view).Select(Title));

        host.Close();
    }
}
