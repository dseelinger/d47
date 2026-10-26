namespace D47.App.Theming;

public enum ColumnFit
{
    /// <summary>As wide as its widest cell or header label.</summary>
    Content,

    /// <summary>Takes the space the other columns leave.</summary>
    Fill,

    /// <summary>A set width.</summary>
    Fixed,
}

/// <summary>One column of a table laid out by <see cref="TableLayout"/>.</summary>
public sealed record TableColumn(string Key, ColumnFit Fit = ColumnFit.Content)
{
    /// <summary>Width for <see cref="ColumnFit.Fixed"/>.</summary>
    public double Fixed { get; init; }

    /// <summary>The narrowest the column goes; the header label's width when null.</summary>
    public double? Min { get; init; }
}

/// <summary>Column widths for a table whose columns fit their content.</summary>
public static class ColumnLayout
{
    /// <summary>Widths for <paramref name="available"/> pixels, spacing already taken off.</summary>
    public static IReadOnlyList<double> Widths(
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<double> natural,
        IReadOnlyList<double> minimum,
        IReadOnlyDictionary<string, double> chosen,
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
            isChosen[i] = chosen.TryGetValue(columns[i].Key, out var width) && double.IsFinite(width) && width > 0;
            widths[i] = Math.Max(floor[i], isChosen[i] ? chosen[columns[i].Key] : natural[i]);
        }

        var extra = available - widths.Sum();

        if (extra > 0)
        {
            Grow(columns, natural, widths, isChosen, extra);
        }
        else if (extra < 0)
        {
            var remaining = Shrink(widths, floor, c => columns[c].Fit != ColumnFit.Fixed && !isChosen[c], -extra);
            Shrink(widths, floor, c => columns[c].Fit != ColumnFit.Fixed && isChosen[c], remaining);
        }

        return widths;
    }

    private static void Grow(
        IReadOnlyList<TableColumn> columns, IReadOnlyList<double> natural, double[] widths, bool[] isChosen, double extra)
    {
        var receivers = Enumerable.Range(0, columns.Count)
            .Where(c => !isChosen[c] && columns[c].Fit == ColumnFit.Fill)
            .ToList();

        if (receivers.Count == 0)
        {
            receivers = [.. Enumerable.Range(0, columns.Count).Where(c => !isChosen[c] && columns[c].Fit == ColumnFit.Content)];
        }

        if (receivers.Count == 0)
        {
            return;
        }

        var total = receivers.Sum(c => Math.Max(0, natural[c]));

        foreach (var c in receivers)
        {
            widths[c] += total > 0 ? extra * Math.Max(0, natural[c]) / total : extra / receivers.Count;
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
