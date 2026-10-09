using D47.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A long press and the Guardian test share one running flag.</summary>
public class OnePressRunsAtATimeTests
{
    [AvaloniaFact]
    public async Task WhileOnePressRunsTheGuardianTestDoesNotStart()
    {
        var running = new TaskCompletionSource<string?>();
        var rescans = 0;
        var tests = 0;

        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(
            rescan: (_, _) =>
            {
                rescans++;
                return running.Task;
            },
            guardianTest: _ =>
            {
                tests++;
                return Task.FromResult<string?>(null);
            });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var controls = new SettingControls(
            new SettingRowHost(settings, new UserControl(), () => false, (_, _, _) => true, () => { }),
            new SettingServices(null, null, null, null, null, null, null, null, null, null, null, [], null));

        var message = new StatusLine();
        var press = controls.Build(settings.Find(ShipsCapability.RescanKey)!, message);
        var guardian = controls.Build(settings.Find(SpeechCapability.GuardianPresetKey)!, message);
        var test = (Button)controls.DrawnByGroup[SpeechCapability.GuardianTestKey];

        var window = new Window { Content = new StackPanel { Children = { press.Control, guardian.Control } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var button = press.Control.GetVisualDescendants().OfType<Button>().Single(found => found.Name == "Press_ships_remembered");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, rescans);
        Assert.Equal(0, tests);

        running.SetResult(null);
        await running.Task;
        Dispatcher.UIThread.RunJobs();

        test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, tests);

        window.Close();
    }
}
