using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers the columns each table shows, by table id; the headset's arrangements are stored apart from the window's.</summary>
public sealed class ColumnArrangementMemory(ViewStateStore store, bool headset = false)
{
    private ViewState? _cached;

    /// <summary>The column keys stored for a table, in order; null when none are.</summary>
    public IReadOnlyList<string>? Remembered(string table)
    {
        _cached ??= store.Load();

        return _cached.ColumnArrangements.TryGetValue(ColumnArrangement.IdFor(table, headset), out var keys) ? keys : null;
    }

    /// <summary>Stores the column keys a table shows, in order.</summary>
    public void Remember(string table, IReadOnlyList<string> keys)
    {
        var next = store.Load().WithArrangement(ColumnArrangement.IdFor(table, headset), keys);

        store.Save(next);
        _cached = next;
    }
}
