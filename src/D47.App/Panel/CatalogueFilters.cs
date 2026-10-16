using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>
/// The Catalogue's filter as a sentence of tokens, the panel of the token pressed opening inside the page under it,
/// and the results line with Clear filters.
/// </summary>
internal sealed class CatalogueFilters : StackPanel
{
    public const string TokenClass = "catalogue-token";

    public const string LevelHint = "Tick any. With none ticked, every level shows.";

    public const string TokenHint = "Press an orange word to change it.";

    private enum Kind
    {
        Level,
        Compare,
        Length,
        Rating,
        Sort,
    }

    private static readonly string[] LevelNames = ["New commander", "Mid-range", "Endgame"];
    private static readonly string[] CompareLabels = ["Any length", "At least", "Exactly", "At most"];
    private static readonly string[] RatingLabels = ["Any rating", "4 stars & up", "3 stars & up", "2 stars & up", "1 star & up"];
    private static readonly string[] SortLabels = ["Catalogue order", "Highest rating"];

    private readonly Dictionary<Kind, Button> _tokens = [];
    private readonly Dictionary<Kind, TextBlock> _values = [];
    private readonly Dictionary<Kind, IDisposable> _inks = [];
    private readonly Dictionary<Kind, Control> _panels = [];
    private readonly List<CheckBox> _levelBoxes = [];
    private readonly Dictionary<Kind, Segment> _segments = [];
    private readonly Border _panelHost;
    private readonly TextBlock _count;
    private readonly Button _clear;
    private readonly Control[] _lengthControls;
    private readonly Control[] _ratingControls;
    private readonly HashSet<string> _levels;
    private StoryLengthCompare _compare;
    private string _length;
    private int? _minStars;
    private StorySort _sort;
    private Kind? _open;
    private bool _syncing;
    private bool _ratingsShown = true;

