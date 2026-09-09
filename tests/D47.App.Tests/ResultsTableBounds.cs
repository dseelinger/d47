using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The one thing #333 asks of the results tables: whatever the fixed columns add up to, nothing the
/// page paints for them may leave the card that holds them.
/// </summary>
internal static class ResultsTableBounds
{
    /// <summary>
    /// Asserts the block of rows headed by <paramref name="headerText"/> is painted wholly inside the
    /// content rectangle of the card titled <paramref name="cardTitle"/>.
    /// </summary>
    public static void RowsStayInsideTheCard(Visual root, string cardTitle, string headerText)
    {
        var card = Card(root, cardTitle);
        var header = Header(card, headerText);
        var painted = PaintedExtent(header, card);
        var inside = Inside(card);

        // A pixel of slack: layout rounding, not a column hanging out of the card.
        Assert.True(
            painted.Right <= inside.Right + 1,
            $"'{cardTitle}' rows are painted out to {painted.Right:0.#} px, past the card's inner "
            + $"edge at {inside.Right:0.#} px.");

        Assert.True(painted.Left >= inside.Left - 1, $"'{cardTitle}' rows start left of the card.");
        Assert.True(painted.Bottom <= inside.Bottom + 1, $"'{cardTitle}' rows reach below the card.");
    }

    /// <summary>How far the block of rows actually reaches on screen.</summary>
    private static Rect PaintedExtent(Visual header, Border card)
    {
        var row = header.GetVisualAncestors()
            .OfType<StackPanel>()
            .First(panel => panel.Orientation == Avalonia.Layout.Orientation.Horizontal);

        var block = row.GetVisualParent() ?? row;
        var painted = Rect(block, card);

        foreach (var drawn in block.GetVisualDescendants())
        {
            painted = painted.Union(Rect(drawn, card));
        }

        for (var walk = block.GetVisualParent(); walk is not null && walk != card; walk = walk.GetVisualParent())
        {
            if (Clips(walk))
            {
                return painted.Intersect(Rect(walk, card));
            }
        }

        return painted;
    }

    private static Rect Rect(Visual visual, Visual within)
    {
        var origin = visual.TranslatePoint(new Point(0, 0), within);

        Assert.NotNull(origin);

        return new Rect(origin!.Value, visual.Bounds.Size);
    }

    private static bool Clips(Visual visual) =>
        visual.ClipToBounds || visual is ScrollContentPresenter or ScrollViewer;

    private static Visual Header(Border card, string headerText)
    {
        var header = card.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(block => block.Text == headerText);

        Assert.NotNull(header);

        return header!;
    }

    /// <summary>The innermost <see cref="Border"/> whose contents include the card's own title.</summary>
    private static Border Card(Visual root, string title)
    {
        var card = root.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(block => block.Text == title))
            .OrderBy(border => border.Bounds.Width)
            .FirstOrDefault();

        Assert.NotNull(card);

        return card!;
    }

    private static Rect Inside(Border card)
    {
        var padding = card.Padding;
        var edge = card.BorderThickness;

        return new Rect(
            edge.Left + padding.Left,
            edge.Top + padding.Top,
            Math.Max(0, card.Bounds.Width - edge.Left - edge.Right - padding.Left - padding.Right),
            Math.Max(0, card.Bounds.Height - edge.Top - edge.Bottom - padding.Top - padding.Bottom));
    }
}
