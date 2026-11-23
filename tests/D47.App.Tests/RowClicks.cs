using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Theming;

namespace D47.App.Tests;

/// <summary>The presses of a double-click on a list row, as the pointer stack would raise them.</summary>
internal static class RowClicks
{
    /// <summary>A left press on <paramref name="row"/> carrying <paramref name="clickCount"/>.</summary>
    public static void Press(Control row, TopLevel root, int clickCount, KeyModifiers modifiers = KeyModifiers.None)
    {
        row.RaiseEvent(new PointerPressedEventArgs(
            row,
            new Pointer(1, PointerType.Mouse, isPrimary: true),
            root,
            new Point(1, 1),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            modifiers,
            clickCount));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The click that follows a press on a <see cref="Button"/>.</summary>
    public static void Click(Button row)
    {
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs the dispatcher until <paramref name="done"/> holds, for up to ten seconds.</summary>
    public static void Until(Func<bool> done)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!done() && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lets three double-click times pass, running the dispatcher as it does.</summary>
    public static void WaitPastDoubleClick()
    {
        var until = DateTime.UtcNow + (ListRow.DoubleClickTime * 3);

        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
