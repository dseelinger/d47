using D47.Core.Configuration;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Remembers the Galaxy map's chart kinds, which are ticked and the travels range, shared by every surface.</summary>
public sealed class GalaxyMapLayersMemory(ViewStateStore store)
{
    /// <summary>The layers as last saved; the default kinds on the map and ticked, with the range at All, when nothing is saved.</summary>
    public GalaxyMapLayers Read()
    {
        var state = store.Load();
        var removed = Kinds(state.GalaxyMapRemoved).ToHashSet();
        var added = Kinds(state.GalaxyMapAdded).ToHashSet();

        return new GalaxyMapLayers(
            onMap: StarCharts.All.Where(kind => StarCharts.Default.Contains(kind) ? !removed.Contains(kind) : added.Contains(kind)),
            unticked: Kinds(state.GalaxyMapUnticked),
            range: state.GalaxyMapRange);
    }

    public void Write(GalaxyMapLayers layers) =>
        store.Save(store.Load() with
        {
            GalaxyMapRemoved = [.. layers.Removed.Select(kind => kind.ToString())],
            GalaxyMapAdded = [.. layers.Added.Select(kind => kind.ToString())],
            GalaxyMapUnticked = [.. layers.Unticked.Order().Select(kind => kind.ToString())],
            GalaxyMapRange = layers.Range,
        });

    private static IEnumerable<StarChartKind> Kinds(IEnumerable<string> names) =>
        names.Select(name => Enum.TryParse<StarChartKind>(name, out var kind) ? (StarChartKind?)kind : null).OfType<StarChartKind>();
}
