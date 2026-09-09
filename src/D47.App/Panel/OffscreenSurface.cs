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
/// A view, laid out and rasterised at a fixed pixel size, with nothing on screen to show for it.
/// </summary>
public sealed class OffscreenSurface : IDisposable
{
    private readonly Window _root;
    private readonly Control _view;

    /// <summary>Where a chooser is drawn, over the view and inside the same visual tree.</summary>
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

    /// <summary>Changes the pixel size the view is laid out at.</summary>
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

    /// <summary>Lays the view out and rasterises it.</summary>
    /// <param name="settle">
    /// Run between the layout pass and the rasterise, for anything that can only be decided once the
    /// tree has a size — and then laid out again, because what it decides is a position.
    /// </param>
    public RenderTargetBitmap Render(Action? settle = null)
    {
        var bounds = new Rect(0, 0, _size.Width, _size.Height);

        Layout(bounds);

        if (settle is not null)
        {
            settle();

            // Again, because what settling decides is where things sit rather than how big they are, and a
            // scroll offset is applied by an arrange.
            Layout(bounds);
        }

        _target!.Render(_surface);
        return _target;
    }

    /// <summary>One full layout pass over the offscreen tree.</summary>
    private void Layout(Rect bounds)
    {
        // Every element is invalidated first, and that is the fix rather than a precaution. Measure
        // short-circuits on a control that is already valid, and a control that changed does not mark its
        // ancestors: it marks itself and queues itself with the layout manager, which is what would normally
        // run the pass.
        Invalidate();

        // What the Commander has scrolled to, taken down before the pass can move it (Phase 39).
        var scrolled = _view.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Select(viewer => (Viewer: viewer, Was: viewer.Offset))
            .ToList();

        // The window's own layout pass is what applies styling and materialises the template.
        _root.Measure(bounds.Size);
        _root.Arrange(bounds);
        _view.Measure(bounds.Size);
        _view.Arrange(bounds);

        // Put back once the view is its real size, and written through the viewer so an offset that genuinely
        // no longer exists — the list got shorter while the ray was elsewhere — is clamped by it rather than
        // forced.
        if (Restore(scrolled))
        {
            // And laid out again, because an offset is applied by an arrange and there is no layout manager
            // here to run one: setting it marks the presenter and stops, exactly as an invalidated measure
            // does above.
            Invalidate();

            _view.Measure(bounds.Size);
            _view.Arrange(bounds);
        }

        _surface.Measure(bounds.Size);
        _surface.Arrange(bounds);
    }

    /// <summary>Marks the whole tree as needing measure.</summary>
    private void Invalidate()
    {
        foreach (var element in _root.GetVisualDescendants().OfType<Avalonia.Layout.Layoutable>())
        {
            element.InvalidateMeasure();
        }

        _root.InvalidateMeasure();
    }

    /// <summary>Puts each viewer back where it was before the pass, and tells its bar where that is.</summary>
    private static bool Restore(IReadOnlyList<(ScrollViewer Viewer, Vector Was)> scrolled)
    {
        var moved = false;

        foreach (var (viewer, was) in scrolled)
        {
            if (viewer.Offset != was)
            {
                viewer.Offset = was;
                moved = true;
            }

            // And the bar is told where the document ended up, because the same clamp moved it and the
            // binding does not carry the correction back.
            foreach (var bar in viewer.GetVisualDescendants().OfType<ScrollBar>())
            {
                if (bar.Orientation == Orientation.Vertical
                    && bar.Maximum > 0
                    && Math.Abs(bar.Value - viewer.Offset.Y) > 0.5)
                {
                    bar.Value = Math.Clamp(viewer.Offset.Y, bar.Minimum, bar.Maximum);
                }
            }
        }

        return moved;
    }

    /// <summary>
    /// Copies the last render into a caller-owned buffer — the mapped staging texture, in production.
    /// </summary>
    public void CopyInto(IntPtr destination, int rowBytes) =>
        _target!.CopyPixels(
            new PixelRect(0, 0, _size.Width, _size.Height),
            destination,
            rowBytes * _size.Height,
            rowBytes);

