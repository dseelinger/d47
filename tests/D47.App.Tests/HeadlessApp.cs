using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using D47.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace D47.App.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessApp>()
        .UseSkia()
        .WithInterFont()
        // Real Skia rendering rather than the null drawing backend, so a test can capture what
        // a frame actually looks like — which is how the settings surface gets eyes on it in CI
        // instead of only on a Commander's desktop.
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// A minimal application standing in for <see cref="D47.App.App"/>: same Fluent theme, no
/// AppHost, no windows of its own. Tests apply the real <see cref="Theming.ThemeManager"/>
/// against it, so what they render is the shipped palette.
/// </summary>
public class HeadlessApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // The same scale App.axaml merges. Without it every surface that names a role fails to
        // resolve it, which is a load-time throw rather than a wrong size.
        Resources.MergedDictionaries.Add(
            new Avalonia.Markup.Xaml.Styling.ResourceInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/TypeScale.axaml"),
            });
    }
}
