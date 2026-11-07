using Avalonia;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>
/// Where a <see cref="PowerChart"/> draws and hit-tests the stack, the drill-in column and the P-labels for one
/// <see cref="PowerPriorities"/>. Heights are measured up from the chart's bottom edge; <see cref="Flip"/> turns one
/// into the control's coordinates.
/// </summary>
internal sealed record PowerChartLayout(
    PowerPriorities Power,
    IReadOnlyList<PowerChartLayout.Block> Blocks,
    IReadOnlyList<PowerChartLayout.Slice> Slices,
    PriorityDrill Drill,
    IReadOnlyList<PowerChartLayout.Bar> Bars,
    double Top)
{
    public const double StackLeft = 205;
    public const double StackRight = 295;
    public const double DrillLeft = 425;
    public const double DrillRight = 705;
    public const double Column = 480;
    public const double Floor = 10;
    public const double LeastBar = 24;

    // This and the 1px every slice leaves at its top put priorities 4px apart.
    public const double PriorityGap = 3;
    public const double EmptySlot = 20;
    public const double LeastLabel = 16;

    public sealed record Slice(ModuleSpan Span, int Priority, double Bottom, double Top);

    public sealed record Bar(ModuleSpan Span, double Bottom, double Height)
    {
        public double Middle => Bottom + (Height / 2);
    }

    /// <summary>One priority's block of the stack, linear in megawatts from its own bottom edge.</summary>
    public sealed record Block(PriorityBand Band, double Bottom, double Top, double Scale)
    {
        public bool IsEmpty => Band.Total <= 0;

        public double Middle => (Bottom + Top) / 2;

        public double At(double megawatts) => Bottom + Math.Round((megawatts - Band.Start) * Scale);
    }

    public Block Selected => Blocks[Drill.Band.Priority - 1];

    /// <summary>Lays out <paramref name="power"/> with <paramref name="selected"/> opened up, scaled to <paramref name="deployed"/> or the output, whichever is more.</summary>
    public static PowerChartLayout Of(PowerPriorities power, int selected, double deployed)
    {
        var reach = Math.Max(power.Output, deployed) * 1.06;
        var empties = power.Bands.Count(band => band.Total <= 0);
        var room = Math.Max(0, Column - (PriorityGap * (PowerPriorities.Lowest - 1)) - (EmptySlot * empties));
        var scale = reach > 0 ? room / reach : 0;

        var blocks = new List<Block>();
        var bottom = Floor;

        foreach (var band in power.Bands)
        {
            var height = band.Total <= 0 ? EmptySlot : Math.Round(band.Total * scale);

            blocks.Add(new Block(band, bottom, bottom + height, scale));
            bottom += height + PriorityGap;
        }

        var slices = blocks
            .Where(block => !block.IsEmpty)
            .SelectMany(block => power.Drill(block.Band.Priority).Spans
                .Select(span => new Slice(span, block.Band.Priority, block.At(span.Start), block.At(span.End))))
            .ToList();

        var drill = power.Drill(selected);
        var heights = Heights([.. drill.Spans.Select(span => span.Module.Megawatts)], Column, LeastBar);
        var bars = new List<Bar>();
        var y = Floor;

        for (var index = 0; index < drill.Spans.Count; index++)
        {
            bars.Add(new Bar(drill.Spans[index], y, heights[index]));
            y += heights[index];
        }

        return new PowerChartLayout(power, blocks, slices, drill, bars, y);
    }

    /// <summary>
    /// Bar heights in proportion to <paramref name="megawatts"/>, filling <paramref name="column"/>. A bar
    /// that would come out shorter than <paramref name="least"/> is raised to it, and the others share
    /// what is left in proportion.
    /// </summary>
    public static double[] Heights(IReadOnlyList<double> megawatts, double column, double least)
    {
        var heights = new double[megawatts.Count];
        var raised = new bool[megawatts.Count];

        while (true)
        {
            var free = Math.Max(0, column - (least * raised.Count(up => up)));
            var shared = megawatts.Where((_, index) => !raised[index]).ToList();
            var total = shared.Sum();
            var changed = false;

            for (var index = 0; index < megawatts.Count; index++)
            {
                if (raised[index])
                {
                    heights[index] = least;
                    continue;
                }

                heights[index] = total > 0 ? megawatts[index] / total * free : free / shared.Count;

                if (heights[index] < least)
                {
                    raised[index] = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                return heights;
            }
        }
    }

    /// <summary>A height measured up from the bottom edge, as a y in the control.</summary>
    public static double Flip(double fromBottom) => PowerChart.ChartHeight - fromBottom;

    /// <summary>Where <paramref name="megawatts"/> falls on the stack: in the first block that reaches it, or above them all.</summary>
    public double At(double megawatts)
    {
        foreach (var block in Blocks.Where(block => !block.IsEmpty))
        {
            if (megawatts <= block.Band.Cumulative + 1e-9)
            {
                return block.At(megawatts);
            }
        }

        var last = Blocks[^1];

        return last.Top + Math.Round((megawatts - last.Band.Cumulative) * last.Scale);
    }

    /// <summary>Where <paramref name="megawatts"/> falls in the drill-in column, or null outside it.</summary>
    public double? Inside(double megawatts) =>
        Bars.FirstOrDefault(bar =>
                megawatts >= bar.Span.Start - 1e-9 && megawatts <= bar.Span.End + 1e-9) is { } bar
            ? bar.Bottom + ((megawatts - bar.Span.Start) / bar.Span.Module.Megawatts * bar.Height)
            : null;

    /// <summary>The drill-in bar under <paramref name="point"/>, in the control's coordinates, or null.</summary>
    public Bar? BarAt(Point point)
    {
        if (point.X < DrillLeft || point.X > DrillRight)
        {
            return null;
        }

        var up = Flip(point.Y);

        return Bars.FirstOrDefault(bar => up >= bar.Bottom && up < bar.Bottom + bar.Height);
    }

    /// <summary>The priority whose block of the stack, or the gap above it, is under <paramref name="point"/>, or null.</summary>
    public int? PriorityAt(Point point)
    {
        if (point.X < StackLeft || point.X > StackRight)
        {
            return null;
        }

        var up = Flip(point.Y);

        return Blocks
            .FirstOrDefault(block => up >= block.Bottom && up < block.Top + PriorityGap)?.Band.Priority;
    }

    /// <summary>
    /// Where each priority's P-label of <paramref name="sizes"/> is drawn, in the control's coordinates: centred in its
    /// block when the block holds it, otherwise just right of the stack and raised clear of the label below.
    /// </summary>
    public IReadOnlyList<(Point At, bool Inside)> Labels(IReadOnlyList<Size> sizes)
    {
        var labels = new List<(Point, bool)>();
        var clear = double.NegativeInfinity;

        for (var index = 0; index < Blocks.Count; index++)
        {
            var block = Blocks[index];
            var size = sizes[index];

            if (IsLabelledInside(block))
            {
                labels.Add((new Point((StackLeft + StackRight - size.Width) / 2, Flip(block.Middle) - (size.Height / 2)), true));
                continue;
            }

            var bottom = Math.Max(block.Middle - (size.Height / 2), clear);

            clear = bottom + size.Height;
            labels.Add((new Point(StackRight + 6, Flip(bottom + size.Height)), false));
        }

        return labels;
    }

    /// <summary>Whether <paramref name="block"/> is tall enough to hold its P-label.</summary>
    public static bool IsLabelledInside(Block block) => block.Top - block.Bottom - 1 >= LeastLabel;
}
