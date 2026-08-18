using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Controls.Templates;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace D47.App.Panel;

/// <summary>
/// A view, laid out and rasterised at a fixed pixel size, with nothing on screen to show for
/// it. This is what the VR overlay draws from (architecture.md D1).
/// <para>
/// <b>It hosts the view in a window that is never shown, and that is not an oversight.</b> The
/// spike proved a detached <c>Visual</c> renders, but it proved it with a hand-built tree of
/// borders and text blocks carrying literal brushes. A real view is neither of those things: a
/// <c>UserControl</c> is a templated control, its template comes from a control theme, control
/// themes arrive through styling, and styling only runs for an element attached to a logical
/// tree with a root. Detached, <see cref="PanelView"/> measures to 0x0, materialises one
/// visual, and rasterises as an empty rectangle — no error, no warning, just a blank panel in
/// the headset. Measured, not guessed: 1 visual detached against 51 hosted.
/// </para>
/// <para>
/// The window is constructed and never shown, so it has no desktop presence to minimise, no
/// taskbar entry and nothing the Commander can reach. Minimise-safety is unaffected — it never
/// depended on there being no window so much as on the VR path not depending on the state of
/// the one the Commander can see, and this window has no state because it is never shown.
/// </para>
/// </summary>
public sealed class OffscreenSurface : IDisposable
{
    private readonly Window _root;
    private readonly Control _view;

    /// <summary>
    /// Where a chooser is drawn, over the view and inside the same visual tree.
    /// <para>
    /// <b>d47 draws its own rather than opening a popup, and that is not a preference.</b> A popup
    /// asks the platform for a top level of its own; this window has never been shown, so there is
    /// nothing for one to hang off, and opening one does not fail politely — it recurses until the
    /// stack is gone and takes the process with it. Measured: <c>IsDropDownOpen = true</c> on a
    /// combo box in here exits at <c>0xC00000FD</c>, before any dispatcher work, with no exception
    /// and nothing in the log. Forcing the popup into the window's own overlay layer does not help;
    /// it is the same crash. That is what pressing "Panel content" from the headset did
    /// (remediation.md 9).
    /// </para>
    /// <para>
    /// A layer here is a plain <see cref="Avalonia.Controls.Panel"/> in the tree that is already laid out, drawn and
    /// hit-tested every frame. Everything that works for a button on the panel works for a row in
    /// here for free — including the ray, which is the whole point.
    /// </para>
    /// </summary>
    private readonly Avalonia.Controls.Panel _over = new() { IsVisible = false };

    private RenderTargetBitmap? _target;
    private PixelSize _size;

    /// <summary>Both, so a render includes whatever is being chosen from.</summary>
    private readonly Avalonia.Controls.Panel _surface;

    public OffscreenSurface(Control view, PixelSize size)
    {
        _view = view;

        _surface = new Avalonia.Controls.Panel();
        _surface.Children.Add(view);
        _surface.Children.Add(_over);

        _root = new Window
        {
            ShowInTaskbar = false,
            Content = _surface,
        };

        Resize(size);
    }

    /// <summary>Whether something is being chosen from right now.</summary>
    public bool IsChoosing => _over.IsVisible;

    public PixelSize Size => _size;

    /// <summary>
    /// Changes the pixel size the view is laid out at.
    /// <para>
    /// A resize is a relayout, not a rescale. Apparent text size in a headset is the pixel
    /// count and the quad's width in metres together, so a surface asked to be smaller has to
    /// be drawn smaller rather than drawn the same and hung nearer — the second is how a
    /// glanceable panel becomes an unreadable one.
    /// </para>
    /// </summary>
    public void Resize(PixelSize size)
    {
        if (size == _size)
        {
            return;
        }

        _size = size;
        _target?.Dispose();
        _target = new RenderTargetBitmap(size);

        _root.Width = size.Width;
        _root.Height = size.Height;
    }

