using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>The settings page drawn from <see cref="SettingsLayout"/>: areas in the nav, places as pages.</summary>
public sealed class EverySettingSitsWhereTheLayoutPutsItTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(out SettingsService settings)
    {
        var (service, viewState, paths) = TestSurface.Create();
        settings = service;
        return SettingsHost.Open(service, viewState, paths);
    }

    private static List<Control> Nav(SettingsView view, string cssClass) =>
        [.. ((StackPanel)view.FindControl<Control>("NavItems")!).Children
            .Where(item => item.Classes.Contains(cssClass))];

    private static string? Words(Control item) =>
        item.GetVisualDescendants().OfType<TextBlock>().First().Text;

    private static string LabelOf(SettingsService settings, string key) =>
        settings.Sections.SelectMany(section => section.Rows).First(row => row.Key == key).Label;

    /// <summary>The area an area's title names, in <see cref="SettingsLayout.Areas"/> order.</summary>
    private static int AreaIndex(string title) =>
        SettingsLayout.Areas.ToList().FindIndex(area => area.Title == title);

    [AvaloniaFact]
    public void TheNavHeadingsAreTheAreasAndEveryPlaceIsASectionSomewhere()
    {
        var host = Open(out _);
        var places = SettingsLayout.Areas.SelectMany(area => area.Places).ToList();

        Assert.Equal(SettingsLayout.Areas.Select(area => area.Title), Nav(host.View, SettingsView.NavAreaClass).Select(Words));

        // Every place is built, whether or not its area is the one open right now (#220).
        Assert.Equal(places.Select(place => place.Id), host.View.SectionIds);

        host.Close();
    }

    /// <summary>
    /// An area's places are listed under its heading in layout order, and its heading opens the first of
    /// them.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Voice and hearing")]
    [InlineData("The ship's AI")]
    [InlineData("Privacy and this install")]
    public void AnAreasPlacesAreListedUnderItsHeading(string areaTitle)
    {
        var host = Open(out _);
        var area = SettingsLayout.Areas.Single(a => a.Title == areaTitle);

        // Diagnostics holds only diagnostics.paused and diagnostics.coverage, and a fresh test
        // surface has neither a paused subscriber nor coverage recording — so that place has no page
        // here and is absent from the nav (#283).
        var expected = area.Places.Where(place => place.Id != "diagnostics").Select(place => place.Title).ToList();

        var nav = ((StackPanel)host.View.FindControl<Control>("NavItems")!).Children.Where(item => item.IsVisible).ToList();
        var heading = nav.FindIndex(item => item.Classes.Contains(SettingsView.NavAreaClass) && Words(item) == areaTitle);
        var listed = nav.Skip(heading + 1).TakeWhile(item => item.Classes.Contains(SettingsView.NavPlaceClass));

        Assert.Equal(expected, listed.Select(Words));

        host.View.SelectArea(AreaIndex(areaTitle));
        Jobs();

        Assert.Equal(expected[0], SettingsPageReading.Words(SettingsPageReading.Title(host.View)));

        host.Close();
    }

    [AvaloniaFact]
    public void AttemptsIsDrawnInWhenATurnFailsUnderTheShipsAi()
    {
        var host = Open(out var settings);

        host.View.ShowPlaceOf(SpeechCapability.RetryAttemptsKey);
        Jobs();

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == LabelOf(settings, SpeechCapability.RetryAttemptsKey));

        // The nearest area heading above its nav item.
        var nav = ((StackPanel)host.View.FindControl<Control>("NavItems")!).Children.ToList();
        var at = nav.FindIndex(item => item.Classes.Contains(SettingsView.NavPlaceClass) && Words(item) == "When a turn fails");
        var area = nav.Take(at).Last(item => item.Classes.Contains(SettingsView.NavAreaClass));

        Assert.Equal("The ship's AI", Words(area));

        host.Close();
    }

    /// <summary>
    /// Searched for by key rather than by label: Privacy and egress has a row called Hull pictures of its
    /// own.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(ShipsCapability.HullArtKey)]
    [InlineData(CommunityGoalCapability.KeyRow)]
    [InlineData(GalaxyCapability.NotablePlacesKey)]
    public void ARowPlacedOnATabIsNotOnTheSettingsPage(string key)
    {
        var host = Open(out _);
        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

        box.Text = key;
        Jobs();

        Assert.Empty(SettingsPageReading.Counted(host.View));

        host.Close();
    }

    /// <summary>The same search does find a row the layout puts on the page, so the one above can fail.</summary>
    [AvaloniaFact]
    public void SearchingForAPlacedRowsKeyFindsIt()
    {
        var host = Open(out _);
        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

        box.Text = SpeechCapability.RetryAttemptsKey;
        Jobs();

        Assert.Equal(["When a turn fails"], SettingsPageReading.Counted(host.View).Keys);

        host.Close();
    }

    [AvaloniaFact]
    public void ResettingWhenATurnFailsLeavesTheVoiceProviderAlone()
    {
        var host = Open(out var settings);

        host.View.ShowPlaceOf(SpeechCapability.RetryAttemptsKey);
        Jobs();

        var provider = settings.Sections.SelectMany(section => section.Rows)
            .First(row => row.Key == SpeechCapability.ProviderKey)
            .Choices!
            .First(id => id != settings.Current.Speech.Provider);

        Assert.Equal(SettingApplyStatus.Applied, settings.Apply(SpeechCapability.RetryAttemptsKey, "5", SettingsCaller.Panel).Status);
        Assert.Equal(SettingApplyStatus.Applied, settings.Apply(SpeechCapability.ProviderKey, provider, SettingsCaller.Panel).Status);
        Jobs();

        host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetName(button) == "Reset When a turn fails")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.False(settings.IsChanged(SpeechCapability.RetryAttemptsKey));
        Assert.True(settings.IsChanged(SpeechCapability.ProviderKey));

        host.Close();
    }
}
