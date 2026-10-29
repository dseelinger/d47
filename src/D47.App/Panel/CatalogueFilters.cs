using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>
/// The Catalogue's filter as a sentence of tokens, the panel of the token pressed opening inside the page under it,
/// and the results line with Clear filters.
/// </summary>
internal sealed class CatalogueFilters : TokenFilterBar<CatalogueFilters.Kind>
{
    public const string LevelHint = "Tick any. With none ticked, every level shows.";

    internal enum Kind
    {
        Level,
        Compare,
        Length,
        Black,
        Rating,
        Sort,
    }

    private static readonly string[] LevelNames = ["New commander", "Mid-range", "Endgame"];
    private static readonly string[] CompareLabels = ["Any length", "At least", "Exactly", "At most"];
    private static readonly string[] BlackLabels = ["Anywhere", "In the black"];
    private static readonly string[] RatingLabels = ["Any rating", "4 stars & up", "3 stars & up", "2 stars & up", "1 star & up"];
    private static readonly string[] SortLabels = ["Catalogue order", "Highest rating"];

    private readonly Dictionary<Kind, Control> _panels = [];
    private readonly List<CheckBox> _levelBoxes = [];
    private readonly Dictionary<Kind, Segment> _segments = [];
    private readonly Control[] _lengthControls;
    private readonly Control[] _ratingControls;
    private readonly HashSet<string> _levels;
    private StoryLengthCompare _compare;
    private string _length;
    private int? _minStars;
    private StorySort _sort;
    private bool _black;
    private bool _ratingsShown = true;

    public CatalogueFilters(StoryFilter start)
        : base(null)
    {
        _levels = [.. start.Levels ?? []];
        _compare = start.Compare;
        _length = StoryPacing.Find(start.Length)?.Key ?? StoryPacing.OneMonth.Key;
        _minStars = start.MinStars;
        _sort = start.Sort;
        _black = start.InTheBlack;

        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Kind.Level, "Level"));
        sentence.Children.Add(Words("stories of"));
        sentence.Children.Add(Token(Kind.Compare, "Length"));
        _lengthControls = [Token(Kind.Length, "Story length")];
        sentence.Children.Add(_lengthControls[0]);
        sentence.Children.Add(Words("set"));
        sentence.Children.Add(Token(Kind.Black, "Setting"));
        _ratingControls = [Words("with"), Token(Kind.Rating, "Rating"), Words("sorted by"), Token(Kind.Sort, "Sort")];

        foreach (var control in _ratingControls)
        {
            sentence.Children.Add(control);
        }

        Assemble(sentence, null);

        Refresh();
    }

    public StoryFilter Filter => new(
        StoryCard.Levels.Where(_levels.Contains).ToList() is { Count: > 0 } levels ? levels : null,
        _compare,
        _length,
        _minStars,
        _sort,
        _black);

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
        ShowCount(shown, total, "stories");
        ClearShown = filtered;
    }

    protected override void Clear()
    {
        _levels.Clear();
        _compare = StoryLengthCompare.Any;
        _minStars = null;
        _sort = StorySort.Catalogue;
        _black = false;
        Open = null;
        Refresh();
        RaiseChanged();
    }

    private int Selected(Kind kind) => kind switch
    {
        Kind.Compare => (int)_compare,
        Kind.Black => _black ? 1 : 0,
        Kind.Length => StoryPacing.All.ToList().FindIndex(pacing => pacing.Key == _length),
        Kind.Rating => _minStars is { } stars ? Math.Clamp(5 - stars, 0, RatingLabels.Length - 1) : 0,
        _ => (int)_sort,
    };

    protected override void Update()
    {
        _lengthControls[0].IsVisible = _compare != StoryLengthCompare.Any;

        foreach (var control in _ratingControls)
        {
            control.IsVisible = _ratingsShown;
        }

        if ((!_ratingsShown && Open is Kind.Rating or Kind.Sort) || (Open == Kind.Length && _compare == StoryLengthCompare.Any))
        {
            Open = null;
        }

        Values[Kind.Level].Text = LevelText().ToUpperInvariant();
        Values[Kind.Compare].Text = CompareLabels[(int)_compare].ToUpperInvariant();
        Values[Kind.Length].Text = (StoryPacing.Find(_length)?.Name ?? _length).ToUpperInvariant();
        Values[Kind.Black].Text = BlackLabels[Selected(Kind.Black)].ToUpperInvariant();
        Values[Kind.Rating].Text = RatingLabels[Selected(Kind.Rating)].ToUpperInvariant();
        Values[Kind.Sort].Text = SortLabels[(int)_sort].ToUpperInvariant();

        foreach (var (kind, segment) in _segments)
        {
            segment.SelectedIndex = Selected(kind);
        }

        for (var i = 0; i < _levelBoxes.Count; i++)
        {
            _levelBoxes[i].IsChecked = _levels.Contains(StoryCard.Levels[i]);
        }
    }

    private string LevelText()
    {
        var ticked = StoryCard.Levels.Where(_levels.Contains).ToList();

        return ticked.Count == 0 || ticked.Count == StoryCard.Levels.Count
            ? "All"
            : string.Join(" and ", ticked.Select(level => LevelNames[StoryCard.Levels.ToList().IndexOf(level)]));
    }

    protected override Control PanelFor(Kind kind)
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
            Kind.Black => OptionsPanel(kind, BlackLabels, "Setting", index => _black = index == 1),
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
            if (Syncing || segment.SelectedIndex < 0)
            {
                return;
            }

            pick(segment.SelectedIndex);
            Open = null;
            Refresh();
            RaiseChanged();
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
                if (Syncing)
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
                RaiseChanged();
            };

            _levelBoxes.Add(box);
            panel.Children.Add(box);
        }

        var hint = AdventuresPage.Text(LevelHint, TypeScale.Small, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(hint);

        return panel;
    }
}
