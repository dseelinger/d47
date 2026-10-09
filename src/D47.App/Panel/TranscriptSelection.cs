using Avalonia;
using Avalonia.Controls;

namespace D47.App.Panel;

/// <summary>The transcript's text selection, including a drag that crosses bubbles.</summary>
internal sealed class TranscriptSelection(
    StackPanel bubbles,
    Func<IReadOnlyList<SelectableTextBlock>> blocks,
    Action changed)
{
    private SelectableTextBlock? _selection;
    private int _dragAnchor = -1;
    private int _dragAnchorOffset;
    private (int First, int Last)? _span;

    /// <summary>A drag is under way.</summary>
    public bool Dragging => _dragAnchor >= 0;

    /// <summary>Raises <c>changed</c> whenever the block's selection moves.</summary>
    public void Watch(SelectableTextBlock block) =>
        block.PropertyChanged += (sender, args) =>
        {
            if (args.Property != SelectableTextBlock.SelectionStartProperty
                && args.Property != SelectableTextBlock.SelectionEndProperty)
            {
                return;
            }

            if (sender is SelectableTextBlock { SelectedText.Length: > 0 } selected)
            {
                _selection = selected;
            }

            changed();
        };

    /// <summary>Starts a drag at a point in the bubbles panel's coordinate space.</summary>
    public void Begin(Point point)
    {
        if (HitBubble(point) is not { } hit)
        {
            return;
        }

        _dragAnchor = hit.Index;
        _dragAnchorOffset = hit.Offset;
        _span = null;

        var all = blocks();

        for (var i = 0; i < all.Count; i++)
        {
            if (i != hit.Index)
            {
                all[i].SelectionStart = 0;
                all[i].SelectionEnd = 0;
            }
        }
    }

    /// <summary>Extends the selection across every bubble the drag has crossed.</summary>
    public void Continue(Point point)
    {
        if (_dragAnchor < 0 || HitBubble(point) is not { } hit)
        {
            return;
        }

        if (hit.Index == _dragAnchor && _span is null)
        {
            return;
        }

        var all = blocks();

        if (hit.Index == _dragAnchor)
        {
            all[_dragAnchor].SelectionStart = _dragAnchorOffset;
            all[_dragAnchor].SelectionEnd = hit.Offset;

            for (var i = 0; i < all.Count; i++)
            {
                if (i != _dragAnchor)
                {
                    all[i].SelectionStart = 0;
                    all[i].SelectionEnd = 0;
                }
            }

            _span = null;
            return;
        }

        var down = hit.Index > _dragAnchor;
        var low = Math.Min(_dragAnchor, hit.Index);
        var high = Math.Max(_dragAnchor, hit.Index);

        for (var i = 0; i < all.Count; i++)
        {
            var block = all[i];

            if (i < low || i > high)
            {
                block.SelectionStart = 0;
                block.SelectionEnd = 0;
                continue;
            }

            var length = block.Inlines?.Text?.Length ?? 0;

            (block.SelectionStart, block.SelectionEnd) = i switch
            {
                _ when i == _dragAnchor && down => (_dragAnchorOffset, length),
                _ when i == _dragAnchor => (0, _dragAnchorOffset),
                _ when i == hit.Index && down => (0, hit.Offset),
                _ when i == hit.Index => (hit.Offset, length),
                _ => (0, length),
            };
        }

        _span = (low, high);
    }

    /// <summary>The bubbles were cleared: no drag, no span.</summary>
    public void Reset()
    {
        _dragAnchor = -1;
        _span = null;
    }

    /// <summary>The block that last held a selection, or none while that selection is empty.</summary>
    public SelectableTextBlock? Selected() =>
        _selection is { SelectedText.Length: > 0 } held ? held : null;

    /// <summary>What a selection spanning several bubbles holds, or null with no such span.</summary>
    public string? SpanText() =>
        _span is { } span
            ? string.Join(
                Environment.NewLine,
                Enumerable.Range(span.First, span.Last - span.First + 1)
                    .Select(i => blocks()[i].SelectedText)
                    .Where(text => !string.IsNullOrEmpty(text)))
            : null;

    /// <summary>The bubble a point falls over and the character offset in it, clamped to the nearest bubble and character.</summary>
    private (int Index, int Offset)? HitBubble(Point point)
    {
        var all = blocks();

        if (all.Count == 0)
        {
            return null;
        }

        var index = 0;

        for (var i = 1; i < all.Count; i++)
        {
            var top = all[i].TranslatePoint(new Point(0, 0), bubbles)?.Y ?? double.MaxValue;

            if (point.Y < top)
            {
                break;
            }

            index = i;
        }

        var block = all[index];
        var length = block.Inlines?.Text?.Length ?? 0;

        if (length == 0)
        {
            return (index, 0);
        }

        var origin = block.TranslatePoint(new Point(0, 0), bubbles) ?? default;
        var local = point - origin;
        var offset = block.TextLayout.HitTestPoint(local).TextPosition;

        return (index, Math.Clamp(offset, 0, length));
    }
}
