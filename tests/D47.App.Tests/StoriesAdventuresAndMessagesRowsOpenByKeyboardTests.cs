using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>The rows on Stories, Adventures and Messages take focus, open on Enter and step with Down (#1515).</summary>
[Trait("Category", "Integration")]
public sealed class StoriesAdventuresAndMessagesRowsOpenByKeyboardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    private static (Window Window, PanelView Panel) Open(string rootKey, MessageStore? messages = null)
    {
        var surface = Build(TriggerKind.Arrive);

        if (messages is not null)
        {
            surface = surface with { Messages = messages };
        }

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 1500, height: 800);
        var panel = host.Panel;
        var window = host.Window;
        panel.EnableAdventures(surface);

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(rootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<Control> Rows(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Focusable && c.Classes.Contains(ListRow.OpensClass) && c.IsEffectivelyVisible)
            .OrderBy(c => c.TranslatePoint(default, panel)!.Value.Y)];

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void EnterOpensTheFocusedRow(Window window, PanelView panel, Control row, string crumbPrefix)
    {
        var before = panel.Nav.Trail.Count;

        row.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Press(window, Key.Enter);

        Assert.True(panel.Nav.Trail.Count > before);
        Assert.StartsWith(crumbPrefix, panel.Nav.Trail[^1].Key, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AStoryCardInTheCatalogueOpensOnEnterAndDownFocusesTheNextCard()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open(StoriesView.RootKey);
        var rows = Rows(panel);

        Assert.True(rows.Count >= 2, $"rows: {rows.Count}");

        rows[0].Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Press(window, Key.Down);
        Assert.Equal(1, Rows(panel).IndexOf((Control)window.FocusManager!.GetFocusedElement()!));

        EnterOpensTheFocusedRow(window, panel, rows[0], StoriesView.ReadPrefix);

        window.Close();
    }

    [AvaloniaFact]
    public void AChapterCardOnAdventuresOpensOnEnter()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open(AdventuresPage.RootKey);
        var rows = Rows(panel);

        Assert.NotEmpty(rows);

        EnterOpensTheFocusedRow(window, panel, rows[0], AdventuresPage.ReadPrefix);

        window.Close();
    }

    [AvaloniaFact]
    public void AMessageOpensOnEnterAndDownFocusesTheNextMessage()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var store = new MessageStore("messages.json", new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
        store.Post("narrator", "First", "One.", Now);
        store.Post("narrator", "Second", "Two.", Now.AddMinutes(1));

        var (window, panel) = Open(MessagesView.RootKey, store);
        var rows = Rows(panel);

        Assert.True(rows.Count >= 2, $"rows: {rows.Count}");

        rows[0].Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Press(window, Key.Down);
        Assert.Equal(1, Rows(panel).IndexOf((Control)window.FocusManager!.GetFocusedElement()!));

        EnterOpensTheFocusedRow(window, panel, rows[0], MessagesView.ReadPrefix);

        window.Close();
    }
}
