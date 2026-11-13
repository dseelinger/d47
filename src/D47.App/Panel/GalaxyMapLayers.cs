using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Which chart kinds are on the Galaxy map, which of those are ticked, and the travels range.</summary>
public sealed class GalaxyMapLayers(IEnumerable<StarChartKind>? onMap = null, IEnumerable<StarChartKind>? unticked = null, int range = GalaxyMapLayers.AllTime)
{
    public const int AllTime = 2;

    private readonly HashSet<StarChartKind> _unticked = [.. unticked ?? []];

    /// <summary>The kinds on the map, in <see cref="StarChartKind"/> order.</summary>
    public IReadOnlyList<StarChartKind> OnMap { get; private set; } = InOrder(onMap ?? StarCharts.All);

    /// <summary>The travels range: 0 for 30 days, 1 for 1 year, 2 for all.</summary>
    public int Range { get; set; } = range is >= 0 and <= AllTime ? range : AllTime;

    /// <summary>The kinds the Commander has unticked.</summary>
    public IReadOnlyCollection<StarChartKind> Unticked => _unticked;

    /// <summary>The kinds not on the map, in <see cref="StarChartKind"/> order.</summary>
    public IEnumerable<StarChartKind> Removed => StarCharts.All.Where(kind => !OnMap.Contains(kind));

    public bool Ticked(StarChartKind kind) => OnMap.Contains(kind) && !_unticked.Contains(kind);

    public void Tick(StarChartKind kind, bool ticked)
    {
        if (ticked)
        {
            _unticked.Remove(kind);
        }
        else
        {
            _unticked.Add(kind);
        }
    }

    /// <summary>Puts exactly <paramref name="kinds"/> on the map; a kind not on it before comes in ticked.</summary>
    public void Choose(IEnumerable<StarChartKind> kinds)
    {
        var chosen = InOrder(kinds);

        _unticked.RemoveWhere(kind => !OnMap.Contains(kind) || !chosen.Contains(kind));
        OnMap = chosen;
    }

    private static StarChartKind[] InOrder(IEnumerable<StarChartKind> kinds) => [.. kinds.Distinct().Order()];
}
