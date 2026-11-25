namespace D47.App.Theming;

public enum ColumnFit
{
    /// <summary>As wide as its widest cell or header label.</summary>
    Content,

    /// <summary>
    /// A name column: names, lists of names or prose. Spare width is shared as for <see cref="Content"/>; when the table is
    /// short of room, only these columns narrow, their text wrapping at word breaks.
    /// </summary>
    Fill,

    /// <summary>A set width.</summary>
    Fixed,
}

/// <summary>One column of a table laid out by <see cref="TableLayout"/>.</summary>
public sealed record TableColumn(string Key, ColumnFit Fit = ColumnFit.Content)
{
    /// <summary>Width for <see cref="ColumnFit.Fixed"/>.</summary>
    public double Fixed { get; init; }

    /// <summary>The narrowest the column goes; the header label's width when null. See <see cref="TableLayout.MinimumOf"/>.</summary>
    public double? Min { get; init; }

    /// <summary>A <see cref="ColumnFit.Fill"/> column that never narrows below its widest cell, as other columns do not.</summary>
    public bool Whole { get; init; }
}

/// <summary>Column widths for a table whose columns fit their content.</summary>
public static class ColumnLayout
{
    /// <summary>
    /// Widths for <paramref name="available"/> pixels, spacing already taken off. A column in
    /// <paramref name="shares"/> takes that fraction of <paramref name="available"/>; every other column
    /// that is not fixed takes its natural width plus an equal part of what is left.
    /// </summary>
    public static IReadOnlyList<double> Widths(
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<double> natural,
        IReadOnlyList<double> minimum,
        IReadOnlyDictionary<string, double> shares,
        double available)
    {
        var count = columns.Count;
        var widths = new double[count];
        var floor = new double[count];
        var isChosen = new bool[count];

        for (var i = 0; i < count; i++)
        {
            if (columns[i].Fit == ColumnFit.Fixed)
            {
                widths[i] = columns[i].Fixed;
                floor[i] = widths[i];
                continue;
            }

            floor[i] = Math.Max(0, minimum[i]);
            isChosen[i] = shares.TryGetValue(columns[i].Key, out var share) && double.IsFinite(share) && share > 0;
            widths[i] = Math.Max(floor[i], isChosen[i] ? shares[columns[i].Key] * available : natural[i]);
        }

        var extra = available - widths.Sum();

        if (extra > 0)
        {
            Grow(columns, widths, isChosen, extra);
        }
        else if (extra < 0)
        {
            var remaining = Shrink(widths, floor, c => columns[c].Fit != ColumnFit.Fixed && !isChosen[c], -extra);
            Shrink(widths, floor, c => columns[c].Fit != ColumnFit.Fixed && isChosen[c], remaining);
        }

        return widths;
    }

    private static void Grow(IReadOnlyList<TableColumn> columns, double[] widths, bool[] isChosen, double extra)
    {
        var receivers = Enumerable.Range(0, columns.Count)
            .Where(c => !isChosen[c] && columns[c].Fit != ColumnFit.Fixed)
            .ToList();

        foreach (var c in receivers)
        {
            widths[c] += extra / receivers.Count;
        }
    }

    private static double Shrink(double[] widths, double[] floor, Func<int, bool> takes, double overflow)
    {
        var slack = Enumerable.Range(0, widths.Length).Where(takes).ToDictionary(c => c, c => Math.Max(0, widths[c] - floor[c]));
        var total = slack.Values.Sum();

        if (total <= 0)
        {
            return overflow;
        }

        var taken = Math.Min(overflow, total);

        foreach (var (c, room) in slack)
        {
            widths[c] -= taken * room / total;
        }

        return overflow - taken;
    }
}
