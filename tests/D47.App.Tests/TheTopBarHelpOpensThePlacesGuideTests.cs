using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Help;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>The top bar's HELP on the settings page opens the guide for the place that is open (#435).</summary>
public class TheTopBarHelpOpensThePlacesGuideTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(settings, viewState, paths);
    }

    private static void PressHelp(SettingsHost host)
    {
        host.Panel.FindControl<Button>("HelpButton")!
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    /// <summary>
    /// Pressed on Voice Input, it draws the Listening band — that place's own subject, not the page about
    /// Settings.
    /// </summary>
    [AvaloniaFact]
    public void OnVoiceInputItDrawsThatPlacesOwnPageInThePanel()
    {
        var host = Open();

        SettingsPageReading.Open(host.View, "voice-input");
        PressHelp(host);

        Assert.True(host.Panel.Nav.Modal, "help took the panel");
        Assert.Equal(HelpLevel.Prefix + ListeningCapability.Id, host.Panel.Nav.Trail[^1].Key);

        // The band, not the settings page it was pressed from.
        var shown = host.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.Contains(shown, text => text.StartsWith("Whisper turns your voice", StringComparison.Ordinal));

        host.Close();
    }

    /// <summary>On Its voice, the guide its place names.</summary>
    [AvaloniaFact]
    public void OnItsVoiceItOpensTheGuideThatPlaceNames()
    {
        var host = Open();
        var place = SettingsLayout.Areas.SelectMany(a => a.Places).Single(p => p.Id == "voice");

        SettingsPageReading.Open(host.View, "voice");
        PressHelp(host);

        Assert.Equal(HelpLevel.Prefix + place.DocsCapabilityId, host.Panel.Nav.Trail[^1].Key);

        host.Close();
    }

    /// <summary>And there is a way back to the page it was pressed from.</summary>
    [AvaloniaFact]
    public void TheBreadcrumbGoesBackToTheSettingsPage()
    {
        var host = Open();

        Assert.Equal("Settings", host.Panel.Nav.Trail[^1].Word);

        SettingsPageReading.Open(host.View, "voice-input");
        PressHelp(host);

        Assert.Equal("Help", host.Panel.Nav.Trail[^1].Word);

        Assert.True(host.Panel.GoBack());
        Jobs();

        Assert.False(host.Panel.Nav.Modal, "help was dismissed");
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);
        Assert.Equal("Settings", host.Panel.Nav.Trail[^1].Word);
        Assert.Equal("voice-input", host.View.SectionIds[host.View.ActiveSection]);

        host.Close();
    }

    /// <summary>What it draws ends with the way out to the site, named for where it goes.</summary>
    [AvaloniaFact]
    public void WhatItDrawsOffersTheLongFormOnline()
    {
        var host = Open();

        SettingsPageReading.Open(host.View, "voice-input");
        PressHelp(host);

        var shown = host.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.Contains("More details online", shown);

        host.Close();
    }

    /// <summary>A place whose guide nobody has illustrated opens the Settings guide rather than nothing.</summary>
    [AvaloniaFact]
    public void APlaceWithNoBandOpensTheSettingsGuide()
    {
        var bandless = SettingsLayout.Areas.SelectMany(a => a.Places)
            .FirstOrDefault(place => HelpLibrary.For(place.DocsCapabilityId) is null);

        // Only meaningful while some place names a guide with no band.
        if (bandless is null)
        {
            return;
        }

        var host = Open();

        SettingsPageReading.Open(host.View, bandless.Id);

        if (host.View.SectionIds[host.View.ActiveSection] != bandless.Id)
        {
            // That place has no page on a fresh surface.
            host.Close();
            return;
        }

        PressHelp(host);

        Assert.True(host.Panel.Nav.Modal, "the mark always opens something");
        Assert.Equal(HelpLevel.Prefix + SettingsCapability.Id, host.Panel.Nav.Trail[^1].Key);

        host.Close();
    }

    /// <summary>A chooser no longer makes the mark inert.</summary>
    [AvaloniaFact]
    public void HelpOpensOverAChooserAndBackReturnsToIt()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Loadout, new NavCrumb("ships", "Ships") { Help = "ships" });
        nav.Select(PanelTab.Loadout);

        // What PanelPrompts does for a page-surface chooser, with the help its request declares.
        nav.Take(new NavCrumb("loadout.module", "Module") { Help = ShipsMode.ModuleChoiceHelp });

        Assert.True(nav.Modal, "the chooser has the panel");

        Assert.True(HelpLevel.Open(nav), "the mark is not inert over a chooser");
        Assert.Equal(HelpLevel.Prefix + ShipsMode.ModuleChoiceHelp, nav.Trail[^1].Key);

        // Pressing it again is not a request for help about help.
        Assert.False(HelpLevel.Open(nav));

        // And the chooser is still underneath, which is what makes this safe to stack.
        Assert.True(nav.Back());
        Assert.Equal("loadout.module", nav.Trail[^1].Key);
        Assert.True(nav.Modal);
    }

    /// <summary>
    /// The module picker names its own page rather than inheriting the slot's, which is about
    /// engineering a module rather than about choosing one.
    /// </summary>
    [Fact]
    public void TheModulePickerAndTheAdventureEditorHavePagesOfTheirOwn()
    {
        foreach (var (id, title) in new[]
                 {
                     (ShipsMode.ModuleChoiceHelp, "Choosing a module"),
                     (AdventuresPage.EditHelp, "Writing an adventure"),
                 })
        {
            var article = HelpLibrary.For(id);

            Assert.True(article is not null, $"{id} has no band");
            Assert.Equal(title, article!.Title);
            Assert.NotEmpty(article.Sections);
        }

        // Not the slot's page.
        Assert.NotEqual(D47.Core.Capabilities.Builtin.EngineeringCapability.Id, ShipsMode.ModuleChoiceHelp);
    }
}
