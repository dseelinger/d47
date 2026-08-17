using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
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

    private RenderTargetBitmap? _target;
    private PixelSize _size;

    public OffscreenSurface(Control view, PixelSize size)
    {
        _view = view;

        _root = new Window
        {
            ShowInTaskbar = false,
            Content = view,
        };

        Resize(size);
    }

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
    public RenderTargetBitmap Render()
    {
        var bounds = new Rect(0, 0, _size.Width, _size.Height);

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

        _target!.Render(_view);
        return _target;
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
        if (Deepest(_view, at) is not { } target)
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
        _root.Content = null;
        _root.Close();
    }
}
