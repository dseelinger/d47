using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>Shows a control in a window under the Elite theme with the control-kit styles included.</summary>
internal static class KitWindow
{
    /// <summary>The page in a themed window of the given size.</summary>
    public static Window Show(Control page, double width, double height)
    {
        IncludeKit();

        var window = new Window
        {
            Content = page,
            Width = width,
            Height = height,
            Background = (IBrush?)Application.Current!.Resources[ThemeManager.BgKey],
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>The content at the top of a 600 by 300 window, on the default background.</summary>
    public static Window ShowInStrip(Control content)
    {
        IncludeKit();

        var window = new Window
        {
            Content = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Children = { content } },
            Width = 600,
            Height = 300,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    // The headless app leaves the control kit out, so the Button theme comes from the real file.
    private static void IncludeKit()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });
    }
}
