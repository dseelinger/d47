using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>A modal with a text field opens with the cursor in it, so typing needs no click (#1483).</summary>
public sealed class AModalOpensWithTheCursorInItsFieldTests
{
    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Opened()
    {
        var window = new Window { Content = new Grid(), Width = 900, Height = 700 };
        window.Show();

        return window;
    }

    [AvaloniaFact]
    public void FindOpensWithTheCursorInItsField()
    {
        var window = Opened();
        var dialog = new FindDialog(_ => [], _ => { }, "Ctrl+K");

        _ = dialog.Over(window);
        Jobs();

        var field = dialog.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == FindDialog.FieldName);

        Assert.Same(field, window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public void TypingAfterFindOpensFillsTheQuery()
    {
        var window = Opened();
        var dialog = new FindDialog(_ => [], _ => { }, "Ctrl+K");

        _ = dialog.Over(window);
        Jobs();
        window.KeyTextInput("ships");
        Jobs();

        Assert.Equal("ships", dialog.Query);
    }

    [AvaloniaFact]
    public void TheSavedSearchNameFieldOpensWithTheCursorInIt()
    {
        var window = Opened();
        var dialog = new SaveSearchDialog("Galaxy", "tool", new Dictionary<string, string>(), _ => new SavedSearchOutcome(true, string.Empty));

        _ = dialog.Over(window);
        Jobs();

        var field = dialog.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == SaveSearchDialog.NameFieldName);

        Assert.Same(field, window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public void ClosingTheModalReturnsFocusToWhereItWas()
    {
        var window = Opened();
        var before = new TextBox();
        ((Grid)window.Content!).Children.Add(before);
        _ = ModalHost.For(window);
        Jobs();
        before.Focus();
        Assert.Same(before, window.FocusManager!.GetFocusedElement());

        var dialog = new FindDialog(_ => [], _ => { }, "Ctrl+K");

        _ = dialog.Over(window);
        Jobs();
        dialog.Close();
        Jobs();

        Assert.Same(before, window.FocusManager!.GetFocusedElement());
    }
}
