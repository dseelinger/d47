using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The headset's page for one screenshot: the whole picture, ◄ PREVIOUS, NEXT ►, "n of N" and the place and date line.</summary>
public sealed class ScreenshotPicturePage : UserControl
{
    public const string ImageName = "ScreenshotPicture";

    public const string PositionName = "ScreenshotPicturePosition";

    public const string FactsName = "ScreenshotPictureFacts";

    private readonly ScreenshotSet _set;
    private readonly Func<string?> _currentSystem;
    private readonly List<ScreenshotItem> _items;
    private readonly Image _image = new()
    {
        Name = ImageName,
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Border _frame = new() { Margin = new Thickness(0, 12) };
    private readonly Button _previous;
    private readonly Button _next;
    private readonly TextBlock _position = ScreenshotsPage.Mono(string.Empty);
    private readonly StackPanel _caption = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    private int _index;
    private int _shown;
    private bool _sized;

    public ScreenshotPicturePage(ScreenshotSet set, ScreenshotBrowse browse, string stem, Func<string?> currentSystem)
    {
        _set = set;
        _currentSystem = currentSystem;
        _items = [.. browse.Ordered];
        _index = _items.FindIndex(item => string.Equals(item.Stem, stem, StringComparison.Ordinal));

        _previous = Step("◄ PREVIOUS", "PreviousScreenshotPicture", -1);
        _next = Step("NEXT ►", "NextScreenshotPicture", 1);
        _position.Name = PositionName;

        _frame.Child = _image;

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { _previous, _next, _position },
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(_frame, 1);
        Grid.SetRow(_caption, 2);
        root.Children.Add(bar);
        root.Children.Add(_frame);
        root.Children.Add(_caption);

        if (_index < 0)
        {
            Content = CreditsKit.Message("That screenshot is gone.");

            return;
        }

        Content = root;
        _frame.SizeChanged += (_, _) =>
        {
            if (!_sized && _frame.Bounds.Width > 1 && _frame.Bounds.Height > 1)
            {
                _sized = true;
                Show();
            }
        };
        Labels();
    }

    private Button Step(string label, string name, int step)
    {
        var button = new Button { Name = name, Content = label, Height = TypeScale.MinimumTarget };
        button.Click += (_, _) => Move(step);

        return button;
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

    private void Labels()
    {
        _previous.IsEnabled = _index > 0;
        _next.IsEnabled = _index < _items.Count - 1;
        _position.Text = $"{_index + 1} of {_items.Count}";
        Caption(_items[_index], null);
    }

    private void Show()
    {
        var item = _items[_index];
        var generation = ++_shown;

        Labels();
        _image.Source = null;

        if (!_sized)
        {
            return;
        }

        var limit = PanelResolution.Steps[^1];
        var width = (int)Math.Clamp(Math.Round(_frame.Bounds.Width), 1, limit.Width);
        var height = (int)Math.Clamp(Math.Round(_frame.Bounds.Height), 1, limit.Height);

        _ = Present(item, generation, width, height);
    }

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
        _caption.Children.Add(ScreenshotsPage.SystemName(item, _currentSystem(), TypeScale.Description));

        var facts = ScreenshotsPage.When(item.Shot.At)
            + (size is { } known ? $" · {known.Width} × {known.Height}" : string.Empty)
            + $" · {ScreenshotsPage.Size(item.Length)}";
        var meta = ScreenshotsPage.Mono(facts);
        meta.Name = FactsName;
        _caption.Children.Add(meta);

        if (item.CodexPicture)
        {
            _caption.Children.Add(ScreenshotsPage.CodexTagBlock());
        }
    }
}
