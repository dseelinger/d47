using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// The settings page's navigation: area headings and place entries, the open place and area, and the
/// place picker shown once the nav column has collapsed.
/// </summary>
/// <param name="items">Holds the headings and entries.</param>
/// <param name="scroller">Scrolls <paramref name="items"/>.</param>
/// <param name="resources">Supplies theme resources.</param>
/// <param name="mark">Writes a name into a block with the query marked.</param>
/// <param name="open">Opens a place; called by an entry's press and the picker's selection.</param>
internal sealed class SettingsNav(
    StackPanel items, ScrollViewer scroller, Control resources, Action<TextBlock, string> mark, Action<int> open)
{
    private readonly List<AreaEntry> _areas = [];
    private readonly List<PlaceEntry> _places = [];
    private readonly List<int> _dropdownPlaces = [];
    private SettingsSift _sift = new(string.Empty, [], [], []);

    /// <summary>True while the nav is writing the picker, so its own selection change does not open a place.</summary>
    private bool _settingDropdown;

    private Stepper? _dropdown;

    public Stepper Dropdown => _dropdown ??= BuildDropdown();

    /// <summary>The open place, or −1 before one is open.</summary>
    public int ActiveSection { get; private set; } = -1;

    /// <summary>The area holding the open place, or −1 before one is open.</summary>
    public int ActiveArea { get; private set; } = -1;

    public string? ActiveAreaId =>
        ActiveArea >= 0 && ActiveArea < _areas.Count ? _areas[ActiveArea].Id : null;

    public int AreaCount => _areas.Count;

    public void Clear()
    {
        items.Children.Clear();
        _areas.Clear();
        _places.Clear();
        _dropdownPlaces.Clear();
        ActiveSection = -1;
        ActiveArea = -1;
        Dropdown.IsVisible = false;
    }

    /// <summary>Adds the area's heading, then an entry for each of its places in section order.</summary>
    public void AddArea(SettingsArea area)
    {
        var areaIndex = _areas.Count;
        var first = _places.Count;
        var heading = BuildHeading(area.Title, areaIndex);

        items.Children.Add(heading.Item);

        foreach (var place in area.Places)
        {
            var entry = BuildEntry(_places.Count, place.Title);

            items.Children.Add(entry.Item);
            _places.Add(entry);
        }

        _areas.Add(new AreaEntry(area.Id, area.Title, heading.Item, heading.Text, heading.Bar, first, _places.Count - first));
    }

    /// <summary>Which area a place belongs to, or −1.</summary>
    public int AreaOf(int section) =>
        _areas.FindIndex(area => section >= area.First && section < area.First + area.Count);

    public string AreaTitle(int area) => _areas[area].Title;

    /// <summary>
    /// The area's first place with a match while a query is typed, else its first place with a page,
    /// else its first place.
    /// </summary>
    public int FirstShownPlace(int area, SettingsSift sift)
    {
        var entry = _areas[area];
        var places = Enumerable.Range(entry.First, entry.Count).ToList();

        if (sift.Filtering && places.FirstOrDefault(i => sift.Places[i].Showing > 0, -1) is var matched and >= 0)
        {
            return matched;
        }

        return places.FirstOrDefault(i => sift.Places[i].Exists, entry.First);
    }

    /// <summary>Makes the place and its area the open ones, and scrolls its entry into view.</summary>
    public void Select(int section)
    {
        if (section < 0 || section >= _places.Count)
        {
            return;
        }

        ActiveSection = section;
        ActiveArea = AreaOf(section);

        var item = _places[section].Item;

        // Not laid out yet, which is the case during Build.
        if (item.Bounds.Height <= 0)
        {
            return;
        }

        var top = item.Bounds.Y;
        var bottom = top + item.Bounds.Height;
        var seen = scroller.Offset.Y;
        var floor = seen + scroller.Viewport.Height;

        if (top >= seen && bottom <= floor)
        {
            return;
        }

        // Scrolled to whichever edge it went past, so the list moves the least it can.
        scroller.Offset = scroller.Offset.WithY(top < seen ? top : bottom - scroller.Viewport.Height);
    }

    /// <summary>
    /// Shows each place that has a page, with its match count and the query marked in its name; while a
    /// query is typed, a place or area with no match is drawn in Grey2. Moves the open place to the first
    /// with a page when the open one has none.
    /// </summary>
    public void Apply(SettingsSift sift)
    {
        _sift = sift;

        for (var i = 0; i < _places.Count; i++)
        {
            var entry = _places[i];
            var matches = MatchesIn(i);

            mark(entry.Text, entry.Title);
            entry.Item.IsVisible = sift.Places[i].Exists;
            entry.Count.Text = matches.ToString(CultureInfo.InvariantCulture);
            entry.Count.IsVisible = matches > 0;
        }

        if (ActiveSection >= 0 && !sift.Places[ActiveSection].Exists
            && sift.Places.ToList().FindIndex(place => place.Exists) is var next and >= 0)
        {
            ActiveSection = next;
            ActiveArea = AreaOf(next);
        }

        for (var i = 0; i < _places.Count; i++)
        {
            PaintPlace(
                _places[i],
                i == ActiveSection ? NavPaint.Active
                : sift.Filtering && MatchesIn(i) == 0 ? NavPaint.Dim
                : NavPaint.Normal);
        }

        for (var a = 0; a < _areas.Count; a++)
        {
            var area = _areas[a];
            var places = Enumerable.Range(area.First, area.Count);

            mark(area.HeadingText, area.Title);
            area.Heading.IsVisible = places.Any(i => sift.Places[i].Exists);

            PaintArea(
                area,
                a == ActiveArea ? NavPaint.Active
                : sift.Filtering && !places.Any(i => MatchesIn(i) > 0) ? NavPaint.Dim
                : NavPaint.Normal);
        }

        SyncDropdown();
    }

    private int MatchesIn(int place) => _sift.Filtering ? _sift.Places[place].Showing : 0;

    private Stepper BuildDropdown()
    {
        var combo = new Stepper
        {
            Name = "AreaDropdown",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            IsVisible = false,
        };
        SettingsView.DressAsAChoice(combo);
        AutomationProperties.SetName(combo, "Page");

        combo.SelectionChanged += (_, _) =>
        {
            if (_settingDropdown || combo.SelectedIndex < 0)
            {
                return;
            }

            open(_dropdownPlaces[combo.SelectedIndex]);
        };

        return combo;
    }

    private void SyncDropdown()
    {
        _dropdownPlaces.Clear();
        var titles = new List<string>();

        for (var i = 0; i < _places.Count; i++)
        {
            if (!_sift.Places[i].Exists)
            {
                continue;
            }

            _dropdownPlaces.Add(i);
            titles.Add($"{_areas[AreaOf(i)].Title} › {_places[i].Title}");
        }

        _settingDropdown = true;
        try
        {
            if (Dropdown.ItemsSource is not IEnumerable<string> shown || !shown.SequenceEqual(titles))
            {
                Dropdown.ItemsSource = titles;
            }

            Dropdown.SelectedIndex = _dropdownPlaces.IndexOf(ActiveSection);
        }
        finally
        {
            _settingDropdown = false;
        }
    }

    private IBrush? Res(string key) => resources.FindResource(key) as IBrush;

    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, resources.GetResourceObservable(key));

    /// <summary>An area's title in the nav; pressing it opens the area's first place.</summary>
    private (Border Item, TextBlock Text, Border Bar) BuildHeading(string title, int areaIndex)
    {
        var text = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Secondary * Fonts.ChromeTracking,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var bar = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4, 8, 4),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AKey);

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(text);

        var item = new Border
        {
            Padding = new Thickness(8, 12, 8, 4),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = layout,
        };

        item.Classes.Add(SettingsView.NavAreaClass);
        item.PointerPressed += (_, _) => open(FirstShownPlace(areaIndex, _sift));

        return (item, text, bar);
    }

    private PlaceEntry BuildEntry(int index, string title)
    {
        var bar = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AKey);

        var text = new TextBlock
        {
            Text = title,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Body,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // In the name's own ink, so it follows the same states.
        var count = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
        count.Bind(TextBlock.ForegroundProperty, text.GetObservable(TextBlock.ForegroundProperty));

        var words = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(count, 1);
        words.Children.Add(text);
        words.Children.Add(count);

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(words);

        var item = new Border
        {
            Padding = new Thickness(8, 8),
            Margin = new Thickness(16, 0, 0, 0),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = layout,
        };

        item.Classes.Add(SettingsView.NavPlaceClass);
        item.PointerPressed += (_, _) => open(index);
        item.PointerEntered += (_, _) =>
        {
            if (index != ActiveSection)
            {
                item.Background = Res(ThemeManager.TileKey);
            }
        };
        item.PointerExited += (_, _) =>
        {
            if (index != ActiveSection)
            {
                item.Background = Brushes.Transparent;
            }
        };

        return new PlaceEntry(title, item, bar, text, count);
    }

    /// <summary>The open place: an A fill with Knock text. Another is Grey, and Grey2 when a query finds nothing in it.</summary>
    private void PaintPlace(PlaceEntry entry, NavPaint paint)
    {
        if (entry.Painted == paint)
        {
            return;
        }

        entry.Painted = paint;

        var active = paint == NavPaint.Active;

        entry.Bar.Opacity = active ? 1 : 0;
        entry.Text.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;

        entry.Fill?.Dispose();
        entry.Fill = null;

        entry.Ink?.Dispose();

        if (active)
        {
            entry.Fill = Themed(entry.Item, Border.BackgroundProperty, ThemeManager.AKey);
            entry.Ink = Themed(entry.Text, TextBlock.ForegroundProperty, ThemeManager.KnockKey);
        }
        else
        {
            // No resource for "nothing", so the fill is dropped rather than bound.
            entry.Item.Background = Brushes.Transparent;
            entry.Ink = Themed(
                entry.Text,
                TextBlock.ForegroundProperty,
                paint == NavPaint.Dim ? ThemeManager.Grey2Key : ThemeManager.GreyKey);
        }
    }

    /// <summary>The open place's area heading is marked as its parent.</summary>
    private void PaintArea(AreaEntry area, NavPaint paint)
    {
        if (area.Painted == paint)
        {
            return;
        }

        area.Painted = paint;

        area.Ink?.Dispose();
        area.Ink = Themed(
            area.HeadingText,
            TextBlock.ForegroundProperty,
            paint switch
            {
                NavPaint.Active => ThemeManager.WhiteKey,
                NavPaint.Dim => ThemeManager.Grey2Key,
                _ => ThemeManager.GreyKey,
            });

        area.HeadingBar.Opacity = paint == NavPaint.Active ? 1 : 0;

        area.Fill?.Dispose();
        area.Fill = null;

        if (paint == NavPaint.Active)
        {
            area.Fill = Themed(area.Heading, Border.BackgroundProperty, ThemeManager.Tile2Key);
        }
        else
        {
            area.Heading.Background = Brushes.Transparent;
        }
    }

    private enum NavPaint
    {
        Normal,
        Active,

        /// <summary>No match for the query.</summary>
        Dim,
    }

    private sealed record PlaceEntry(string Title, Border Item, Border Bar, TextBlock Text, TextBlock Count)
    {
        public NavPaint? Painted { get; set; }

        public IDisposable? Fill { get; set; }

        public IDisposable? Ink { get; set; }
    }

    private sealed record AreaEntry(
        string Id, string Title, Border Heading, TextBlock HeadingText, Border HeadingBar, int First, int Count)
    {
        public NavPaint? Painted { get; set; }

        public IDisposable? Ink { get; set; }

        public IDisposable? Fill { get; set; }
    }
}
