using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Controls;

namespace D47.App.Tests;

/// <summary>A picker page on screen in a window of its own, for tests that exercise the page alone.</summary>
internal static class PickerPageHost
{
    /// <summary>Puts the page in a shown window, so its template and handlers are live.</summary>
    public static Window Show(this PickerPage page, double width = 800, double height = 500)
    {
        var window = new Window { Width = width, Height = height, Content = page };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>Closes the window the page is in, which is what ends anything it was playing.</summary>
    public static void Close(this PickerPage page)
    {
        if (TopLevel.GetTopLevel(page) is Window window)
        {
            window.Close();
        }

        Dispatcher.UIThread.RunJobs();
    }
}