    /// <summary>
    /// Delivers a press and a release at a point on the surface, as a mouse would, and says whether
    /// there was anything there to press.
    /// </summary>
    public bool Click(Point at)
    {
        if (Deepest(_surface, at) is not { } target)
        {
            return false;
        }

        // Decided before a single pointer event is raised, because for two kinds of control the gesture
        // itself is the problem rather than what it activates.
        var actionable = target.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(Actionable);

        // A combo box would open a popup, which is the crash.
        if (actionable is ComboBox combo)
        {
            return Choose(combo);
        }

        // A text box has nothing to type into it here: there is no keyboard in a cockpit and no focus to give
        // it.
        if (actionable is TextBox box)
        {
            Type(box);
            return true;
        }

        // A control that opens a window is left alone, and the panel says why rather than appearing to
        // have missed the press.
        if (actionable is not null && actionable.Classes.Contains(DesktopOnly))
        {
            Say(Refusal);
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

        // Released explicitly: a control that captured this pointer on the press would otherwise keep hold of
        // an object nothing will ever move again.
        pointer.Capture(null);

        Activate(target);

        return true;
    }

    /// <summary>Does what the release should have done, for the controls this panel is made of.</summary>
    public const string DesktopOnly = "desktop-only";

    /// <summary>What the panel says when it refuses a press on a control that opens a window.</summary>
    public const string Refusal = "Not currently supported in VR";

    /// <summary>
    /// Marks a control this surface must not press, because pressing it opens a window: a dialog on a
    /// desktop the Commander is not looking at is a dialog they cannot answer.
    /// </summary>
    public static void OpensAWindow(Control control) => control.Classes.Add(DesktopOnly);

    /// <summary>Whether this is a control a press means something to.</summary>
    private static bool Actionable(Control control) =>
        control is ComboBox or TextBox or ToggleButton or Button || control.Classes.Contains(DesktopOnly);

    /// <summary>Puts a combo box's list on the panel, as a chooser the ray can press.</summary>
    private bool Choose(ComboBox combo)
    {
        if (combo.ItemCount == 0)
        {
            return false;
        }

        // Never the control's own.
        combo.IsDropDownOpen = false;

        var items = new List<string>(combo.ItemCount);

        foreach (var item in combo.Items)
        {
            items.Add(item?.ToString() ?? string.Empty);
        }

        Offer(items, combo.SelectedIndex, chosen => combo.SelectedIndex = chosen);
        return true;
    }

    /// <summary>What the panel's own overlays are drawn in.</summary>
    private static T Painted<T>(T control, AvaloniaProperty property, string key)
        where T : Control
    {
        control.Bind(property, control.GetResourceObservable(key));

        return control;
    }

    /// <summary>One pressable thing, dressed so it can be read from across a cockpit.</summary>
    private static Button Pressable(string label, bool marked = false)
    {
        var button = new Button
        {
            Content = label,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            FontSize = Theming.TypeScale.Heading,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        Painted(button, TemplatedControl.ForegroundProperty, Theming.ThemeManager.TextKey);
        Painted(button, TemplatedControl.BorderBrushProperty, Theming.ThemeManager.BorderKey);

        // The marked row takes the accent rather than a blue of its own.
        Painted(
            button,
            TemplatedControl.BackgroundProperty,
            marked ? Theming.ThemeManager.AccentMutedKey : Theming.ThemeManager.SurfaceAltKey);

        return button;
    }

    /// <summary>Draws a list over the panel and calls back with what was pressed.</summary>
    public void Offer(IReadOnlyList<string> items, int selected, Action<int> pick)
    {
        var rows = new StackPanel { Spacing = 2 };

        for (var index = 0; index < items.Count; index++)
        {
            var at = index;

            var row = Pressable(items[index], marked: index == selected);

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
    /// The keys, in rows, as they are drawn — <see cref="PanelPrompts.Keys"/>, not a copy of it.
    /// </summary>
    private static string[] Keys => PanelPrompts.Keys;

    /// <summary>Puts a keyboard on the panel for one text box, and writes what was typed back into it.</summary>
    public void Type(TextBox box)
    {
        var typed = box.Text ?? string.Empty;

        var shown = new TextBox
        {
            Text = typed,
            IsReadOnly = true,
            FontSize = Theming.TypeScale.Heading,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 0, 0, 14),
        };

        Painted(shown, TemplatedControl.ForegroundProperty, Theming.ThemeManager.TextKey);
        Painted(shown, TemplatedControl.BackgroundProperty, Theming.ThemeManager.BackgroundKey);
        Painted(shown, TemplatedControl.BorderBrushProperty, Theming.ThemeManager.BorderKey);

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

        var done = Pressable("Done", marked: true);
        done.Height = 52;
        done.Padding = new Thickness(24, 0);

        done.Click += (_, _) =>
        {
            Dismiss();

            // Written once, at the end.
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

    /// <summary>Puts one line of state on the panel, over whatever is under it, until it is dismissed.</summary>
    public void Say(string line)
    {
        var text = new TextBlock
        {
            Text = line,
            FontSize = Theming.TypeScale.Heading,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
        };

        Painted(text, TextBlock.ForegroundProperty, Theming.ThemeManager.TextKey);

        var close = Pressable("Close", marked: true);
        close.Height = 52;
        close.Padding = new Thickness(24, 0);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Dismiss();

        var card = Card(new StackPanel { Children = { text, close } });
        card.MinWidth = Math.Min(460, _size.Width - 120);
        card.MaxWidth = Math.Min(560, _size.Width - 80);

        Overlay(card);
    }

    /// <summary>The card everything on this layer sits in.</summary>
    private Border Card(Control body)
    {
        var card = new Border
        {
            Child = body,
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),

            // Short of the panel, so it reads as something over the page rather than a new page.
            MaxHeight = Math.Max(180, _size.Height - 60),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Painted(card, Border.BackgroundProperty, Theming.ThemeManager.SurfaceKey);
        Painted(card, Border.BorderBrushProperty, Theming.ThemeManager.BorderKey);

        return card;
    }

    /// <summary>Puts a card over the page, and dims what is behind it.</summary>
    private void Overlay(Control card)
    {
        // The dimmer is also what makes a press anywhere else land on this layer rather than on the page
        // underneath: the panel must not be pressable while something is over it.
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

                    // And the click, which a real release also raises (Phase 39). Setting the property
                    // alone is a tick that does nothing. A checkbox whose handler hangs off <c>Click</c>
                    // — which is the right event for it, because it is the one that means the Commander did
                    // this rather than a rebuild did — saw the box change and never heard the press, so
                    // ticking a checklist line in the headset drew a tick and left the line open until the
                    // next rebuild rubbed it out.
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = toggle });
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
    /// How far from a scrollbar a ray may land and still count as being on it, in surface pixels.
    /// </summary>
    public const double AimTolerance = 28;

    /// <summary>
    /// How far the ray has to leave a bar to put it out, having lit it at <see cref="AimTolerance"/>
    /// (#29).
    /// </summary>
    public const double AimRelease = 42;

    /// <summary>The vertical scrollbar a ray at this point is aiming at, or null.</summary>
    public ScrollBar? ScrollbarNear(Point at)
    {
        ScrollBar? nearest = null;
        var closest = double.MaxValue;

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

            // The bar already lit keeps a wider berth than one being found for the first time, which is the
            // whole of the hysteresis: leaving costs more than arriving did (#29).
            var limit = ReferenceEquals(bar, _lit) ? AimRelease : AimTolerance;

            if (away <= limit && away < closest)
            {
                closest = away;
                nearest = bar;
            }
        }

        return nearest;
    }

    /// <summary>Puts a bar where the ray is pointing along it, top to bottom.</summary>
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

    /// <summary>Lights the control a ray is resting on, and puts out whatever it was resting on before.</summary>
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

    /// <summary>The space a point is expressed in, for callers that need to translate into it.</summary>
    public Control View => _surface;

    /// <summary>The window the view is hosted in.</summary>
    public Control Root => _root;

    /// <summary>
    /// One id for every synthetic press, because there is exactly one thing pointing at this surface at
    /// a time — the panel is carried with the same button that clicks it, so a second simultaneous
    /// pointer is not a state the gesture can be in.
    /// </summary>
    private const int PointerId = 47;

    /// <summary>
    /// The topmost thing under a point, found by walking the tree rather than by asking the framework.
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

        // The children go too, not just the window's content.
        _over.Children.Clear();
        _surface.Children.Clear();

        _root.Content = null;
        _root.Close();
    }
}
