using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Writing a core of your own is reachable from the panel (remediation.md 11, item 9).
/// <para>
/// The store, the editor window and the catalogue wiring all shipped built and tested, and the
/// settings row that reaches them was never declared. <c>PersonaCapability.OwnKey</c> named a row
/// nothing carried, the branch in <c>SettingsView</c> that draws the button could therefore never
/// match, and the only way to write a core was to write <c>personas.json</c> by hand and restart.
/// </para>
/// <para>
/// Driven through the real settings page for the reason
/// <see cref="ARescanIsOfferedWhereTheDoubtIsTests"/> records: this row draws its button only when
/// the store was handed to the view, so a probe of Core goes on passing on the day it is absent.
/// </para>
/// </summary>
public class AWrittenCoreIsReachableFromThePanelTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-own-personas-panel", Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        // The catalogue's source is static, so a test that left one pointing at a deleted folder
        // would hand every later test somebody else's cores.
        PersonaCatalog.Own = null;

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>The row names what is already written, and the button opens the editor.</b> Both
    /// halves: a button with nothing above it cannot answer <i>what have I got</i>, which is the
    /// question in front of somebody who wrote a core three months ago.
    /// </summary>
    [AvaloniaFact]
    public void TheRowNamesTheCoresAndOffersTheEditor()
    {
        var store = Written();

        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, ownPersonas: store);

        var button = host.View.GetVisualDescendants().OfType<Button>()
            .Single(found => found.Name == "OpenOwnPersonas");

        Assert.Equal("Write a core", button.Content);

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<SelectableTextBlock>(),
            text => text.Text?.Contains("Rusty", StringComparison.Ordinal) == true);

        host.Close();
    }

    /// <summary>
    /// <b>And the editor's legend sits across the top rather than down the side.</b> A
    /// <c>DockPanel</c> child with no dock takes <c>Left</c>, and the legend added by #253 had
    /// none — so it stood in a column of its own down the left of the window, clipped at its own
    /// width, with every card pushed sideways to clear it. Nothing caught it because nothing had
    /// ever opened this window: the row that opens it was the one that was never declared.
    /// </summary>
    [AvaloniaFact]
    public void TheEditorsCardsGetTheWidthOfTheWindow()
    {
        var editor = new PersonaWindow(Written()) { Width = 680, Height = 620 };

        editor.Show();
        Dispatcher.UIThread.RunJobs();

        var card = editor.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Child is StackPanel);

        // Docked sideways the card measured 440 of the window's 680. Across the top it gets all
        // of it bar the margins and the scroller.
        Assert.True(card.Bounds.Width > 600, $"the card was {card.Bounds.Width:0} wide");

        editor.Close();
    }

    /// <summary>One core, in a store of this test's own.</summary>
    private OwnPersonaStore Written()
    {
        Directory.CreateDirectory(_folder);

        var store = new OwnPersonaStore(
            Path.Combine(_folder, "personas.json"),
            NullLogger<OwnPersonaStore>.Instance);

        PersonaCatalog.Own = () => [.. store.Cores.Select(core => core.AsPersona())];

        store.Save([new OwnPersona("own.rusty", "Rusty", "You are Rusty. Salvage crew, not a Guardian.")]);

        return store;
    }
}
