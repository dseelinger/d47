using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Diagnostics;
using Xunit;

namespace D47.App.Tests;

/// <summary>The per-subsystem level track's interaction (#283): one press sets a level, the same
/// press again hands the row back, and the count chip and an inheriting row's readout follow.</summary>
public class PressingAStopSetsOrClearsASubsystemsLevelTests
{
    private static (SettingsView View, Window Window, D47.Core.Configuration.SettingsService Settings) Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        viewState.Save(viewState.Load().With("log-levels", expanded: true));

        var view = new SettingsView();
        view.Attach(settings, viewState, paths, tabPlaceId: "log-levels");

        var window = new Window { Content = view, Width = 900, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (view, window, settings);
    }

    private static Button StopFor(SettingsView view, string subsystem, string level) =>
        view.GetVisualDescendants().OfType<Button>()
            .First(b => AutomationProperties.GetName(b) == $"{Subsystems.DisplayName(subsystem)}: {level}");

    [AvaloniaFact]
    public void APressSetsTheLevelAndTheSamePressHandsItBack()
    {
        var (view, window, settings) = Open();

        var stop = StopFor(view, Subsystems.App, "Trace");

        stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(settings.Current.Logging.Subsystems.TryGetValue(Subsystems.App, out var level));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Trace, level);

        stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(settings.Current.Logging.Subsystems.ContainsKey(Subsystems.App));

        window.Close();
    }

    [AvaloniaFact]
    public void TheResetButtonHandsARowBack()
    {
        var (view, window, settings) = Open();

        StopFor(view, Subsystems.Voice, "Debug").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(settings.Current.Logging.Subsystems.ContainsKey(Subsystems.Voice));

        var reset = view.GetVisualDescendants().OfType<Button>()
            .First(b => AutomationProperties.GetName(b) == $"Reset {Subsystems.DisplayName(Subsystems.Voice)}");

        reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(settings.Current.Logging.Subsystems.ContainsKey(Subsystems.Voice));

        window.Close();
    }

    [AvaloniaFact]
    public void TheCountChipCountsRowsWithTheirOwnLevel()
    {
        var (view, window, settings) = Open();

        var chip = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "NONE");

        StopFor(view, Subsystems.App, "Debug").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal($"1 OF {Subsystems.All.Count}", chip.Text);

        StopFor(view, Subsystems.Journal, "Warning").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal($"2 OF {Subsystems.All.Count}", chip.Text);

        window.Close();
    }
}
