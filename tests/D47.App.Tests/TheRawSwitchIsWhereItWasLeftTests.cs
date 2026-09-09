using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The journal's Raw switch, kept where the Commander left it across launches.</summary>
public sealed class TheRawSwitchIsWhereItWasLeftTests
{
    [AvaloniaFact]
    public void TheSwitchGoesBackOnWhereItWasLeftOn()
    {
        var store = Store();

        var (first, window) = Shown(store);

        first.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Toggle(first).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.RawJournal, first.Page);

        window.Close();

        // A second panel over the same store, which is what the next launch has.
        var (next, second) = Shown(store);

        next.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.RawJournal, next.Page);
        Assert.True(Toggle(next).IsChecked);

        second.Close();
    }

    /// <summary>Both directions are one fact.</summary>
    [AvaloniaFact]
    public void TurningItOffIsRememberedToo()
    {
        var store = Store();

        store.Save(store.Load() with { JournalRaw = true });

        var (first, window) = Shown(store);

        first.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.RawJournal, first.Page);

        // And off again.
        Toggle(first).IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Journal, first.Page);

        window.Close();

        var (next, second) = Shown(store);

        next.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Journal, next.Page);
        Assert.False(Toggle(next).IsChecked);

        second.Close();
    }

    [AvaloniaFact]
    public void LaunchDoesNotOpenOnTheRawReading()
    {
        var store = Store();

        store.Save(store.Load() with { JournalRaw = true });

        var (panel, window) = Shown(store);

        Assert.Equal(TranscriptPage.Conversation, panel.Page);
        Assert.Equal(PanelTab.Transcript, panel.Tab);

        window.Close();
    }

    /// <summary>
    /// Going to the log file and back is not a way in either: the memory is about the journal reading
    /// and touches nothing else.
    /// </summary>
    [AvaloniaFact]
    public void AnotherReadingIsLeftAlone()
    {
        var store = Store();

        store.Save(store.Load() with { JournalRaw = true });

        var (panel, window) = Shown(store);

        panel.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Log, panel.Page);

        window.Close();
    }

    /// <summary>
    /// A surface nobody furnished the raw reading on has no switch to put back, and asking it to
    /// remember one raises nothing.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithoutTheRawReadingIsUntouched()
    {
        var store = Store();

        store.Save(store.Load() with { JournalRaw = true });

        var (panel, window) = Shown(store, rawJournal: false);

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Journal, panel.Page);

        window.Close();
    }

    /// <summary>
    /// A Commander parked on Raw Journal who switches to another tab's root must not see the switch
 /// follow them there.
    /// </summary>
    [AvaloniaFact]
    public void TheSwitchDoesNotBleedIntoAnotherTabsRoot()
    {
        var store = Store();

        var (panel, window) = Shown(store);

        // A single root, the same shape as Fleet's Ships root in the report: no picker of its own to hide
        // behind.
        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"));

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Toggle(panel).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<StackPanel>("RawToggleBox").IsVisible);

        window.Close();
    }

    private static ToggleButton Toggle(PanelView panel) =>
        panel.GetControl<ToggleButton>("RawToggle");

    private static ViewStateStore Store() =>
        new(
            new AppPaths(TempFolders.Create("d47-raw-switch-tests")),
            NullLogger<ViewStateStore>.Instance);

    private static (PanelView Panel, Window Window) Shown(ViewStateStore store, bool rawJournal = true)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (rawJournal)
        {
            panel.EnableRawJournal();
        }

        panel.RememberJournalReading(new JournalReadingMemory(store));

        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }
}
