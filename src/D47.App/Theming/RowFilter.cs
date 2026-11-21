namespace D47.App.Theming;

/// <summary>One block of values a field's cells carry: Economy, or Security under it.</summary>
/// <param name="Label">The block's heading.</param>
/// <param name="Values">The values a row's cell carries; none when the cell has no value.</param>
/// <param name="NoValue">The entry that counts the rows with no value.</param>
public sealed record ValueList<TRow>(string Label, Func<TRow, IReadOnlyList<string>> Values, string NoValue);

/// <summary>One value of a block with the rows that carry it.</summary>
public sealed record FilterValue(string Value, int Count, bool Ticked);

/// <summary>One block of a field as the filter lists it.</summary>
public sealed record FilterBlock(string FieldKey, int Index, string FieldLabel, string Label, IReadOnlyList<FilterValue> Values);

/// <summary>The unticked values of a table's blocks, and the rows they hide. Holds no controls.</summary>
internal sealed class RowFilter<TRow>
{
    public const int ManyValues = 20;

    private readonly List<Block> _blocks = [];

    public RowFilter(IEnumerable<(string Key, string Label, IReadOnlyList<ValueList<TRow>> Lists)> fields)
    {
        foreach (var (key, label, lists) in fields)
        {
            for (var i = 0; i < lists.Count; i++)
            {
                _blocks.Add(new Block(key, i, label, lists[i]));
            }
        }
    }

    public bool Any => _blocks.Count > 0;

    public IReadOnlyList<FilterBlock> Blocks => [.. _blocks.Select(block => block.View())];

    public IReadOnlyList<string> FilteredLabels =>
        [.. _blocks.Where(block => block.Unticked.Count > 0).Select(block => block.FieldLabel).Distinct(StringComparer.Ordinal)];

    public bool Many(string key) => _blocks.Any(block => block.FieldKey == key && block.Listed.Count > ManyValues);

    public IReadOnlyList<TRow> Filter(IReadOnlyList<TRow> rows)
    {
        var active = _blocks.Where(block => block.Unticked.Count > 0).ToList();

        return active.Count == 0 ? rows : [.. rows.Where(row => active.All(block => block.Lets(row)))];
    }

    public void Observe(IReadOnlyList<TRow> rows)
    {
        foreach (var block in _blocks)
        {
            block.Observe(rows);
        }
    }

    public bool Set(string key, int index, string value, bool ticked) =>
        Find(key, index)?.Set(value, ticked) == true;

    public bool SetAll(string key, int index, bool ticked) =>
        Find(key, index)?.SetAll(ticked) == true;

    public bool Clear()
    {
        var changed = _blocks.Any(block => block.Unticked.Count > 0 || block.Listed.Count > 0);

        foreach (var block in _blocks)
        {
            block.Forget();
        }

        return changed;
    }

    private Block? Find(string key, int index) =>
        _blocks.FirstOrDefault(block => block.FieldKey == key && block.Index == index);

    private sealed class Block(string fieldKey, int index, string fieldLabel, ValueList<TRow> list)
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        public string FieldKey { get; } = fieldKey;

        public int Index { get; } = index;

        public string FieldLabel { get; } = fieldLabel;

        public List<string> Listed { get; } = [];

        public HashSet<string> Unticked { get; } = new(StringComparer.Ordinal);

        public FilterBlock View() => new(
            FieldKey,
            Index,
            FieldLabel,
            list.Label,
            [.. Listed.Select(value => new FilterValue(value, _counts.GetValueOrDefault(value), !Unticked.Contains(value)))]);

        public bool Lets(TRow row)
        {
            var values = list.Values(row);

            return values.Count == 0 ? !Unticked.Contains(list.NoValue) : values.Any(value => !Unticked.Contains(value));
        }

        public void Observe(IReadOnlyList<TRow> rows)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var values = list.Values(row);

                foreach (var value in values.Count == 0 ? [list.NoValue] : values.Distinct(StringComparer.Ordinal))
                {
                    counts[value] = counts.GetValueOrDefault(value) + 1;
                }
            }

            _counts.Clear();

            foreach (var (value, count) in counts)
            {
                _counts[value] = count;
            }

            Listed.AddRange(counts.Keys
                .Where(value => !Listed.Contains(value, StringComparer.Ordinal))
                .OrderByDescending(value => counts[value]));

            if (Listed.Remove(list.NoValue))
            {
                Listed.Add(list.NoValue);
            }
        }

        public bool Set(string value, bool ticked) =>
            Listed.Contains(value, StringComparer.Ordinal) && (ticked ? Unticked.Remove(value) : Unticked.Add(value));

        public bool SetAll(bool ticked)
        {
            if (ticked)
            {
                var any = Unticked.Count > 0;
                Unticked.Clear();

                return any;
            }

            return Listed.Count(value => Unticked.Add(value)) > 0;
        }

        public void Forget()
        {
            Listed.Clear();
            Unticked.Clear();
            _counts.Clear();
        }
    }
}