    /// <summary>
    /// Lays the view out and rasterises it. Runs on the UI thread — a <c>Visual</c> is thread
    /// affine and the layout pass is the dispatcher's.
    /// </summary>
    /// <param name="settle">
    /// Run between the layout pass and the rasterise, for anything that can only be decided once
    /// the tree has a size — and then laid out again, because what it decides is a position.
    /// <para>
    /// The panel's "follow the newest line" is the case this exists for. It scrolls to the end of
    /// the extent the scroll viewer currently knows about, and calls <c>UpdateLayout</c> first to
    /// make sure that extent is current — which, on a window that is never shown, does nothing at
    /// all. So it scrolled to the end of an extent equal to the viewport, which is offset zero,
    /// and the headset showed the oldest lines of the transcript for the whole session
    /// (remediation.md, "The Newest button in VR does not appear to work").
    /// </para>
    /// </param>
    public RenderTargetBitmap Render(Action? settle = null)
    {
        var bounds = new Rect(0, 0, _size.Width, _size.Height);

        Layout(bounds);


        if (settle is not null)
        {
            settle();

            // Again, because what settling decides is where things sit rather than how big they
            // are, and a scroll offset is applied by an arrange.
            Layout(bounds);
        }

        _target!.Render(_surface);
        return _target;
    }

    /// <summary>One full layout pass over the offscreen tree.</summary>
    private void Layout(Rect bounds)
    {
        // <b>Every element is invalidated first, and that is the fix rather than a precaution.</b>
        //
        // Measure short-circuits on a control that is already valid, and a control that changed
        // does not mark its ancestors: it marks itself and queues itself with the layout manager,
        // which is what would normally run the pass. This window is constructed and never shown,
        // so nothing runs that pass — Measure on the root returned immediately, never descended,
        // and whatever had changed was never laid out. UpdateLayout on the root does not help for
        // the same reason.
        //
        // What that looked like: the VR panel drew the transcript its model held when the data
        // context was first set and nothing after it — an append did not appear, and the line that
        // <em>was</em> showing vanished on the next one, leaving an empty page under a tab strip
        // that still lit up, because the strip's own properties are on controls the root reaches.
        // Confirmed against a saved frame rather than inferred
        // (remediation.md, "All tabs should update in the VR big panel").
        //
        // Affordable because it only runs on a frame being redrawn at all: the runtime calls Draw
        // only when the surface says it is dirty, which is when something has changed.
        foreach (var element in _root.GetVisualDescendants().OfType<Avalonia.Layout.Layoutable>())
        {
            element.InvalidateMeasure();
        }

        _root.InvalidateMeasure();

        // The window's own layout pass is what applies styling and materialises the template.
        // Arranging the view afterwards is what makes it fill the surface rather than settle
        // at its desired size, which for this panel is about a third of it.
        _root.Measure(bounds.Size);
        _root.Arrange(bounds);
        _view.Measure(bounds.Size);
        _view.Arrange(bounds);
        _surface.Measure(bounds.Size);
        _surface.Arrange(bounds);
    }

    /// <summary>
    /// Copies the last render into a caller-owned buffer — the mapped staging texture, in
    /// production. Writing straight into it removes one full-surface copy from the frame
    /// (docs/spikes/vr-texture.md, variant B).
    /// </summary>
    public void CopyInto(IntPtr destination, int rowBytes) =>
        _target!.CopyPixels(
            new PixelRect(0, 0, _size.Width, _size.Height),
            destination,
            rowBytes * _size.Height,
            rowBytes);

