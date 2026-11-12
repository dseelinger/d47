using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>A screenshot at the window's size over the whole window, with Previous, Next, Rename, Show in folder, Delete and Close; Left, Right and Escape work too.</summary>
internal sealed class ScreenshotViewer : Grid
{
    private const double SideMargin = 24;

    private const double VerticalMargin = 72;

    private readonly OverlayLayer _layer;
    private readonly ScreenshotSet _set;
    private readonly List<ScreenshotItem> _items;
    private readonly ModalHost _modals = new();
    private readonly Action? _changed;
    private readonly Func<string?> _currentSystem;
    private readonly Image _image = new()
    {
        Name = "ViewedScreenshot",
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(SideMargin, VerticalMargin),
    };
    private readonly Button _previous;
    private readonly Button _next;
    private readonly TextBlock _position;
    private readonly StackPanel _caption = new() { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14) };
    private int _index;
    private int _shown;

    private ScreenshotViewer(OverlayLayer layer, ScreenshotSet set, IReadOnlyList<ScreenshotItem> items, int index, Func<string?> currentSystem, Action? changed)
    {
        _layer = layer;
        _set = set;
        _items = [.. items];
        _changed = changed;
        _currentSystem = currentSystem;
        _index = index;

        ThemeInk.Bind(this, BackgroundProperty, ThemeManager.BgKey);
        Children.Add(_image);

        _previous = Tool("◄ Previous", "PreviousScreenshot", () => Move(-1));
        _next = Tool("Next ►", "NextScreenshot", () => Move(1));
        _position = ScreenshotsPage.Mono(string.Empty);
        _position.Name = "ScreenshotPosition";
        _position.Margin = new Thickness(14, 0, 0, 0);

        var rename = Tool("Rename", "RenameScreenshot", Rename);
        var show = Tool("Show in folder", "ShowScreenshotInFolder", () => _set.Show(_items[_index].Shot.Path));
        var delete = Tool("Delete", "DeleteScreenshot", Delete);
        delete.Classes.Add(Settings.SettingsView.DestructiveClass);
        var close = Tool("Close", "CloseScreenshot", Dismiss);
        close.Margin = new Thickness(14, 0, 0, 0);

        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14), LastChildFill = false };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(delete, Dock.Right);
        DockPanel.SetDock(show, Dock.Right);
        DockPanel.SetDock(rename, Dock.Right);

        foreach (var control in new Control[] { _previous, _next, _position, close, delete, show, rename })
        {
            bar.Children.Add(control);
        }

        Children.Add(bar);
        Children.Add(_caption);
        Children.Add(_modals);

        Focusable = true;

        // OverlayLayer arranges a child at the size it asks for, so the layer's size is asked for here.
        Fill();
        layer.SizeChanged += Filled;
        DetachedFromVisualTree += (_, _) => layer.SizeChanged -= Filled;

        AddHandler(KeyDownEvent, Keys, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Focus();

        Show();
    }

    internal static ScreenshotViewer? Open(Visual from, ScreenshotSet set, IReadOnlyList<ScreenshotItem> items, int index, Func<string?> currentSystem, Action? changed = null)
    {
        if (OverlayLayer.GetOverlayLayer(from) is not { } layer || index < 0 || index >= items.Count)
        {
            return null;
        }

        var viewer = new ScreenshotViewer(layer, set, items, index, currentSystem, changed);
        layer.Children.Add(viewer);

        return viewer;
    }

    private static Button Tool(string label, string name, Action pressed)
    {
        var button = LoadoutPages.Press(label, pressed);
        button.Name = name;
        button.MinHeight = TypeScale.MinimumTarget;
        button.Margin = new Thickness(0, 0, Gaps.Tile, 0);

        return button;
    }

    private void Keys(object? sender, KeyEventArgs e)
    {
        if (_modals.Top is not null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                Dismiss();
                break;
            case Key.Left:
                Move(-1);
                break;
            case Key.Right:
                Move(1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Move(int step)
    {
        var next = _index + step;

        if (next < 0 || next >= _items.Count)
        {
            return;
        }

        _index = next;
        Show();
    }

    private void Show()
    {
        var item = _items[_index];
        var generation = ++_shown;

        _previous.IsEnabled = _index > 0;
        _next.IsEnabled = _index < _items.Count - 1;
        _position.Text = $"{_index + 1} of {_items.Count}";
        Caption(item, null);
        _image.Source = null;

        var width = (int)Math.Max(1, Math.Round(Fitted(Width, 1280) - (2 * SideMargin)));
        var height = (int)Math.Max(1, Math.Round(Fitted(Height, 800) - (2 * VerticalMargin)));

        _ = Present(item, generation, width, height);
    }

    private static double Fitted(double length, double fallback) => double.IsNaN(length) || length < 200 ? fallback : length;

    private async Task Present(ScreenshotItem item, int generation, int width, int height)
    {
        var (picture, size) = await Task.Run(() => (_set.Decode(item.Shot.Path, width, height), _set.PixelSize(item.Shot.Path)));

        if (generation != _shown)
        {
            return;
        }

        _image.Source = picture;
        Caption(item, size);
    }

    private void Caption(ScreenshotItem item, (int Width, int Height)? size)
    {
        _caption.Children.Clear();
        _caption.Children.Add(ScreenshotsPage.SystemName(item, _currentSystem(), 15));

        var facts = $"{ScreenshotsPage.When(item.Shot.At)}"
            + (size is { } known ? $" · {known.Width} × {known.Height}" : string.Empty)
            + $" · {ScreenshotsPage.Size(item.Length)}";
        var meta = ScreenshotsPage.Mono(facts);
        meta.Name = "ScreenshotFacts";
        _caption.Children.Add(meta);

        if (item.CodexPicture)
        {
            _caption.Children.Add(ScreenshotsPage.CodexTagBlock());
        }
    }

    private void Rename()
    {
        var item = _items[_index];
        var dialog = new RenameScreenshotDialog(Path.GetFileName(item.Shot.Path), stem => Renamed(item, stem));

        _ = Ask(dialog);
    }

    private async Task<ScreenshotRename> Renamed(ScreenshotItem item, string stem)
    {
        var result = await Task.Run(() => _set.Rename(item, stem));

        if (result.Path is { } path && _items.IndexOf(item) is >= 0 and var at)
        {
            _items[at] = item with { Shot = item.Shot with { Path = path } };

            if (at == _index)
            {
                Show();
            }

            _changed?.Invoke();
        }

        return result;
    }

    private async void Delete()
    {
        var item = _items[_index];
        var (bmp, entries) = await Task.Run(() => (_set.BmpOf(item) is not null, _set.CodexEntriesCut(item)));

        if (_items.IndexOf(item) < 0)
        {
            return;
        }

        var dialog = new DeleteScreenshotDialog(Path.GetFileName(item.Shot.Path), bmp, entries, withBmp => Deleted(item, withBmp));

        _ = Ask(dialog);
    }

    private async Task<string?> Deleted(ScreenshotItem item, bool withBmp)
    {
        var reason = await Task.Run(() => _set.Delete(item, withBmp));

        if (reason is not null)
        {
            return reason;
        }

        var at = _items.IndexOf(item);
        _items.RemoveAt(at);
        _changed?.Invoke();

        if (at >= _items.Count)
        {
            Dismiss();
        }
        else
        {
            _index = at;
            Show();
        }

        return null;
    }

    private async Task Ask(ModalDialog dialog)
    {
        await _modals.Show(dialog);
        Focus();
    }

    private void Dismiss() => _layer.Children.Remove(this);

    private void Filled(object? sender, SizeChangedEventArgs e) => Fill();

    private void Fill()
    {
        Width = _layer.Bounds.Width;
        Height = _layer.Bounds.Height;
    }
}
