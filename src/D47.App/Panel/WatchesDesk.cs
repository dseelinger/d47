using D47.Core.Knowledge;
using D47.Core.Watches;

namespace D47.App.Panel;

/// <summary>What Commander › Watches reads and changes, shared by the window, the mini panel and the headset.</summary>
public sealed class WatchesDesk(
    WatchStore store,
    WatchSetup setup,
    Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? systemNames = null,
    Func<IGalaxyService?>? galaxy = null)
{
    /// <summary>Whether Galaxy search is on, which a next-system watch needs to speak.</summary>
    public bool GalaxySearchOn => galaxy is null || galaxy() is not null;

    public WatchStore Store => store;

    public WatchSetup Setup => setup;

    public Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? SystemNames => systemNames;
}
