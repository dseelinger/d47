using D47.Core.Storage;
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

/// <summary>Writing a core of your own is reachable from the panel.</summary>
[Trait("Category", "Integration")]
public class AWrittenCoreIsReachableFromThePanelTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-own-personas-panel", Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        // The catalogue's source is static, so a test that left one pointing at a deleted folder would hand
        // every later test somebody else's cores.
        PersonaCatalog.Own = null;

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>The caption counts what is already written, no list is drawn, and the button opens the editor.</summary>
    [AvaloniaFact]
    public void TheRowCountsTheCoresAndOffersTheEditor()
    {
        var store = Written();

        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, ownPersonas: store);

        // Persona's own area (#220).
        host.View.Reveal(D47.Core.Capabilities.Builtin.PersonaCapability.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var button = host.View.GetVisualDescendants().OfType<Button>()
            .Single(found => found.Name == "OpenOwnPersonas");

        Assert.Equal("Manage", button.Content);

        Assert.Equal("Custom cores (1)", host.View.LabelFor(D47.Core.Capabilities.Builtin.PersonaCapability.OwnKey)?.Text);

        Assert.DoesNotContain(
            host.View.GetVisualDescendants().OfType<SelectableTextBlock>(),
            text => text.Text?.Contains("Rusty", StringComparison.Ordinal) == true);

        host.Close();
    }

    /// <summary>Writing the file while Settings is open moves the count, and with none the row says so.</summary>
    [AvaloniaFact]
    public void TheCountFollowsTheFileWhileSettingsIsOpen()
    {
        var store = Written();
        store.Save([]);

        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, ownPersonas: store);

        host.View.Reveal(D47.Core.Capabilities.Builtin.PersonaCapability.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var button = host.View.GetVisualDescendants().OfType<Button>()
            .Single(found => found.Name == "OpenOwnPersonas");

        Assert.Equal("Custom cores (0)", host.View.LabelFor(D47.Core.Capabilities.Builtin.PersonaCapability.OwnKey)?.Text);
        Assert.Equal("Write a core", button.Content);

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<SelectableTextBlock>(),
            text => text.Text?.StartsWith("None yet.", StringComparison.Ordinal) == true);

        store.Save([new OwnPersona("own.rusty", "Rusty", "You are Rusty."), new OwnPersona("own.vesper", "Vesper", "You are Vesper.")]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("Custom cores (2)", host.View.LabelFor(D47.Core.Capabilities.Builtin.PersonaCapability.OwnKey)?.Text);
        Assert.Equal("Manage", button.Content);

        host.Close();
    }

    /// <summary>And the editor's legend sits across the top rather than down the side.</summary>
    [AvaloniaFact]
    public void TheEditorsCardsGetTheWidthOfTheWindow()
    {
        var editor = new PersonaPage(Written()) { Width = 680, Height = 620 };

        editor.Show();
        Dispatcher.UIThread.RunJobs();

        var card = editor.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Child is StackPanel && border.BorderThickness.Top == 1);

        // Docked sideways the card measured 440 of the window's 680.
        Assert.True(card.Bounds.Width > 600, $"the card was {card.Bounds.Width:0} wide");

        editor.Close();
    }

    /// <summary>One core, in a store of this test's own.</summary>
    private OwnPersonaStore Written()
    {
        Directory.CreateDirectory(_folder);

        var store = new OwnPersonaStore(
            Path.Combine(_folder, "personas.json"), new DiskFileSystem(),
            NullLogger<OwnPersonaStore>.Instance);

        PersonaCatalog.Own = () => [.. store.Cores.Select(core => core.AsPersona())];

        store.Save([new OwnPersona("own.rusty", "Rusty", "You are Rusty. Salvage crew, not a Guardian.")]);

        return store;
    }
}
