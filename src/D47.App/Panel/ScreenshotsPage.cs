using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Screenshots: the JPEG copies of Elite's screenshots as a grid of tiles that open a viewer.</summary>
public sealed class ScreenshotsPage : UserControl, IPageSummary
{
    public const string PageKey = "screenshots";

    public const string SummaryText = "The pictures you have taken in Elite.";

    public const string EmptyText = "No screenshots yet. Press Elite's screenshot key, or say \"take a picture\", to take one.";

    public const string CodexTag = "CODEX PICTURE";

    private const double TileWidth = 260;

    private const double TileImageHeight = 146;

    private readonly ScreenshotSet _set;
    private readonly Func<string?> _currentSystem;
    private readonly Segment _sort = SortChoice();
    private readonly StackPanel _body = new() { Spacing = 12, Margin = new Thickness(0, 12, 0, 16) };
    private IReadOnlyList<ScreenshotItem> _items = [];
    private Task _thumbnails = Task.CompletedTask;
    private int _drawn;

    public ScreenshotsPage(ScreenshotSet set, Func<string?> currentSystem)
    {
        _set = set;
        _currentSystem = currentSystem;
        _sort.SelectionChanged += (_, _) => Fill();

        Content = _body;
    }

    public string Summary => SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>"12 screenshots · 31.4 MB".</summary>
    public static string CountLine(int count, long bytes) =>
        $"{count.ToString("N0", CultureInfo.InvariantCulture)} {(count == 1 ? "screenshot" : "screenshots")} · {Size(bytes)}";

    public static string Size(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{(bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture)} MB"
            : $"{Math.Max(1, (bytes + 1023) / 1024).ToString("N0", CultureInfo.InvariantCulture)} KB";

    public static string When(DateTimeOffset at) => at.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The panel keeps a level once built, so the folder is listed again each time the page is shown.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Draw();
    }

    internal static Control SystemName(ScreenshotItem item, string? current, double size)
    {
        var isCurrent = current is not null && string.Equals(current, item.System, StringComparison.OrdinalIgnoreCase);

        var system = new TextBlock
        {
            Text = item.System,
            FontFamily = Fonts.ChromeFamily,
            FontSize = size,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(system, TextBlock.ForegroundProperty, isCurrent ? ThemeManager.CyanKey : ThemeManager.AKey);

        var body = new TextBlock { Text = item.Body, FontFamily = Fonts.ChromeFamily, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        ThemeInk.Bind(body, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { system, body },
        };
    }

    internal static TextBlock CodexTagBlock()
    {
        var tag = new TextBlock
        {
            Name = "CodexPictureTag",
            Text = CodexTag,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.MetaSmall,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(tag, TextBlock.ForegroundProperty, ThemeManager.YellowKey);

        return tag;
    }

    internal static TextBlock Mono(string text)
    {
        var block = CreditsKit.Mono(text, TypeScale.MetaSmall, ThemeManager.GreyKey, right: false);
        block.VerticalAlignment = VerticalAlignment.Center;

        return block;
    }

    private static Segment SortChoice()
    {
        var sort = SortBar.Choice("ScreenshotSort", "Newest first", "Oldest first");
        sort.Width = 360;
        AutomationProperties.SetName(sort, "Sort by date taken");

        return sort;
    }

    private void Draw()
    {
        var generation = ++_drawn;

        _ = Load(generation);
    }

    private async Task Load(int generation)
    {
        var items = await Task.Run(() => _set.List());

        if (generation != _drawn)
        {
            return;
        }

        _items = items;
        Fill();

        await _thumbnails;
        await Task.Run(() => _set.PruneThumbnails(items));
    }

    private IReadOnlyList<ScreenshotItem> Ordered() =>
        _sort.SelectedIndex == 1 ? [.. _items.OrderBy(item => item.Shot.At)] : [.. _items.OrderByDescending(item => item.Shot.At)];

    private void Fill()
    {
        var ordered = Ordered();
        var current = _currentSystem();
        var generation = _drawn;
        var slots = new List<(Image Slot, ScreenshotItem Item)>();

        _body.Children.Clear();

        if (_items.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(EmptyText));

            return;
        }

        var open = new Button { Name = "ScreenshotsShowInFolder", Content = "Show in folder", MinHeight = TypeScale.MinimumTarget };
        open.Click += (_, _) => _set.Show(null);

        (_sort.Parent as Avalonia.Controls.Panel)?.Children.Remove(_sort);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 16 };
        CreditsKit.Place(head, 0, _sort);
        CreditsKit.Place(head, 1, Mono(CountLine(_items.Count, _items.Sum(item => item.Length))));
        CreditsKit.Place(head, 3, open);
        _body.Children.Add(head);

        var grid = new WrapPanel { Name = "ScreenshotGrid", Orientation = Orientation.Horizontal };

        for (var i = 0; i < ordered.Count; i++)
        {
            grid.Children.Add(Tile(ordered, i, current, slots));
        }

        _body.Children.Add(grid);

        _thumbnails = Task.Run(() => LoadThumbnails(slots, generation));
    }

    private Button Tile(IReadOnlyList<ScreenshotItem> ordered, int index, string? current, List<(Image, ScreenshotItem)> slots)
    {
        var item = ordered[index];
        var slot = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var picture = new Border { Width = TileWidth, Height = TileImageHeight, ClipToBounds = true, Child = slot };
        ThemeInk.Bind(picture, Border.BackgroundProperty, ThemeManager.SlabKey);
        slots.Add((slot, item));

        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 16 };
        CreditsKit.Place(line, 0, Mono(When(item.Shot.At)));

        if (item.CodexPicture)
        {
            CreditsKit.Place(line, 1, CodexTagBlock());
        }

        var text = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(12, 8, 12, 10),
            Children = { SystemName(item, current, TypeScale.Description), line },
        };

        var button = ListRow.Dress(new Button
        {
            Name = "Screenshot." + item.Stem,
            Content = new StackPanel { Width = TileWidth, Children = { picture, text } },
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, Gaps.Tile, Gaps.Tile),
            HorizontalContentAlignment = HorizontalAlignment.Left,
        });
        AutomationProperties.SetName(button, $"{item.System} {item.Body} {When(item.Shot.At)}".Replace("  ", " ", StringComparison.Ordinal));
        button.Click += (_, _) => ScreenshotViewer.Open(this, _set, ordered, index, _currentSystem);

        return button;
    }

    private void LoadThumbnails(List<(Image Slot, ScreenshotItem Item)> slots, int generation)
    {
        foreach (var (slot, item) in slots)
        {
            if (generation != _drawn)
            {
                return;
            }

            if (_set.Thumbnail(item) is { } small)
            {
                Dispatcher.UIThread.Post(() => slot.Source = small);
            }
        }
    }
}
