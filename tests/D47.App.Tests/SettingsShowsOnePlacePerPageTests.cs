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
[Trait("Category", "Integration")]
public sealed class SettingsShowsOnePlacePerPageTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static int AreaIndex(string title) => SettingsLayout.Areas.ToList().FindIndex(a => a.Title == title);

    /// <summary>The keys of every row a place's groups resolve to.</summary>
    private static List<string> Keys(SettingsService settings, SettingsPlace place) =>
        [.. place.Groups.SelectMany(group => group.Entries).SelectMany(settings.RowsForEntry).Select(row => row.Key)];

    [AvaloniaTheory]
    [InlineData("voice-input", "HEARING AND SOUND ›")]
    [InlineData("voice", "YOUR CORE ›")]
    [InlineData("sounds", "HEARING AND SOUND ›")]
    [InlineData("speech-synthesis", "PROVIDERS ›")]
    public void EachPlaceIsShownAloneUnderItsBreadcrumbAndTitle(string placeId, string crumb)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, placeId);

        var area = SettingsLayout.Areas.Single(a => a.Places.Any(p => p.Id == placeId));
        var place = area.Places.Single(p => p.Id == placeId);

        Assert.Equal(crumb, Crumb(host.View).Text);
        Assert.Equal(place.Title.ToUpperInvariant(), Words(Title(host.View)));

        Assert.Contains(Keys(settings, place), key => OnPage(host.View, host.ControlFor(key)));

        // Every row label on the page is the caption of one of this place's own rows.
        var captions = Keys(settings, place)
            .Select(key => settings.Find(key)?.CaptionFor(settings.Current))
            .ToHashSet();
        var drawn = host.View.RowLabels().Select(Words).ToList();

        Assert.NotEmpty(drawn);
        var foreign = drawn.Where(caption => !captions.Contains(caption)).ToList();

        Assert.True(foreign.Count == 0, "drawn here but not this place's: " + string.Join(", ", foreign));

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

        Assert.Equal("voice-input", host.View.OpenPlaceId());
        Assert.Equal("VOICE INPUT", Words(Title(host.View)));

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

        Assert.Equal("turn-fails", host.View.OpenPlaceId());
        Assert.Equal("PROVIDERS ›", Crumb(host.View).Text);
        Assert.Equal("WHEN A TURN FAILS", Words(Title(host.View)));

        host.Close();
    }

    /// <summary>Choosing an area heading opens that area's first place.</summary>
    [AvaloniaFact]
    public void AnAreaHeadingOpensItsFirstPlace()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.SelectArea(AreaIndex("Providers"));
        Jobs();

        Assert.Equal("language-model", host.View.OpenPlaceId());

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

        Assert.Contains("Your core › Its voice", entries);

        var index = entries.IndexOf("Providers › When a turn fails");

        var next = dropdown.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == "Next");

        // A press at a time, so the choice reaches the page through the stepper's own event rather than
        // by setting its value directly (#274).
        for (var tries = 0; dropdown.SelectedIndex != index && tries < entries.Count; tries++)
        {
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Jobs();

        Assert.Equal("turn-fails", view.OpenPlaceId());
        Assert.Equal("WHEN A TURN FAILS", Words(Title(view)));

        host.Close();
    }
}
