using D47.Core.Knowledge;

namespace D47.Core.Campaigns;

/// <summary>The systems the last galaxy report fetched this session placed each faction in. Held in memory only.</summary>
public sealed class FactionSystemsSeen
{
    private readonly Lock _gate = new();

    private Dictionary<string, IReadOnlyList<FactionSystem>> _byFaction = new(StringComparer.OrdinalIgnoreCase);

    public void Remember(FactionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        lock (_gate)
        {
            _byFaction = new Dictionary<string, IReadOnlyList<FactionSystem>>(_byFaction, StringComparer.OrdinalIgnoreCase)
            {
                [report.Name] = report.Systems,
            };
        }
    }

    /// <summary>The faction's systems from the last report fetched, or empty.</summary>
    public IReadOnlyList<FactionSystem> For(string faction)
    {
        lock (_gate)
        {
            return _byFaction.GetValueOrDefault(faction) ?? [];
        }
    }
}
