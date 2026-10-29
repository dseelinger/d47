using Avalonia.Headless.XUnit;
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
        var (settings, viewState, _, registry, _) = TestSurface.CreateFull(coverage: () => "x");
        var page = new SettingsView();

        page.Attach(settings, viewState);

        var unreachable = CoverageInventory.Of(registry)
            .Where(item => item.Kind == CoverageKind.Setting)
            .Where(item => !page.ShowPlaceOf(item.Id) && !OnAStrip(item.Id) && !OnThePageBar(registry, item.Id))
            .Select(item => item.Id)
            .ToList();

        Assert.True(unreachable.Count == 0, "No row to reveal: " + string.Join(", ", unreachable));
    }

    /// <summary>A page-top toggle sits in the settings page's bar, which shows whenever the page does.</summary>
    private static bool OnThePageBar(CapabilityRegistry registry, string key) =>
        registry.All.SelectMany(capability => capability.Descriptor.Settings).Any(row => row.Key == key && row.PageTop);

    private static bool OnAStrip(string key) =>
        SettingsLayout.Tabs.Any(tab => tab.Entries.Any(entry => entry.Key == key || entry.Family?.Invoke(key) == true));
}
