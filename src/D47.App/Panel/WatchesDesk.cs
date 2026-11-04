using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Watches;

namespace D47.App.Panel;

/// <summary>What Commander › Watches reads and changes, shared by the window, the mini panel and the headset.</summary>
public sealed class WatchesDesk(
    WatchStore store,
    WatchSetup setup,
    SettingsService settings,
    Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? systemNames = null)
{
    public WatchStore Store => store;

    public WatchSetup Setup => setup;

    public Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? SystemNames => systemNames;

    public bool GalaxySearch => settings.Current.Knowledge.GalaxySearch;

    public bool BgsTick => settings.Current.Knowledge.BgsTick;

    public bool GalNet => settings.Current.Knowledge.GalNet;

    /// <summary>The three gates as one value, so a page can tell when one has changed.</summary>
    public (bool GalaxySearch, bool BgsTick, bool GalNet) Gates => (GalaxySearch, BgsTick, GalNet);

    public void TurnOnGalaxySearch() => Apply(GalaxyCapability.EnabledKey);

    public void TurnOnBgsTick() => Apply(GalaxyCapability.BgsTickKey);

    public void TurnOnGalNet() => Apply(GalaxyCapability.GalNetKey);

    private void Apply(string key) => settings.Apply(key, "true", SettingsCaller.Panel);
}
