using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using D47.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace D47.App.Tests;

public class TestAppBuilder
{
    private static readonly MethodInfo ResetDispatcher =
        typeof(Dispatcher).GetMethod("ResetBeforeUnitTests", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Avalonia no longer has Dispatcher.ResetBeforeUnitTests.");

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder
            .Configure<HeadlessApp>()
            .UseSkia()
            .WithInterFont()
            // Real Skia rendering rather than the null drawing backend, so a test can capture what a frame
            // actually looks like — which is how the settings surface gets eyes on it in CI instead of only on a
            // Commander's desktop.
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

        var runtime = builder.RuntimePlatformServicesInitializer;
        return builder.UseRuntimePlatformSubsystem(
            () =>
            {
                ClaimTheDispatcher();
                runtime?.Invoke();
            },
            builder.RuntimePlatformServicesName ?? string.Empty);
    }

    /// <summary>
    /// Makes the setting-up thread the dispatcher's UI thread. The session resets the dispatcher before each
    /// test, and any other thread that reads <see cref="Dispatcher.UIThread"/> first would own it.
    /// </summary>
    private static void ClaimTheDispatcher()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                return;
            }

            ResetDispatcher.Invoke(null, null);
        }

        Dispatcher.UIThread.VerifyAccess();
    }
}

/// <summary>
/// A minimal application standing in for <see cref="D47.App.App"/>: same Fluent theme, no AppHost, no
/// windows of its own.
/// </summary>
public class HeadlessApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // The same scrollbar template App.axaml adds after FluentTheme.
        Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ScrollViewerTheme.axaml"),
            });

        // The same scale App.axaml merges.
        Resources.MergedDictionaries.Add(
            new Avalonia.Markup.Xaml.Styling.ResourceInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/TypeScale.axaml"),
            });

        // The same families App.axaml merges.
        Resources.MergedDictionaries.Add(
            new Avalonia.Markup.Xaml.Styling.ResourceInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/Fonts.axaml"),
            });
    }
}
