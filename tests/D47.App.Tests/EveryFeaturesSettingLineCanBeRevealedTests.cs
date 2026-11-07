using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Settings;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Coverage;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every settings line on the Features page has a row the Go button can reveal.</summary>
[Trait("Category", "Gate")]
public sealed class EveryFeaturesSettingLineCanBeRevealedTests
{
    [AvaloniaFact]
    public void EverySettingItemHasARowOnTheSettingsPageOrAStrip()
    {
        using var look = AppLook.Put();
        var (settings, viewState, _, registry, _) = TestSurface.CreateFull(coverage: () => "x");
        var page = new SettingsView();

        page.Attach(settings, viewState);

        var window = new Window { Content = page, Width = 1180, Height = 880 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var settingItems = CoverageInventory.Of(registry).Where(item => item.Kind == CoverageKind.Setting).ToList();
        var unreachable = settingItems
            .Where(item => !OnAStrip(item.Id) && !OnThePageBar(registry, item.Id) && !Revealed(window, page, item.Id))
            .Select(item => item.Id)
            .ToList();

        Assert.NotEmpty(settingItems);
        Assert.True(unreachable.Count == 0, "No row to reveal: " + string.Join(", ", unreachable));

        window.Close();
    }

    /// <summary>
    /// What the Go button does: reveal the row, which marks its frame when the page draws one. Counted only
    /// when the reveal marks a control that was not drawn in the mark's ink before it.
    /// </summary>
    private static bool Revealed(Window window, SettingsView page, string key)
    {
        // A press takes the last key's mark off; off the window, no control is in its hover ink.
        window.MouseDown(new Point(600, 10), MouseButton.Left);
        window.MouseUp(new Point(600, 10), MouseButton.Left);
        window.MouseMove(new Point(-50, -50));
        Dispatcher.UIThread.RunJobs();

        var before = page.Marked().ToHashSet();

        page.RevealRow(key);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        return page.Marked().Any(control => !before.Contains(control));
    }

    /// <summary>A page-top toggle sits in the settings page's bar, which shows whenever the page does.</summary>
    private static bool OnThePageBar(CapabilityRegistry registry, string key) =>
        registry.All.SelectMany(capability => capability.Descriptor.Settings).Any(row => row.Key == key && row.PageTop);

    private static bool OnAStrip(string key) =>
        SettingsLayout.Tabs.Any(tab => tab.Entries.Any(entry => entry.Key == key || entry.Family?.Invoke(key) == true));
}
