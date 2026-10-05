using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Hulls;

namespace D47.App.Panel;

/// <summary>One hull's camera, shared by the page's viewer and the whole-window viewer.</summary>
internal sealed class HullPose(HullMesh mesh)
{
    private HullCamera _camera = HullCamera.Rest(mesh);

    public HullMesh Mesh { get; } = mesh;

    public HullCamera Camera
    {
        get => _camera;
        set
        {
            if (_camera == value)
            {
                return;
            }

            _camera = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;

    /// <summary>The rest pose, keeping the light level.</summary>
    public void Rest() => Camera = HullCamera.Rest(Mesh) with { Light = _camera.Light };
}

/// <summary>
/// A hull mesh drawn on the CPU into a bitmap sized to the control in device pixels, turned with the mouse and
/// the keyboard. Draws only on input, resize or theme change, at most once per frame.
/// </summary>
internal sealed class HullViewer : Control
{
    /// <summary>The page viewer's height for its width, the shape of the 4K still it stands in for.</summary>
    private const double Aspect = 9.0 / 16.0;

    /// <summary>One arrow press, in the camera's pixels: 5°.</summary>
    private const float KeyTurn = 5f / HullCamera.DegreesPerPixel;

    /// <summary>One Ctrl+arrow press, as a share of the viewer's height.</summary>
    private const double KeyPan = 0.05;

    private readonly HullPose _pose;

    private IDisposable[] _inks = [];

    private WriteableBitmap? _bitmap;
    private uint[] _pixels = [];
    private bool _queued;
    private Point _from;
    private MouseButton _dragging = MouseButton.None;

    internal HullViewer(HullPose pose)
    {
        _pose = pose;

        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        DoubleTapped += (_, e) =>
        {
            _pose.Rest();
            e.Handled = true;
        };
    }

    /// <summary>Takes whatever height it is given, rather than the still's shape.</summary>
    internal bool Fills { get; init; }

    internal HullPose Pose => _pose;

    /// <summary>The last frame drawn, or null before the first.</summary>
    internal WriteableBitmap? Frame => _bitmap;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;

        return Fills && !double.IsInfinity(availableSize.Height)
            ? new Size(width, availableSize.Height)
            : new Size(width, width * Aspect);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _pose.Changed += Request;

        // A theme change replaces these brushes, which is the redraw's cue.
        _inks =
        [
            Application.Current!.Resources.GetResourceObservable(ThemeManager.BgKey).Subscribe(new AnonymousObserver<object?>(_ => Request())),
            Application.Current!.Resources.GetResourceObservable(ThemeManager.WhiteKey).Subscribe(new AnonymousObserver<object?>(_ => Request())),
        ];

        Request();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _pose.Changed -= Request;

        foreach (var ink in _inks)
        {
            ink.Dispose();
        }

        _inks = [];
        _bitmap?.Dispose();
        _bitmap = null;
        _pixels = [];
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            Request();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap is { } bitmap)
        {
            context.DrawImage(bitmap, new Rect(Bounds.Size));
        }
    }

    /// <summary>Asks for one draw on the next frame; further asks before it runs are folded into it.</summary>
    private void Request()
    {
        if (_queued || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        _queued = true;
        top.RequestAnimationFrame(_ =>
        {
            _queued = false;
            Draw();
        });
    }

    private void Draw()
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var scale = top.RenderScaling;
        var width = (int)Math.Ceiling(Bounds.Width * scale);
        var height = (int)Math.Ceiling(Bounds.Height * scale);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height), new Vector(96 * scale, 96 * scale), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pixels = new uint[width * height];
        }

        HullRasteriser.Draw(_pose.Mesh, _pose.Camera, _pixels, width, height, Shade());

        using (var frame = _bitmap.Lock())
        {
            // The same bits as int, which is what Marshal.Copy takes.
            var bits = Unsafe.As<int[]>(_pixels);

            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(bits, row * width, frame.Address + (row * frame.RowBytes), width);
            }
        }

        InvalidateVisual();
    }

    /// <summary>The theme's ground, and its primary text colour for a fully lit face.</summary>
    private HullShade Shade() => new(Colour(ThemeManager.WhiteKey, Colors.White), Colour(ThemeManager.BgKey, Colors.Black));

    private uint Colour(string key, Color fallback)
    {
        var colour = this.TryFindResource(key, out var found) && found is ISolidColorBrush brush ? brush.Color : fallback;

        uint Premultiplied(byte channel) => (uint)Math.Round(channel * colour.A / 255.0);

        return ((uint)colour.A << 24) | (Premultiplied(colour.R) << 16) | (Premultiplied(colour.G) << 8) | Premultiplied(colour.B);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this).Properties;

        Focus(NavigationMethod.Pointer);

        _dragging = point.IsLeftButtonPressed ? MouseButton.Left
            : point.IsRightButtonPressed ? MouseButton.Right
            : point.IsMiddleButtonPressed ? MouseButton.Middle
            : MouseButton.None;

        if (_dragging == MouseButton.None)
        {
            return;
        }

        _from = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragging == MouseButton.None)
        {
            return;
        }

        var at = e.GetPosition(this);
        var dx = (float)(at.X - _from.X);
        var dy = (float)(at.Y - _from.Y);
        _from = at;

        var camera = _pose.Camera;

        _pose.Camera = _dragging != MouseButton.Left ? camera.Pan(dx, dy, (int)Math.Round(Bounds.Height))
            : e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? camera.Roll(dx)
            : camera.Turn(dx, dy);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging == MouseButton.None)
        {
            return;
        }

        _dragging = MouseButton.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _dragging = MouseButton.None;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        _pose.Camera = _pose.Camera.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Moved(_pose.Camera, e.Key, e.KeyModifiers, Bounds.Height) is { } moved)
        {
            _pose.Camera = moved;
            e.Handled = true;
        }
        else if (e.Key == Key.Home)
        {
            _pose.Rest();
            e.Handled = true;
        }
    }

    /// <summary>Where one key press moves the camera, or null for a key the viewer does not take.</summary>
    internal static HullCamera? Moved(HullCamera camera, Key key, KeyModifiers modifiers, double height)
    {
        var (x, y) = key switch
        {
            Key.Left => (-1f, 0f),
            Key.Right => (1f, 0f),
            Key.Up => (0f, -1f),
            Key.Down => (0f, 1f),
            _ => (0f, 0f),
        };

        if (x != 0 || y != 0)
        {
            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                var step = (float)(height * KeyPan);

                return camera.Pan(x * step, y * step, (int)Math.Round(height));
            }

            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                return x == 0 ? null : camera.Roll(x * KeyTurn);
            }

            return camera.Turn(x * KeyTurn, y * KeyTurn);
        }

        return key switch
        {
            Key.OemPlus or Key.Add or Key.PageUp => camera.Zoom(1),
            Key.OemMinus or Key.Subtract or Key.PageDown => camera.Zoom(-1),
            _ => null,
        };
    }
}

