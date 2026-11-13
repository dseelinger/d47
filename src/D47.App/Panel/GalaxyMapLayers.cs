using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Which chart kinds are on the Galaxy map, which of those are ticked, and the travels range.</summary>
public sealed class GalaxyMapLayers(IReadOnlyList<StarChartKind>? onMap = null, IEnumerable<StarChartKind>? unticked = null, int range = GalaxyMapLayers.AllTime)
{
    public const int AllTime = 2;

    private readonly HashSet<StarChartKind> _unticked = [.. unticked ?? []];

    public IReadOnlyList<StarChartKind> OnMap { get; } = onMap ?? StarCharts.All;

    /// <summary>The travels range: 0 for 30 days, 1 for 1 year, 2 for all.</summary>
    public int Range { get; set; } = range is >= 0 and <= AllTime ? range : AllTime;

    /// <summary>The kinds the Commander has unticked.</summary>
    public IReadOnlyCollection<StarChartKind> Unticked => _unticked;

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
}
