using D47.Core.Configuration;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Remembers the Galaxy map's ticked chart kinds and travels range, shared by every surface.</summary>
public sealed class GalaxyMapLayersMemory(ViewStateStore store)
{
    /// <summary>The layers as last saved; all ticked with the range at All when nothing is saved.</summary>
    public GalaxyMapLayers Read()
    {
        var state = store.Load();

        return new GalaxyMapLayers(
            unticked: state.GalaxyMapUnticked.Select(name => Enum.TryParse<StarChartKind>(name, out var kind) ? (StarChartKind?)kind : null).OfType<StarChartKind>(),
            range: state.GalaxyMapRange);
    }

    public void Write(GalaxyMapLayers layers) =>
        store.Save(store.Load() with
        {
            GalaxyMapUnticked = [.. layers.Unticked.Order().Select(kind => kind.ToString())],
            GalaxyMapRange = layers.Range,
        });
}
