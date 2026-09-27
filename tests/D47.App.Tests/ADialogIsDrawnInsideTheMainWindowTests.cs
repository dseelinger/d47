using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A dialog of 640 or less is drawn as a modal inside the main window, over a scrim that starts under the
/// caption strip, rather than as a window of its own (#521).
/// </summary>
public class ADialogIsDrawnInsideTheMainWindowTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static MainWindow MainWindowShown()
    {
        // The scrim's background is a themed resource; without it applied here the scrim renders with
        // no background and a click on it falls through to whatever sits behind it instead of closing
        // the modal.
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var window = new MainWindow(host: null);

        window.Show();
        Jobs();

        return window;
    }

    private static ConfirmDialog Ask() => new("Forget this ship?", "Its loadout is removed.", "Forget", "Keep");

    private static Border Scrim(ModalDialog dialog) =>
        dialog.GetVisualAncestors().OfType<Border>().First(border => border.Name == ModalHost.ScrimName);

    private static void Press(Window window, Key key)
    {
        var focused = (InputElement?)window.FocusManager!.GetFocusedElement() ?? window;
        focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Jobs();
    }

    [AvaloniaFact]
    public void ItOpensOnTheMainWindowsHostAndNotAsAWindow()
    {
        var window = MainWindowShown();
        var dialog = Ask();

        var shown = dialog.Over(window);
        Jobs();

        Assert.Empty(window.OwnedWindows);
        Assert.Same(dialog, window.Modals.Top);
        Assert.True(window.Modals.IsVisible);

        dialog.Close();
        Jobs();

        Assert.True(shown.IsCompleted);
        Assert.False(window.Modals.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TheScrimCoversThePanelAndNotTheCaptionStrip()
    {
        var window = MainWindowShown();
        var dialog = Ask();

        _ = dialog.Over(window);
        Jobs();

        var scrim = Scrim(dialog);
        var top = scrim.TranslatePoint(new Point(0, 0), window)!.Value;

        Assert.Equal(CaptionStrip.StripHeight, top.Y, precision: 0);
        Assert.Equal(window.Panel.Bounds.Size, scrim.Bounds.Size);

        window.Close();
    }

    [AvaloniaFact]
    public void TheModalIsCentredAndAtMost640Wide()
    {
        var window = MainWindowShown();
        var dialog = Ask();

        _ = dialog.Over(window);
        Jobs();

        var frame = (Border)dialog.GetVisualParent()!;
        var scrim = Scrim(dialog);

        Assert.Equal(ModalHost.FrameName, frame.Name);
        Assert.True(frame.Bounds.Width <= Modal.Width + 2, $"{frame.Bounds.Width} wide");
        Assert.Equal(scrim.Bounds.Width / 2, frame.Bounds.Center.X, precision: 0);
        Assert.Equal(scrim.Bounds.Height / 2, frame.Bounds.Center.Y, precision: 0);

        window.Close();
    }

    [AvaloniaFact]
    public void EscClosesItAndAnswersNo()
    {
        var window = MainWindowShown();

        var answer = Ask().AskAsync(window);
        Jobs();

        Press(window, Key.Escape);

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.False(answer.Result);
        Assert.Null(window.Modals.Top);

        window.Close();
    }

    [AvaloniaFact]
    public void AClickOnTheScrimClosesItButAClickOnTheModalDoesNot()
    {
        var window = MainWindowShown();
        var dialog = Ask();

        var shown = dialog.Over(window);
        Jobs();

        var scrim = Scrim(dialog);

        window.MouseDown(new Point(window.Bounds.Width / 2, window.Modals.Bounds.Height / 2 + CaptionStrip.StripHeight), MouseButton.Left);
        window.MouseUp(new Point(window.Bounds.Width / 2, window.Modals.Bounds.Height / 2 + CaptionStrip.StripHeight), MouseButton.Left);
        Jobs();

        Assert.False(shown.IsCompleted, "a click inside the modal closed it");

        var corner = scrim.TranslatePoint(new Point(4, 4), window)!.Value;
        window.MouseDown(corner, MouseButton.Left);
        window.MouseUp(corner, MouseButton.Left);
        Jobs();

        Assert.True(shown.IsCompleted);

        window.Close();
    }

    [AvaloniaFact]
    public void FocusCannotLeaveTheModalForThePanel()
    {
        var window = MainWindowShown();
        var dialog = Ask();

        _ = dialog.Over(window);
        Jobs();

        var ask = window.Panel.FindControl<TextBox>("AskBox")!;
        ask.Focus();
        Jobs();

        Assert.False(ask.IsFocused);
        var focused = (Visual)window.FocusManager!.GetFocusedElement()!;
        Assert.True(ReferenceEquals(dialog, focused) || dialog.IsVisualAncestorOf(focused));

        window.Close();
    }

    [AvaloniaFact]
    public void ASecondModalOpensOverTheFirstAndClosesAlone()
    {
        var window = MainWindowShown();
        var first = Ask();
        var second = Ask();

        var under = first.Over(window);
        var over = second.Over(window);
        Jobs();

        Assert.Same(second, window.Modals.Top);

        Press(window, Key.Escape);

        Assert.True(over.IsCompleted);
        Assert.False(under.IsCompleted);
        Assert.Same(first, window.Modals.Top);

        window.Close();
    }

    [AvaloniaFact]
    public void TheModalHasNoCaptionStripOrBorderOfItsOwn()
    {
        var window = MainWindowShown();
        var dialog = Ask();

        _ = dialog.Over(window);
        Jobs();

        Assert.IsNotAssignableFrom<Window>(dialog);
        Assert.DoesNotContain(
            dialog.GetVisualDescendants().OfType<Button>(),
            button => button.Name is "Minimize" or "Maximize" || ToolTip.GetTip(button) as string == "Minimize");

        window.Close();
    }
}
