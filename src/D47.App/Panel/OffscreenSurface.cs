using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
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

    public void Dispose()
    {
        _target?.Dispose();
        _root.Content = null;
        _root.Close();
    }
}