/// <summary>The marks the page viewer and the whole-window viewer share.</summary>
internal static class HullMarks
{
    internal const string Hints = "DRAG TURN · SHIFT DRAG ROLL · RIGHT DRAG PAN · WHEEL ZOOM · DOUBLE CLICK REST";

    /// <summary>A 32px glyph face in a 44px target, with its label as tooltip and accessible name.</summary>
    internal static Button Glyph(string glyph, string says)
    {
        var button = new Button
        {
            Theme = Application.Current?.FindResource("D47.GlyphButton") as ControlTheme,
            Width = TypeScale.MinimumTarget,
            Height = TypeScale.MinimumTarget,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = Glyphs.Text(glyph, TypeScale.Glyph),
        };

        ToolTip.SetTip(button, says);
        AutomationProperties.SetName(button, says);

        return button;
    }

    /// <summary>Returns the pose to rest, keeping the light.</summary>
    internal static Button Reset(HullPose pose)
    {
        var reset = Glyph(Glyphs.ResetText, "Reset the view");
        reset.Click += (_, _) => pose.Rest();

        return reset;
    }

    /// <summary>The LIGHT label and its level, 0 to 2 in tenths, following the pose while on screen.</summary>
    internal static (TextBlock Label, Level Level) Light(HullPose pose)
    {
        var label = Words("LIGHT", TypeScale.Meta);

        var level = new Level
        {
            Minimum = 0,
            Maximum = HullCamera.MaxLight,
            Step = 0.1,
            Value = pose.Camera.Light,
            VerticalAlignment = VerticalAlignment.Center,
        };

        AutomationProperties.SetName(level, "Light");
        level.ValueChanged += (_, _) => pose.Camera = pose.Camera with { Light = (float)level.Value };

        void Follow() => level.Value = Math.Round(pose.Camera.Light, 1);

        level.AttachedToVisualTree += (_, _) =>
        {
            pose.Changed += Follow;
            Follow();
        };
        level.DetachedFromVisualTree += (_, _) => pose.Changed -= Follow;

        return (label, level);
    }

    /// <summary>The hint line: chrome type, upper case, tracked, grey.</summary>
    internal static TextBlock Hint(string text)
    {
        var hint = Words(text, TypeScale.MetaSmall);
        hint.TextWrapping = TextWrapping.Wrap;

        return hint;
    }

    private static TextBlock Words(string text, double size)
    {
        var words = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = size,
            LetterSpacing = size * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };

        LoadoutPages.Themed(words, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return words;
    }
}

/// <summary>The turnable hull over the whole window, at the page viewer's pose.</summary>
internal sealed class HullViewerFull : Grid
{
    private readonly OverlayLayer _layer;
    private readonly HullViewer _viewer;

    internal HullViewerFull(HullPose pose, string name, OverlayLayer layer)
    {
        _layer = layer;
        _viewer = new HullViewer(pose) { Fills = true };

        Children.Add(_viewer);
        LoadoutPages.Themed(this, BackgroundProperty, ThemeManager.BgKey);

        var (label, level) = HullMarks.Light(pose);
        level.Width = 240;

        var strip = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(14, 4),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children = { label, level, HullMarks.Reset(pose) },
            },
        };

        LoadoutPages.Themed(strip, Border.BackgroundProperty, ThemeManager.BarKey);
        Children.Add(strip);

        var close = LoadoutPages.Press("Close", Dismiss);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(14);
        Children.Add(close);

        var title = new TextBlock
        {
            Text = name,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Subheading,
            LetterSpacing = TypeScale.Subheading * Fonts.ChromeTracking,
        };

        LoadoutPages.Themed(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(14),
            Spacing = 4,
            IsHitTestVisible = false,
            Children = { title, HullMarks.Hint(HullMarks.Hints + " · ESC CLOSE") },
        });

        // OverlayLayer arranges each child at the size it asked for, so the window's size is asked for here.
        Fill();
        layer.SizeChanged += Filled;
        DetachedFromVisualTree += (_, _) => layer.SizeChanged -= Filled;

        // The viewer passes Escape on, so it is taken here whichever control has focus.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Dismiss();
                e.Handled = true;
            }
        };

        // Posted, because focus is refused to a control that has not been laid out yet.
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => _viewer.Focus());
    }

    private void Dismiss() => _layer.Children.Remove(this);

    private void Filled(object? sender, SizeChangedEventArgs e) => Fill();

    private void Fill()
    {
        Width = _layer.Bounds.Width;
        Height = _layer.Bounds.Height;
    }
}
