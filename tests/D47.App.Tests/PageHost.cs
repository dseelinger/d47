using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;

namespace D47.App.Tests;

/// <summary>Shows a page in a themed window and disposes the app look when the window closes.</summary>
public static class PageHost
{
    public static (Window Window, T Page) Open<T>(Func<T> make, double width = 1280, double height = 900)
        where T : Control
    {
        var look = AppLook.Put();
        var page = make();
        var window = new Window
        {
            Content = page,
            Width = width,
            Height = height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }
}
