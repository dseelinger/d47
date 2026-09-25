using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The row that resets every voice to its pairing is a hazard on a shared setting, so a first press
/// only asks and the second one inside a short window is what actually does it (#85).
/// </summary>
public class AVoiceResetAsksTwiceThenActsTests
{
    private static Button Press(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == $"Press_{SpeechCapability.ResetVoicesKey.Replace('.', '_')}");

    /// <summary>
    /// Every non-empty line on the page — an Info row's own is a stack rather than the label-and-
    /// control grid a Choice row draws, so there is no single row container to scope this to.
    /// </summary>
    private static string PageText(SettingsHost host) =>
        string.Join(
            " | ",
            host.View.GetVisualDescendants().OfType<TextBlock>()
                .Select(text => text.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static SettingsHost Open(Func<string> resetVoices)
    {
        var (settings, viewState, paths) = TestSurface.Create(resetVoices: resetVoices);
        var host = SettingsHost.Open(settings, viewState, paths);
        Dispatcher.UIThread.RunJobs();

        host.View.ShowPlaceOf(SpeechCapability.ResetVoicesKey);
        Dispatcher.UIThread.RunJobs();

        return host;
    }

    /// <summary>A single press changes nothing.</summary>
    [AvaloniaFact]
    public void OnePressAsksAndDoesNothingElse()
    {
        var host = Open(() => "RESET-OUTCOME: one core restored.");

        var button = Press(host);
        Click(button);

        Assert.Equal("Press again to confirm", button.Content as string);
        Assert.DoesNotContain("RESET-OUTCOME", PageText(host), StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>The second press, inside the window, is what actually resets the voices.</summary>
    [AvaloniaFact]
    public void ASecondPressWithinTheWindowResets()
    {
        var calls = 0;
        var host = Open(() =>
        {
            calls++;
            return "RESET-OUTCOME: one core restored.";
        });

        var button = Press(host);
        Click(button);
        Click(button);

        Assert.Equal(1, calls);
        Assert.Equal("Forget and pair again", button.Content as string);
        Assert.Contains("RESET-OUTCOME", PageText(host), StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>Waiting past the window lapses the ask back to the first state, on its own.</summary>
    [AvaloniaFact]
    public async Task WaitingPastTheWindowAsksAgainRatherThanActing()
    {
        var calls = 0;
        var host = Open(() =>
        {
            calls++;
            return "RESET-OUTCOME: one core restored.";
        });

        var button = Press(host);
        Click(button);

        await Task.Delay(
            SettingsView.ConfirmPressWindow + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Forget and pair again", button.Content as string);

        Click(button);

        Assert.Equal(0, calls);
        Assert.Equal("Press again to confirm", button.Content as string);

        host.Close();
    }
}
