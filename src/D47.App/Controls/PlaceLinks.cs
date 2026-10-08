using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Controls;

/// <summary>Finds the places a surface's text names and takes that surface to one when it is pressed.</summary>
public sealed class PlaceLinker(Func<string, IReadOnlyList<PanelPlace>> find, Action<PanelPlace> go)
{
    /// <summary>Raised when the places this surface has change, so text drawn earlier can be linked again.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<PanelPlace> Find(string text) => find(text);

    public void Go(PanelPlace place) => go(place);

    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Draws the places a message names as Cyan runs inside its own text block, underlined under the pointer,
/// and takes a press on one to that place (#951). The runs stay plain text, so selection, copy and search
/// see the message as written.
/// </summary>
public static class PlaceLinks
{
    /// <summary>The linker for every block under the surface that sets it.</summary>
    public static readonly AttachedProperty<PlaceLinker?> LinkerProperty =
        AvaloniaProperty.RegisterAttached<Control, PlaceLinker?>("Linker", typeof(PlaceLinks), inherits: true);

    public static PlaceLinker? GetLinker(Control control) => control.GetValue(LinkerProperty);

    public static void SetLinker(Control control, PlaceLinker? linker) => control.SetValue(LinkerProperty, linker);

    private static readonly ConditionalWeakTable<TextBlock, Links> Drawn = [];

    private static Cursor? _hand;

    /// <summary>
    /// Starts drawing <paramref name="places"/> into <paramref name="block"/>, whose offsets are into the
    /// block's own text; <paramref name="go"/> is what a press on one does.
    /// </summary>
    public static void Begin(TextBlock block, IReadOnlyList<PanelPlace> places, Action<PanelPlace> go)
    {
        if (places.Count == 0 && !Drawn.TryGetValue(block, out _))
        {
            return;
        }

        var links = Drawn.GetValue(block, Watch);

        links.Hover(block, null);
        links.Places = places;
        links.Runs.Clear();
        links.Go = go;
    }

    /// <summary>Records <paramref name="run"/> as part of <paramref name="place"/>, drawn in Cyan unless <paramref name="tinted"/> is false.</summary>
    public static void Add(TextBlock block, Run run, PanelPlace place, bool tinted = true)
    {
        if (tinted)
        {
            run.Bind(TextElement.ForegroundProperty, block.GetResourceObservable(ThemeManager.CyanKey));
        }

        if (Drawn.TryGetValue(block, out var links))
        {
            links.Runs.Add((run, place));
        }
    }

    /// <summary>
    /// <paramref name="text"/> cut at the edges of <paramref name="places"/>, each piece with the place it
    /// belongs to; <paramref name="offset"/> is where the text starts in the block.
    /// </summary>
    public static IEnumerable<(string Text, PanelPlace? Place)> Cut(string text, int offset, IReadOnlyList<PanelPlace> places)
    {
        var cursor = 0;

        foreach (var place in places)
        {
            var start = Math.Max(place.Start - offset, cursor);
            var end = Math.Min(place.Start + place.Length - offset, text.Length);

            if (end <= start)
            {
                continue;
            }

            if (start > cursor)
            {
                yield return (text[cursor..start], null);
            }

            yield return (text[start..end], place);
            cursor = end;
        }

        if (cursor < text.Length)
        {
            yield return (text[cursor..], null);
        }
    }

    /// <summary>The place under a pointer on <paramref name="block"/>, or null.</summary>
    public static PanelPlace? At(TextBlock block, Point point)
    {
        if (!Drawn.TryGetValue(block, out var links) || links.Places.Count == 0)
        {
            return null;
        }

        var hit = block.TextLayout.HitTestPoint(point - new Point(block.Padding.Left, block.Padding.Top));

        if (!hit.IsInside)
        {
            return null;
        }

        var offset = hit.TextPosition;

        return links.Places.FirstOrDefault(place => offset >= place.Start && offset < place.Start + place.Length);
    }

    private static Links Watch(TextBlock block)
    {
        var links = new Links();

        // Tunnelling, so the text is hit-tested before a selectable block's own handling moves its selection.
        block.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
            links.Hover(block, At(block, e.GetPosition(block))), RoutingStrategies.Tunnel, handledEventsToo: true);

        block.AddHandler(InputElement.PointerExitedEvent, (_, _) => links.Hover(block, null), handledEventsToo: true);

        block.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
            links.Pressed = e.GetCurrentPoint(block).Properties.IsLeftButtonPressed ? At(block, e.GetPosition(block)) : null,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        block.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            var pressed = links.Pressed;
            links.Pressed = null;

            // A drag that selected text across a link is a selection, not a press.
            if (pressed is null
                || At(block, e.GetPosition(block)) != pressed
                || block is SelectableTextBlock { SelectedText.Length: > 0 })
            {
                return;
            }

            links.Go?.Invoke(pressed);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        return links;
    }

    private sealed class Links
    {
        public IReadOnlyList<PanelPlace> Places { get; set; } = [];

        public List<(Run Run, PanelPlace Place)> Runs { get; } = [];

        public Action<PanelPlace>? Go { get; set; }

        public PanelPlace? Pressed { get; set; }

        private PanelPlace? _hovered;

        public void Hover(TextBlock block, PanelPlace? place)
        {
            if (place == _hovered)
            {
                return;
            }

            foreach (var (run, of) in Runs)
            {
                if (of == _hovered || of == place)
                {
                    run.TextDecorations = of == place ? TextDecorations.Underline : null;
                }
            }

            _hovered = place;

            if (place is null)
            {
                block.ClearValue(InputElement.CursorProperty);
            }
            else
            {
                block.Cursor = _hand ??= new Cursor(StandardCursorType.Hand);
            }
        }
    }
}
