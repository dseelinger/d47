using Avalonia.Controls;
using D47.App.Controls;

namespace D47.App.Tests;

/// <summary>Puts a dialog page on screen by itself, for a test that is about the page and not the panel.</summary>
public static class DialogPageHosting
{
    /// <summary>Shows the page in a window of its own, closed when the page closes.</summary>
    public static Window Show(this DialogPage page, double width = 720, double height = 720)
    {
        var host = new Window { Content = page, Width = width, Height = height };

        page.Closed += (_, _) => host.Close();
        host.Show();

        return host;
    }
}
