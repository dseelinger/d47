using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>
/// What <see cref="D47.App.App"/> sets up and <see cref="HeadlessApp"/> does not: ControlKitTheme.axaml,
/// the Dark variant and a theme's palette. Each is put onto <see cref="Application.Current"/> for the
/// caller's duration and taken off again on Dispose.
/// </summary>
public static class AppLook
{
    /// <summary>The panel size the captures use.</summary>
    public const double CaptureWidth = 1024;

    public const double CaptureHeight = 640;

    /// <summary>Merges ControlKitTheme.axaml onto Application.Current, removed again on Dispose.</summary>
    public static IDisposable ControlKit()
    {
        var include = new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        };

        Application.Current!.Styles.Add(include);

        return new Undo(() => Application.Current!.Styles.Remove(include));
    }

    /// <summary>
    /// The control kit, the Dark variant and <paramref name="themeId"/>'s palette, as the app starts with
    /// them. Dispose restores the application's resources and variant to what they were.
    /// </summary>
    /// <param name="matrix">
    /// The HUD matrix for <see cref="ThemeCatalog.ElitePaletteId"/>; null reads this machine's own.
    /// </param>
    public static IDisposable Put(string themeId = ThemeCatalog.Elite, GuiColourMatrix? matrix = null)
    {
        var application = Application.Current!;
        var resources = application.Resources;
        var before = resources.ToDictionary(pair => pair.Key, pair => pair.Value);
        var variant = application.RequestedThemeVariant;

        var kit = ControlKit();

        application.RequestedThemeVariant = ThemeVariant.Dark;
        new ThemeManager(application, NullLogger<ThemeManager>.Instance).Apply(themeId, matrix);

        return new Undo(() =>
        {
            kit.Dispose();

            foreach (var key in resources.Keys.Where(key => !before.ContainsKey(key)).ToList())
            {
                resources.Remove(key);
            }

            foreach (var (key, value) in before)
            {
                resources[key] = value;
            }

            application.RequestedThemeVariant = variant;
        });
    }

    /// <summary>Renders <paramref name="content"/> in a window under <see cref="Put"/> and returns the frame.</summary>
    public static Avalonia.Media.Imaging.Bitmap Render(
        Control content,
        string themeId = ThemeCatalog.Elite,
        double width = CaptureWidth,
        double height = CaptureHeight,
        GuiColourMatrix? matrix = null)
    {
        using var look = Put(themeId, matrix);

        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame()!;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        return frame;
    }

    /// <summary>
    /// Renders <paramref name="content"/>, saves the frame as <paramref name="fileName"/> through
    /// <see cref="TestSurface.SaveCapture"/> and returns it for the caller to dispose.
    /// </summary>
    public static Avalonia.Media.Imaging.Bitmap Capture(
        Control content,
        string fileName,
        string themeId = ThemeCatalog.Elite,
        double width = CaptureWidth,
        double height = CaptureHeight,
        GuiColourMatrix? matrix = null)
    {
        var frame = Render(content, themeId, width, height, matrix);
        frame.SaveCapture(fileName);
        return frame;
    }

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
