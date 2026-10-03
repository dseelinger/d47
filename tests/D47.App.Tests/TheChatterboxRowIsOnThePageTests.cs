using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheChatterboxRowIsOnThePageTests
{
    private static SettingsHost Open(string provider)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with { Speech = current.Speech with { Provider = provider } });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        Dispatcher.UIThread.RunJobs();

        host.View.ShowPlaceOf(SpeechCapability.ProviderKey);
        Dispatcher.UIThread.RunJobs();

        return host;
    }

    private static List<string> Texts(SettingsHost host) =>
        [.. host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible)
            .Select(text => text.Text ?? string.Empty)];

    [AvaloniaFact]
    public void WithChatterboxSelectedTheRowAndItsButtonAreDrawn()
    {
        var host = Open(TtsProviderCatalog.ChatterboxId);

        var texts = Texts(host);
        Assert.Contains("Chatterbox voice", texts);
        Assert.Contains(texts, t => t.Contains("691 MB", StringComparison.Ordinal));

        var buttons = host.View.GetVisualDescendants().OfType<Button>()
            .Select(button => button.Content as string ?? string.Empty)
            .ToList();
        Assert.Contains("Download it", buttons);

        host.Window.Close();
    }

    [AvaloniaFact]
    public void WithoutChatterboxTheRowIsAbsent()
    {
        var host = Open(TtsProviderCatalog.EdgeId);

        Assert.DoesNotContain("Chatterbox voice", Texts(host));

        host.Window.Close();
    }
}
