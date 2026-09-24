using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// Each place in the nav is a page of its own (#435): choosing one replaces the page with that place
/// alone, under its area's breadcrumb and its title.
/// </summary>
public sealed class SettingsShowsOnePlacePerPageTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static int AreaIndex(string title) => SettingsLayout.Areas.ToList().FindIndex(a => a.Title == title);

    /// <summary>The keys of every row a place's groups resolve to.</summary>
    private static List<string> Keys(SettingsService settings, SettingsPlace place) =>
        [.. place.Groups.SelectMany(group => group.Entries).SelectMany(settings.RowsForEntry).Select(row => row.Key)];

    [AvaloniaTheory]
    [InlineData("voice-input")]
    [InlineData("voice")]
    [InlineData("sounds")]
    public void EachPlaceIsShownAloneUnderItsBreadcrumbAndTitle(string placeId)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, placeId);

        var area = SettingsLayout.Areas.Single(a => a.Places.Any(p => p.Id == placeId));
        var place = area.Places.Single(p => p.Id == placeId);

        Assert.Equal("VOICE AND HEARING ›", Crumb(host.View).Text);
        Assert.Equal(place.Title, Words(Title(host.View)));

        Assert.Contains(Keys(settings, place), key => OnPage(host.View, host.View.ControlFor(key)));

        var ownKeys = Keys(settings, place).ToHashSet();

        foreach (var other in SettingsLayout.Areas.SelectMany(a => a.Places).Where(p => p.Id != placeId))
        {
            Assert.DoesNotContain(
                Keys(settings, other).Where(key => !ownKeys.Contains(key)),
                key => OnPage(host.View, host.View.ControlFor(key)));
        }

        host.Close();
    }

    /// <summary>A reveal opens the page holding the capability, from a page in another area.</summary>
    [AvaloniaFact]
    public void RevealOpensThePageThatHoldsTheCapability()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.SelectArea(AreaIndex("Screens"));
        Jobs();

        host.View.Reveal(ListeningCapability.Id);
        Jobs();

        Assert.Equal("voice-input", host.View.SectionIds[host.View.ActiveSection]);
        Assert.Equal("Voice Input", Words(Title(host.View)));

        host.Close();
    }

    /// <summary>A remembered place opens on its own page (#268).</summary>
    [AvaloniaFact]
    public void ARememberedPlaceOpensItsPage()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        viewState.Save(viewState.Load() with { SettingsSection = "turn-fails" });

        var host = SettingsHost.Open(settings, viewState, paths);
        Jobs();

        Assert.Equal("turn-fails", host.View.SectionIds[host.View.ActiveSection]);
        Assert.Equal("THE SHIP'S AI ›", Crumb(host.View).Text);
        Assert.Equal("When a turn fails", Words(Title(host.View)));

        host.Close();
    }

    /// <summary>Choosing an area heading opens that area's first place.</summary>
    [AvaloniaFact]
    public void AnAreaHeadingOpensItsFirstPlace()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.SelectArea(AreaIndex("The ship's AI"));
        Jobs();

        Assert.Equal("language-model", host.View.SectionIds[host.View.ActiveSection]);

        host.Close();
    }

    /// <summary>
    /// Below the width the nav collapses at, a picker takes its place, listing each place after its
    /// area's title, and choosing one changes page.
    /// </summary>
    [AvaloniaFact]
    public void ANarrowViewPicksPlacesByAreaAndName()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 800, height: 700);
        var view = host.View;

        var nav = view.FindControl<Control>("Nav")!;

        // Built in code rather than declared in the view's XAML, so it is not in its name scope.
        var dropdown = view.GetVisualDescendants().OfType<D47.App.Controls.Stepper>()
            .Single(combo => combo.Name == "AreaDropdown");

        Assert.False(nav.IsVisible, "the nav is expected to collapse at this width");
        Assert.True(dropdown.IsVisible, "the picker is expected to take its place");

        var entries = dropdown.ItemsSource.Cast<object>().Select(item => item.ToString()).ToList();

        Assert.Contains("Voice and hearing › Its voice", entries);

        var index = entries.IndexOf("The ship's AI › When a turn fails");

        var next = dropdown.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == "Next");

        // A press at a time, so the choice reaches the page through the stepper's own event rather than
        // by setting its value directly (#274).
        for (var tries = 0; dropdown.SelectedIndex != index && tries < entries.Count; tries++)
        {
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Jobs();

        Assert.Equal("turn-fails", view.SectionIds[view.ActiveSection]);
        Assert.Equal("When a turn fails", Words(Title(view)));

        host.Close();
    }
}