    /// <summary>
    /// Delivers a press and a release at a point on the surface, as a mouse would, and says
    /// whether there was anything there to press.
    /// <para>
    /// <b>Synthesised rather than routed, because there is no platform pointer here.</b> The
    /// window is never shown, so it receives no input from the desktop at all — the only thing
    /// pointing at this surface is a controller in a headset, and the coordinates it produces are
    /// a fraction across a quad. Raising the real routed events is what lets every control behave
    /// as itself: a button presses, a tab selects, a toggle toggles, and none of them needs to
    /// know where the press came from.
    /// </para>
    /// <para>
    /// Press and release together, at the same point. A drag across the surface would be a second
    /// gesture, and the one the panel already has for a held trigger is carrying the whole quad.
    /// </para>
    /// </summary>
    public bool Click(Point at)
    {
        if (Deepest(_surface, at) is not { } target)
        {
            return false;
        }

        // Decided before a single pointer event is raised, because for two kinds of control the
        // gesture itself is the problem rather than what it activates.
        var actionable = target.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(Actionable);

        // A combo box would open a popup, which is the crash. It gets a chooser drawn into this
        // tree instead — the same list, on the panel, pressable by the same ray.
        if (actionable is ComboBox combo)
        {
            return Choose(combo);
        }

        // A text box has nothing to type into it here: there is no keyboard in a cockpit and no
        // focus to give it. It gets one drawn on the panel.
        if (actionable is TextBox box)
        {
            Type(box);
            return true;
        }

        // A control that opens a window is left alone. The chooser above covers the case that
        // matters; a dialog on a desktop the Commander is not looking at is one they cannot
        // answer, so it is refused rather than opened behind them.
        if (actionable is not null && actionable.Classes.Contains(DesktopOnly))
        {
            return false;
        }

        var pointer = new Pointer(PointerId, PointerType.Mouse, isPrimary: true);

        target.RaiseEvent(new PointerPressedEventArgs(
            target,
            pointer,
            _view,
            at,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        target.RaiseEvent(new PointerReleasedEventArgs(
            target,
            pointer,
            _view,
            at,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));

        // Released explicitly: a control that captured this pointer on the press would otherwise
        // keep hold of an object nothing will ever move again.
        pointer.Capture(null);

        Activate(target);

        return true;
    }

    /// <summary>
    /// Does what the release should have done, for the controls this panel is made of.
    /// <para>
    /// <b>The routed events alone are not enough here, and the reason is the same one
    /// <see cref="Deepest"/> exists for.</b> <c>ButtonBase.OnPointerReleased</c> only calls
    /// <c>OnClick</c> if the renderer says the release landed on the button — and the renderer
    /// belonging to a window that is never shown answers that question with nothing, for every
    /// point. Measured: the press arrived, the button took it, and the release passed the button
    /// silently. So the press and release are still raised, because a control with its own
    /// handling should see a real gesture, and then the activation is done here.
    /// </para>
    /// <para>
    /// A closed list rather than a general mechanism, and it is the list of what the panel
    /// offers: the page tabs are radio buttons and Copy and the search steppers are buttons.
    /// Most specific first, because a radio button is a toggle button is a button. Anything else
    /// — a combo box, a text field on the settings page — receives the gesture and does whatever
    /// it does with it, which for most of them is nothing a Commander in a headset could finish
    /// anyway.
    /// </para>
    /// </summary>
    /// <summary>
    /// Controls this knows how to press. A class rather than a name so the settings surface can
    /// mark one without this having to know what it is for.
    /// </summary>
    public const string DesktopOnly = "desktop-only";

    /// <summary>Whether this is a control a press means something to.</summary>
    private static bool Actionable(Control control) =>
        control is ComboBox or TextBox or ToggleButton or Button || control.Classes.Contains(DesktopOnly);

    /// <summary>
    /// Puts a combo box's list on the panel, as a chooser the ray can press.
    /// <para>
    /// Its own drawing rather than the control's own dropdown, because the dropdown is a popup
    /// and a popup cannot be hosted here at all — see <see cref="_over"/>. What the Commander
    /// gets is the same items in the same order with the current one marked.
    /// </para>
    /// </summary>
    private bool Choose(ComboBox combo)
    {
        if (combo.ItemCount == 0)
        {
            return false;
        }

        // Never the control's own. Nothing here opens it, and a box left open by anything else is
        // the state this exists to keep out of.
        combo.IsDropDownOpen = false;

        var items = new List<string>(combo.ItemCount);

        foreach (var item in combo.Items)
        {
            items.Add(item?.ToString() ?? string.Empty);
        }

        Offer(items, combo.SelectedIndex, chosen => combo.SelectedIndex = chosen);
        return true;
    }

    /// <summary>
    /// What the panel's own overlays are drawn in.
    /// <para>
    /// <b>Deliberately not themed</b>, for the same reason the caption layer is not: these are
    /// read at a metre through a headset, over whatever the cockpit is doing behind them, and
    /// they have to be legible before they are in keeping. The first cut inherited the theme and
    /// came out dark grey on dark grey — present in the tree, pressable, and unreadable.
    /// </para>
    /// </summary>
    private static readonly IBrush Ink = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));

    private static readonly IBrush KeyFill = new SolidColorBrush(Color.FromRgb(0x2C, 0x31, 0x39));

    private static readonly IBrush KeyEdge = new SolidColorBrush(Color.FromRgb(0x50, 0x57, 0x63));

