using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Help;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The question mark on a settings card (asked for 2026-08-23).
/// <para>
/// <b>It used to launch a browser.</b> Reported against 0.57.0 with a picture of the Listening
/// card: the mark took the Commander out of the panel and away from the row they were reading,
/// with no way back to it — and on a surface with no browser it did nothing they could see at
/// all. Drawn as a level instead, going back is the breadcrumb, and the site is still reachable
/// from the card at the foot of what it draws.
/// </para>
/// </summary>
public class ACardsQuestionMarkDrawsHelpTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(settings, viewState, paths);
    }

    /// <summary>
    /// The mark on the card whose heading says this. There is one per card rather than one per
    /// row, so finding it by the heading beside it is finding the one the Commander pressed.
    /// </summary>
    private static Button Mark(SettingsView view, string heading)
    {
        var card = ((StackPanel)view.FindControl<Control>("Cards")!).Children
            .OfType<Border>()
            .First(border => border.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == heading));

        return card.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "?");
    }

    private static void Click(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Pressed on Listening, it draws the Listening band — that card's own subject, not the page
    /// about Settings that the tab's mark opens.
    /// </summary>
    [AvaloniaFact]
    public void ItDrawsThatCardsOwnPageInThePanel()
    {
        var host = Open();

        Click(Mark(host.View, "Listening"));
        Jobs();

        Assert.True(host.Panel.Nav.Modal, "help took the panel");
        Assert.Equal(HelpLevel.Prefix + ListeningCapability.Id, host.Panel.Nav.Trail[^1].Key);

        // The band, not the settings page it was pressed from.
        var shown = host.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.Contains(shown, text => text.StartsWith("Whisper turns your voice", StringComparison.Ordinal));

        host.Close();
    }

    /// <summary>
    /// <b>And there is a way back to the row it was pressed from.</b> This is the whole
    /// complaint: a browser is a one-way trip out of the panel. The crumb, the controller button
    /// and the spoken word are one method, so asserting Back is asserting all three.
    /// </summary>
    [AvaloniaFact]
    public void TheBreadcrumbGoesBackToTheSettingsPage()
    {
        var host = Open();

        Assert.Equal("Settings", host.Panel.Nav.Trail[^1].Word);

        Click(Mark(host.View, "Listening"));
        Jobs();

        Assert.Equal("Help", host.Panel.Nav.Trail[^1].Word);

        Assert.True(host.Panel.GoBack());
        Jobs();

        Assert.False(host.Panel.Nav.Modal, "help was dismissed");
        Assert.Equal(PanelTab.Settings, host.Panel.Tab);
        Assert.Equal("Settings", host.Panel.Nav.Trail[^1].Word);

        host.Close();
    }

    /// <summary>
    /// What it draws ends with the way out to the site, named for where it goes. The panel draws
    /// the band and nothing beneath it, so the tables, the schemas and the working are only
    /// there — a page with no way through would quietly hide the documentation.
    /// </summary>
    [AvaloniaFact]
    public void WhatItDrawsOffersTheLongFormOnline()
    {
        var host = Open();

        Click(Mark(host.View, "Listening"));
        Jobs();

        var shown = host.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.Contains("More details online", shown);

        host.Close();
    }

    /// <summary>
    /// A card whose page nobody has illustrated still opens something, taking the same ladder
    /// down that the tab's own mark takes. <c>privacy</c> is the one left, asked of the library
    /// rather than named, so writing that band does not turn this red.
    /// </summary>
    [AvaloniaFact]
    public void ACardWithNoBandOpensTheIndexRatherThanNothing()
    {
        var bandless = HelpLibrary.Pages.First(id => HelpLibrary.For(id) is null);
        var host = Open();

        var heading = ((StackPanel)host.View.FindControl<Control>("Cards")!).Children
            .OfType<Border>()
            .Select(card => card.GetVisualDescendants().OfType<TextBlock>().First().Text)
            .ToList();

        // Only meaningful while that page is a section on this surface; skip rather than assert
        // something about a capability that declares no settings.
        if (!heading.Any(text => string.Equals(text, "Privacy", StringComparison.Ordinal))
            || !string.Equals(bandless, "privacy", StringComparison.Ordinal))
        {
            host.Close();
            return;
        }

        Click(Mark(host.View, "Privacy"));
        Jobs();

        Assert.True(host.Panel.Nav.Modal, "the mark always opens something");
        Assert.Equal(HelpLevel.Prefix + HelpLevel.Index, host.Panel.Nav.Trail[^1].Key);

        host.Close();
    }

    /// <summary>
    /// <b>A chooser no longer makes the mark inert.</b> Reported 2026-08-23 against the module
    /// picker as "there's no help for this page": it had none twice over — the mark inherited the
    /// slot's engineering page rather than naming its own, and would not have drawn even that,
    /// because help refused to open while a chooser held the panel.
    /// <para>
    /// Asserted here at the navigator, which is where the refusal lived, rather than by driving
    /// Loadout to a slot — the claim is about every chooser, and the module picker is one.
    /// </para>
    /// </summary>
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

        // Not the slot's page, which is what the mark used to inherit.
        Assert.NotEqual(D47.Core.Capabilities.Builtin.EngineeringCapability.Id, ShipsMode.ModuleChoiceHelp);
    }
}
