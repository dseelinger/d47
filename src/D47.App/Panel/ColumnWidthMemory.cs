using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers the column widths the Commander dragged, as shares of the table's width, by table id and column key (#1612).</summary>
public sealed class ColumnWidthMemory(ViewStateStore store)
{
    private ViewState? _cached;

    /// <summary>The shares remembered for a table, by column key; empty when none.</summary>
    public IReadOnlyDictionary<string, double> Remembered(string table)
    {
        _cached ??= store.Load();

        return _cached.ColumnShares.TryGetValue(table, out var shares)
            ? shares.Where(pair => double.IsFinite(pair.Value) && pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, double>();
    }

    public void Remember(string table, string column, double share)
    {
        if (!double.IsFinite(share) || share <= 0)
        {
            return;
        }

        Save(store.Load().With(table, column, share));
    }

    public void Forget(string table, string column) => Save(store.Load().Without(table, column));

    private void Save(ViewState next)
    {
        store.Save(next);
        _cached = next;
    }
}