    private static readonly IBrush CardFill = new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1A));

    private static readonly IBrush Marked = new SolidColorBrush(Color.FromRgb(0x1F, 0x4A, 0x6B));

    /// <summary>
    /// One pressable thing, dressed so it can be read from across a cockpit.
    /// <para>
    /// <see cref="Theming.TypeScale.Heading"/> for every one of them, which is the top of the
    /// scale and the right end of it: the scale resolves upwards precisely because a surface read
    /// at a metre in a headset is the case it was written for, and a key nobody can read is a key
    /// nobody can press.
    /// </para>
    /// </summary>
    private static Button Pressable(string label, IBrush? fill = null) => new()
    {
        Content = label,
        Foreground = Ink,
        Background = fill ?? KeyFill,
        BorderBrush = KeyEdge,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        FontSize = Theming.TypeScale.Heading,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>
    /// Draws a list over the panel and calls back with what was pressed.
    /// <para>
    /// Ordinary controls in the ordinary tree: a border, a scroll viewer and one button a row.
    /// Everything that already works on this surface — the geometric hit test, the activation, the
    /// scrollbar a ray can drag — works on these without knowing they are special.
    /// </para>
    /// </summary>
    public void Offer(IReadOnlyList<string> items, int selected, Action<int> pick)
    {
        var rows = new StackPanel { Spacing = 2 };

        for (var index = 0; index < items.Count; index++)
        {
            var at = index;

            var row = Pressable(items[index], fill: index == selected ? Marked : null);

            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            row.HorizontalContentAlignment = HorizontalAlignment.Left;
            row.Padding = new Thickness(16, 12);
            row.MinHeight = 48;
            row.FontWeight = index == selected ? FontWeight.SemiBold : FontWeight.Normal;

            row.Click += (_, _) =>
            {
                Dismiss();
                pick(at);
            };

            rows.Children.Add(row);
        }

        var cancel = Pressable("Cancel");
        cancel.HorizontalAlignment = HorizontalAlignment.Right;
        cancel.Padding = new Thickness(18, 10);
        cancel.Margin = new Thickness(0, 12, 0, 0);
        cancel.Click += (_, _) => Dismiss();

        var body = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(cancel, Dock.Bottom);
        body.Children.Add(cancel);
        body.Children.Add(new ScrollViewer
        {
            Content = rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        var card = Card(body);
        card.MinWidth = Math.Min(460, _size.Width - 120);
        card.MaxWidth = Math.Min(560, _size.Width - 80);

        Overlay(card);
    }

    /// <summary>
    /// The keys, in rows, as they are drawn.
    /// <para>
    /// A staggered alphabetic board rather than a strict QWERTY, because what is typed into these
    /// rows is a system name, a commander name or a hotkey — hunted for one key at a time with a
    /// controller, where alphabetical order is faster to hunt in than muscle memory that only
    /// works with ten fingers on a desk.
    /// </para>
    /// </summary>
    private static readonly string[] Keys = ["1234567890", "abcdefghij", "klmnopqrst", "uvwxyz-_.", " "];

    /// <summary>
    /// Puts a keyboard on the panel for one text box, and writes what was typed back into it.
    /// <para>
    /// There is no other way to fill one of these from inside a headset: the window is never
    /// shown, so it takes no keystrokes from the desktop, and there is no keyboard in a cockpit
    /// to take them from anyway (remediation.md 9, "all text boxes should be functional in VR").
    /// </para>
    /// <para>
    /// The box is written once, on Done, rather than on every key. A settings row commits what it
    /// is given, and committing a system name letter by letter would be twelve writes and eleven
    /// wrong values on the way to the right one.
    /// </para>
    /// </summary>
    public void Type(TextBox box)
    {
        var typed = box.Text ?? string.Empty;

        var shown = new TextBox
        {
            Text = typed,
            IsReadOnly = true,
            FontSize = Theming.TypeScale.Heading,
            Foreground = Ink,
            Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x0E, 0x11)),
            BorderBrush = KeyEdge,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 0, 0, 14),
        };

        var board = new StackPanel { Spacing = 6 };

        foreach (var row in Keys)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };

            foreach (var key in row)
            {
                var character = key;

                var pressed = Pressable(character == ' ' ? "space" : character.ToString());
                pressed.Width = character == ' ' ? 280 : 60;
                pressed.Height = 52;

                pressed.Click += (_, _) =>
                {
                    typed += character;
                    shown.Text = typed;
                };

                line.Children.Add(pressed);
            }

            board.Children.Add(line);
        }

        var back = Pressable("delete");
        back.Height = 52;
        back.Padding = new Thickness(18, 0);

        back.Click += (_, _) =>
        {
            typed = typed.Length > 0 ? typed[..^1] : typed;
            shown.Text = typed;
        };

        var clear = Pressable("clear");
        clear.Height = 52;
        clear.Padding = new Thickness(18, 0);

        clear.Click += (_, _) =>
        {
            typed = string.Empty;
            shown.Text = typed;
        };

        var done = Pressable("Done", fill: Marked);
        done.Height = 52;
        done.Padding = new Thickness(24, 0);

        done.Click += (_, _) =>
        {
            Dismiss();

            // Written once, at the end. The row commits what it is handed.
            box.Text = typed;
        };

        var cancel = Pressable("Cancel");
        cancel.Height = 52;
        cancel.Padding = new Thickness(18, 0);
        cancel.Click += (_, _) => Dismiss();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { back, clear, cancel, done },
        };

        var body = new StackPanel { Children = { shown, board, actions } };

        Overlay(Card(body));
    }

    /// <summary>The card everything on this layer sits in.</summary>
    private Border Card(Control body) => new()
    {
        Child = body,
        Padding = new Thickness(18),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),

        // Short of the panel, so it reads as something over the page rather than a new page.
        MaxHeight = Math.Max(180, _size.Height - 60),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Background = CardFill,
        BorderBrush = KeyEdge,
    };

    /// <summary>Puts a card over the page, and dims what is behind it.</summary>
    private void Overlay(Control card)
    {
        // The dimmer is also what makes a press anywhere else land on this layer rather than on
        // the page underneath: the panel must not be pressable while something is over it.
        _over.Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0));
        _over.Children.Clear();
        _over.Children.Add(card);
        _over.IsVisible = true;
    }

    /// <summary>Puts the chooser away, whether it was answered or not.</summary>
    public void Dismiss()
    {
        _over.IsVisible = false;
        _over.Children.Clear();
    }

    private static void Activate(Interactive target)
    {
        foreach (var candidate in target.GetSelfAndVisualAncestors().OfType<Control>())
        {
            switch (candidate)
            {
                case RadioButton radio:
                    radio.IsChecked = true;
                    return;

                case ToggleButton toggle:
                    toggle.IsChecked = toggle.IsChecked != true;
                    return;

                case Button button:
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = button });

                    if (button.Command is { } command && command.CanExecute(button.CommandParameter))
                    {
                        command.Execute(button.CommandParameter);
                    }

                    return;
            }
        }
    }

    /// <summary>
    /// How far from a scrollbar a ray may land and still count as being on it, in surface
    /// pixels.
    /// <para>
    /// A scrollbar is about a dozen pixels wide and a hand at arm's length in a headset does not
    /// hold still to a dozen pixels. This is deliberately far more than the bar itself: the
    /// nearest scrollbar within this distance is the one being aimed at, because on this panel
    /// there is nothing else along that edge to confuse it with
    /// (remediation.md, "Scrollbars in VR should be usable with a controller").
    /// </para>
    /// </summary>
    public const double AimTolerance = 28;

    /// <summary>
    /// The vertical scrollbar a ray at this point is aiming at, or null.
    /// <para>
    /// Distance to the bar's rectangle rather than containment, which is the whole of what makes
    /// this usable: the Commander points at roughly the right edge and the nearest bar within
    /// <see cref="AimTolerance"/> takes it.
    /// </para>
    /// </summary>
    public ScrollBar? ScrollbarNear(Point at)
    {
        ScrollBar? nearest = null;
        var closest = AimTolerance;

        foreach (var bar in _surface.GetVisualDescendants().OfType<ScrollBar>())
        {
            if (!bar.IsVisible || bar.Orientation != Orientation.Vertical || bar.Maximum <= 0)
            {
                continue;
            }

            if (bar.TranslatePoint(new Point(0, 0), _surface) is not { } corner)
            {
                continue;
            }

            var box = new Rect(corner, bar.Bounds.Size);
            var away = Distance(box, at);

            if (away <= closest)
            {
                closest = away;
                nearest = bar;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Puts a bar where the ray is pointing along it, top to bottom.
    /// <para>
    /// The position along the bar <em>is</em> the position in the document, rather than the drag
    /// being relative to where a thumb was grabbed. Absolute is the forgiving one in a headset:
    /// there is no thumb to catch, nothing to miss, and letting go and taking hold again does not
    /// jump.
    /// </para>
    /// </summary>
    public static void Aim(ScrollBar bar, Control within, Point at)
    {
        if (bar.TranslatePoint(new Point(0, 0), within) is not { } corner || bar.Bounds.Height <= 0)
        {
            return;
        }

        var along = Math.Clamp((at.Y - corner.Y) / bar.Bounds.Height, 0, 1);

        bar.Value = bar.Minimum + ((bar.Maximum - bar.Minimum) * along);
    }

    /// <summary>Shortest distance from a point to a rectangle, and zero inside it.</summary>
    private static double Distance(Rect box, Point at)
    {
        var across = Math.Max(Math.Max(box.X - at.X, at.X - box.Right), 0);
        var down = Math.Max(Math.Max(box.Y - at.Y, at.Y - box.Bottom), 0);

        return Math.Sqrt((across * across) + (down * down));
    }

    /// <summary>
    /// Lights the control a ray is resting on, and puts out whatever it was resting on before.
    /// <para>
    /// Set as a pseudo-class rather than through the input manager, for the same reason the hit
    /// test here is geometric: the input manager decides <c>:pointerover</c> from the renderer,
    /// and the renderer for a window that is never shown answers nothing. A scrollbar that does
    /// not light up when aimed at is a scrollbar the Commander cannot tell they have found.
    /// </para>
    /// <para>
    /// <b>Answers whether anything actually changed</b>, because the caller is asked this every
    /// frame a ray is on the panel and the answer is almost always no. Only the caller knows what
    /// a change is worth, and marking the surface dirty for a light that did not move re-rasterises
    /// and re-uploads the whole panel thirty times a second — which is not a waste of a frame
    /// budget but a visible fault. See <see cref="D47.Vr.VrPixels"/> on why.
    /// </para>
    /// </summary>
    public bool Illuminate(Control? control)
    {
        if (ReferenceEquals(control, _lit))
        {
            return false;
        }

        if (_lit is not null)
        {
            ((IPseudoClasses)_lit.Classes).Set(":pointerover", false);
        }

        _lit = control;

        if (_lit is not null)
        {
            ((IPseudoClasses)_lit.Classes).Set(":pointerover", true);
        }

        return true;
    }

    private Control? _lit;

    /// <summary>
    /// The space a point is expressed in, for callers that need to translate into it.
    /// <para>
    /// The wrapper rather than the view, because a chooser drawn over the page is a sibling of it
    /// — a hit test that started at the view would walk straight past the thing on top. Both sit
    /// at the origin at the same size, so the coordinates are the same either way.
    /// </para>
    /// </summary>
    public Control View => _surface;

    /// <summary>The window the view is hosted in. Overlay content — popups — lives here.</summary>
    public Control Root => _root;

    /// <summary>
    /// One id for every synthetic press, because there is exactly one thing pointing at this
    /// surface at a time — the panel is carried with the same button that clicks it, so a second
    /// simultaneous pointer is not a state the gesture can be in.
    /// </summary>
    private const int PointerId = 47;

    /// <summary>
    /// The topmost thing under a point, found by walking the tree rather than by asking the
    /// framework.
    /// <para>
    /// <c>InputHitTest</c> answers null for every point on this surface. It resolves against the
    /// visual root's hit-test path, and the root here is a window that is never shown — there is
    /// no composition behind it to test against. Measured: a tab sitting at (208, 92) inside a
    /// laid-out 1024x640 view, visible, enabled and hit-test visible, was not found by it.
    /// </para>
    /// <para>
    /// Geometry is enough for what this has to do. Children are walked back to front so the
    /// topmost wins, an invisible or hit-test-invisible subtree is skipped entirely, and the
    /// deepest match is returned so the event starts where a real pointer would and bubbles from
    /// there. Clipping is not modelled: nothing on this panel draws outside its own bounds.
    /// </para>
    /// </summary>
    private static Interactive? Deepest(Visual from, Point at)
    {
        var children = from.GetVisualChildren().ToList();

        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is not { } child || !child.IsVisible)
            {
                continue;
            }

            if (child is InputElement { IsHitTestVisible: false })
            {
                continue;
            }

            if (from.TranslatePoint(at, child) is not { } local
                || !new Rect(child.Bounds.Size).Contains(local))
            {
                continue;
            }

            if (Deepest(child, local) is { } deeper)
            {
                return deeper;
            }

            if (child is Interactive interactive)
            {
                return interactive;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _target?.Dispose();

        // The children go too, not just the window's content. A Visual belongs to exactly one
        // visual tree, and the view is a child of the wrapper rather than of the window now — so
        // dropping the window alone left the view parented to a wrapper nothing else could see,
        // and handing it to another host threw.
        _over.Children.Clear();
        _surface.Children.Clear();

        _root.Content = null;
        _root.Close();
    }
}
