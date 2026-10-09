using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;

namespace D47.App.Settings;

/// <summary>Marks query hits in caption text and decides which of a row's secondary lines are drawn.</summary>
internal sealed class QueryMarker(Control resources, Func<string, bool> openCapability)
{
    public void Illuminate(
        SettingRow row, TextBlock? label, TextBlock? spoken, TextBlock? help, TextBlock? keyLine, string query)
    {
        if (label is not null)
        {
            Paint(label, row.Label, query);
        }

        if (spoken is not null)
        {
            Paint(spoken, row.Help, query);
        }

        // **The help is behind a glyph, so a query that only it answers has to bring it out.** Matches() has
        // always tested the help text, and since the callout it is no longer on screen — so a row could stay
        // behind a filter with every visible word on it disagreeing with the query, which reads as the filter
        // being broken rather than as a match the Commander cannot see.
        if (help is not null)
        {
            var inTheHelp = query.Length > 0
                && row.Help.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !row.Label.Contains(query, StringComparison.OrdinalIgnoreCase);

            help.IsVisible = inTheHelp;

            if (inTheHelp)
            {
                Paint(help, row.Help, query);
            }
        }

        if (keyLine is null)
        {
            return;
        }

        var onlyTheKey = query.Length > 0
            && row.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
            && !row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
            && !row.Help.Contains(query, StringComparison.OrdinalIgnoreCase);

        keyLine.IsVisible = onlyTheKey;

        if (onlyTheKey)
        {
            Paint(keyLine, row.Key, query);
        }
    }

    /// <summary>
    /// One block of caption text with the hits in it marked, or the plain string when there is no
    /// query.
    /// </summary>
    public void Paint(TextBlock block, string markup, string query)
    {
        // The sentence without its markup: what is read out, what is searched, and what is drawn where there
        // is no link to draw.
        var segments = D47.Core.Interface.HelpLinks.Parse(markup);
        var text = D47.Core.Interface.HelpLinks.Plain(markup);

        // A block composed of runs reports no Text of its own, and Text is what an automation peer reads — so
        // the name is set outright rather than left to be inferred.
        AutomationProperties.SetName(block, text);

        // Qualified: Avalonia.Controls has a TextSearch of its own, about typing to select an item in a list,
        // and it is the one that wins in this file's usings.
        var matches = D47.Core.Interface.TextSearch.Find(text, query);

        var links = segments.Any(segment => segment.Target is not null);

        if (matches.Count == 0 && !links)
        {
            block.Inlines?.Clear();
            block.Text = text;
            return;
        }

        if (links)
        {
            PaintWithLinks(block, segments, matches);
            return;
        }

        // Text and Inlines both draw, one after the other.
        block.Text = null;
        block.Inlines!.Clear();

        var cursor = 0;

        foreach (var match in matches)
        {
            if (match.Start > cursor)
            {
                block.Inlines!.Add(new Run(text[cursor..match.Start]));
            }

            var hit = new Run(text[match.Start..match.End]);
            hit.Bind(TextElement.BackgroundProperty, resources.GetResourceObservable(ThemeManager.LineKey));

            block.Inlines!.Add(hit);
            cursor = match.End;
        }

        if (cursor < text.Length)
        {
            block.Inlines!.Add(new Run(text[cursor..]));
        }
    }

    /// <summary>The same caption when some of it is a cross-reference (#65).</summary>
    private void PaintWithLinks(
        TextBlock block,
        IReadOnlyList<D47.Core.Interface.HelpSegment> segments,
        IReadOnlyList<D47.Core.Interface.SearchMatch> matches)
    {
        block.Text = null;
        block.Inlines!.Clear();

        var at = 0;

        foreach (var segment in segments)
        {
            var start = at;
            var end = at + segment.Text.Length;
            at = end;

            // Every boundary inside this stretch: where it starts, where it ends, and every edge of every hit
            // that falls in it.
            var cuts = new SortedSet<int> { start, end };

            foreach (var match in matches)
            {
                if (match.Start > start && match.Start < end) { cuts.Add(match.Start); }
                if (match.End > start && match.End < end) { cuts.Add(match.End); }
            }

            var edges = cuts.ToArray();

            for (var i = 0; i + 1 < edges.Length; i++)
            {
                var from = edges[i];
                var to = edges[i + 1];

                var run = new Run(segment.Text[(from - start)..(to - start)]);
                var marked = matches.Any(match => match.Start <= from && match.End >= to);

                if (marked)
                {
                    run.Bind(
                        TextElement.BackgroundProperty,
                        resources.GetResourceObservable(ThemeManager.LineKey));
                }

                if (segment.Target is not null)
                {
                    run.Bind(
                        TextElement.ForegroundProperty,
                        resources.GetResourceObservable(ThemeManager.AKey));

                    run.TextDecorations = TextDecorations.Underline;
                }

                block.Inlines!.Add(run);
            }
        }

        // The click is on the block rather than per-run: a Run is not an input element in Avalonia, so it has
        // no pointer events of its own.
        var targets = segments.Where(segment => segment.Target is not null).ToList();

        if (targets.Count == 0)
        {
            return;
        }

        block.Cursor = new Cursor(StandardCursorType.Hand);

        // One handler per painted block, and Paint runs again on every filter keystroke - so the old one is
        // dropped rather than stacked, or a caption painted twenty times would jump twenty times on one
        // click.
        if (_linkHandlers.TryGetValue(block, out var previous))
        {
            block.PointerPressed -= previous;
        }

        EventHandler<PointerPressedEventArgs> handler = (_, e) =>
        {
            // Whichever section the first link on this caption names.
            e.Handled = openCapability(targets[0].Target!);
        };

        block.PointerPressed += handler;
        _linkHandlers[block] = handler;
    }

    /// <summary>
    /// The click handler each linked caption currently carries, so repainting replaces it instead of
    /// adding a second one.
    /// </summary>
    private readonly Dictionary<TextBlock, EventHandler<PointerPressedEventArgs>> _linkHandlers = [];
}