    public CatalogueFilters(StoryFilter start)
    {
        _levels = [.. start.Levels ?? []];
        _compare = start.Compare;
        _length = StoryPacing.Find(start.Length)?.Key ?? StoryPacing.OneMonth.Key;
        _minStars = start.MinStars;
        _sort = start.Sort;

        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Kind.Level, "Level"));
        sentence.Children.Add(Words("stories of"));
        sentence.Children.Add(Token(Kind.Compare, "Length"));
        _lengthControls = [Token(Kind.Length, "Story length")];
        sentence.Children.Add(_lengthControls[0]);
        _ratingControls = [Words("with"), Token(Kind.Rating, "Rating"), Words("sorted by"), Token(Kind.Sort, "Sort")];

        foreach (var control in _ratingControls)
        {
            sentence.Children.Add(control);
        }

        var hint = AdventuresPage.Text(TokenHint, TypeScale.Body, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 4, 0, 0);

        _panelHost = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 0), IsVisible = false };
        _panelHost.Bind(Border.BackgroundProperty, Resource(ThemeManager.BarKey));
        _panelHost.Bind(Border.BorderBrushProperty, Resource(ThemeManager.AKey));

        _count = new TextBlock { FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center };
        _count.Bind(TextBlock.ForegroundProperty, Resource(ThemeManager.GreyKey));

        _clear = new Button { Content = "Clear filters", Height = TypeScale.MinimumTarget, MinHeight = TypeScale.MinimumTarget, IsVisible = false };
        _clear.Click += (_, _) => Clear();

        var results = new DockPanel { Margin = new Thickness(0, 10, 0, 6) };
        DockPanel.SetDock(_clear, Dock.Right);
        results.Children.Add(_clear);
        results.Children.Add(_count);

        Children.Add(sentence);
        Children.Add(hint);
        Children.Add(_panelHost);
        Children.Add(results);

        Refresh();
    }

    /// <summary>Raised after the Commander changes the filter.</summary>
    public event EventHandler? Changed;

    public StoryFilter Filter => new(
        StoryCard.Levels.Where(_levels.Contains).ToList() is { Count: > 0 } levels ? levels : null,
        _compare,
        _length,
        _minStars,
        _sort);

    /// <summary>Whether the rating and sort tokens show: the Story ratings setting.</summary>
    public bool RatingsShown
    {
        get => _ratingsShown;
        set
        {
            if (_ratingsShown == value)
            {
                return;
            }

            _ratingsShown = value;
            Refresh();
        }
    }

    /// <summary>Draws "N of M stories", and Clear filters while <paramref name="filtered"/>.</summary>
    public void ShowResults(int shown, int total, bool filtered)
    {
        _count.Inlines ??= [];
        _count.Inlines.Clear();
        _count.Inlines.Add(Fonts.Mono(shown.ToString(CultureInfo.InvariantCulture)));
        _count.Inlines.Add(new Run(" of "));
        _count.Inlines.Add(Fonts.Mono(total.ToString(CultureInfo.InvariantCulture)));
        _count.Inlines.Add(new Run(" stories"));
        _clear.IsVisible = filtered;
    }

    private void Clear()
    {
        _levels.Clear();
        _compare = StoryLengthCompare.Any;
        _minStars = null;
        _sort = StorySort.Catalogue;
        _open = null;
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static TextBlock Words(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Sentence,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        block.Bind(TextBlock.ForegroundProperty, Resource(ThemeManager.WhiteKey));
        return block;
    }

    private Button Token(Kind kind, string name)
    {
        var value = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Section,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Section * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var caret = new TextBlock
        {
            Text = "▼",
            FontSize = TypeScale.Caption,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        caret.Bind(TextBlock.ForegroundProperty, value.GetObservable(TextBlock.ForegroundProperty));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(value);
        row.Children.Add(caret);

        var button = new Button
        {
            Content = row,
            Height = TypeScale.MinimumTarget,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            Margin = new Thickness(0, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        button.Classes.Add(ListRow.Class);
        button.Classes.Add(TokenClass);
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => Toggle(kind);

        _tokens[kind] = button;
        _values[kind] = value;
        return button;
    }

    private void Toggle(Kind kind)
    {
        _open = _open == kind ? null : kind;
        Refresh();
    }

    private int Selected(Kind kind) => kind switch
    {
        Kind.Compare => (int)_compare,
        Kind.Length => StoryPacing.All.ToList().FindIndex(pacing => pacing.Key == _length),
        Kind.Rating => _minStars is { } stars ? Math.Clamp(5 - stars, 0, RatingLabels.Length - 1) : 0,
        _ => (int)_sort,
    };

    private void Refresh()
    {
        _syncing = true;

        _lengthControls[0].IsVisible = _compare != StoryLengthCompare.Any;

        foreach (var control in _ratingControls)
        {
            control.IsVisible = _ratingsShown;
        }

        if ((!_ratingsShown && _open is Kind.Rating or Kind.Sort) || (_open == Kind.Length && _compare == StoryLengthCompare.Any))
        {
            _open = null;
        }

        _values[Kind.Level].Text = LevelText().ToUpperInvariant();
        _values[Kind.Compare].Text = CompareLabels[(int)_compare].ToUpperInvariant();
        _values[Kind.Length].Text = (StoryPacing.Find(_length)?.Name ?? _length).ToUpperInvariant();
        _values[Kind.Rating].Text = RatingLabels[Selected(Kind.Rating)].ToUpperInvariant();
        _values[Kind.Sort].Text = SortLabels[(int)_sort].ToUpperInvariant();

        foreach (var (kind, token) in _tokens)
        {
            var open = _open == kind;

            token.Classes.Set(ListRow.SelectedClass, open);
            _inks.GetValueOrDefault(kind)?.Dispose();
            _inks[kind] = _values[kind].Bind(TextBlock.ForegroundProperty, Resource(open ? ThemeManager.KnockKey : ThemeManager.AKey));
        }

        foreach (var (kind, segment) in _segments)
        {
            segment.SelectedIndex = Selected(kind);
        }

        for (var i = 0; i < _levelBoxes.Count; i++)
        {
            _levelBoxes[i].IsChecked = _levels.Contains(StoryCard.Levels[i]);
        }

        _panelHost.IsVisible = _open is not null;
        _panelHost.Child = _open is { } opened ? PanelFor(opened) : null;

        _syncing = false;
    }

    private string LevelText()
    {
        var ticked = StoryCard.Levels.Where(_levels.Contains).ToList();

        return ticked.Count == 0 || ticked.Count == StoryCard.Levels.Count
            ? "All"
            : string.Join(" and ", ticked.Select(level => LevelNames[StoryCard.Levels.ToList().IndexOf(level)]));
    }

    private Control PanelFor(Kind kind)
    {
        if (_panels.TryGetValue(kind, out var built))
        {
            return built;
        }

        return _panels[kind] = kind switch
        {
            Kind.Level => LevelPanel(),
            Kind.Compare => OptionsPanel(kind, CompareLabels, "Length", index => _compare = (StoryLengthCompare)index),
            Kind.Length => OptionsPanel(kind, [.. StoryPacing.All.Select(pacing => pacing.Name)], "Story length", index => _length = StoryPacing.All[index].Key),
            Kind.Rating => OptionsPanel(kind, RatingLabels, "Rating", index => _minStars = index == 0 ? null : 5 - index),
            _ => OptionsPanel(kind, SortLabels, "Sort", index => _sort = (StorySort)index),
        };
    }

    private Segment OptionsPanel(Kind kind, IReadOnlyList<string> options, string name, Action<int> pick)
    {
        var segment = new Segment { ItemsSource = options, SelectedIndex = Selected(kind) };
        AutomationProperties.SetName(segment, name + " options");

        segment.SelectionChanged += (_, _) =>
        {
            if (_syncing || segment.SelectedIndex < 0)
            {
                return;
            }

            pick(segment.SelectedIndex);
            _open = null;
            Refresh();
            Changed?.Invoke(this, EventArgs.Empty);
        };

        _segments[kind] = segment;
        return segment;
    }

    private StackPanel LevelPanel()
    {
        var panel = new StackPanel { Spacing = Gaps.Tile };

        for (var i = 0; i < StoryCard.Levels.Count; i++)
        {
            var level = StoryCard.Levels[i];
            var (box, _) = LabeledCheckBox.Build(LevelNames[i]);

            box.IsChecked = _levels.Contains(level);
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.IsCheckedChanged += (_, _) =>
            {
                if (_syncing)
                {
                    return;
                }

                if (box.IsChecked == true)
                {
                    _levels.Add(level);
                }
                else
                {
                    _levels.Remove(level);
                }

                Refresh();
                Changed?.Invoke(this, EventArgs.Empty);
            };

            _levelBoxes.Add(box);
            panel.Children.Add(box);
        }

        var hint = AdventuresPage.Text(LevelHint, TypeScale.Small, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(hint);

        return panel;
    }

    private static IObservable<object?> Resource(string key) =>
        Application.Current!.Resources.GetResourceObservable(key);
}
