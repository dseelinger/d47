using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Viewing;

namespace D47.App.Panel;

/// <summary>
/// Media › Movies › Catalogue's filter: the sentence "Show [kind] from [sources] sorted by [order]" and the results
/// line with Clear filters.
/// </summary>
internal sealed class VideoFilterBar : TokenFilterBar<VideoFilterBar.Part>
{
    public const string SourcesHint = "Tick any. With none ticked, every source is searched.";

    public const string ClearName = "VideoClear";

    public const string SourceName = "VideoSource";

    internal enum Part
    {
        Kind,
        Sources,
        Order,
    }

    public static readonly IReadOnlyList<string> KindLabels = ["Films and footage", "Feature films", "Short films", "Footage", "Other"];

    public static readonly IReadOnlyList<string> OrderLabels = ["Most watched", "Relevance", "Newest", "Title"];

    private readonly WatchHost _host;
    private readonly List<CheckBox> _boxes = [];
    private Segment? _kindSegment;
    private Segment? _orderSegment;
    private StackPanel? _sourcePanel;
    private VideoKind? _kind;
    private VideoOrder _order = VideoOrder.MostWatched;

    public VideoFilterBar(WatchHost host)
        : base("VideoToken.")
    {
        _host = host;

        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Part.Kind, "Kind"));
        sentence.Children.Add(Words("from"));
        sentence.Children.Add(Token(Part.Sources, "Sources"));
        sentence.Children.Add(Words("sorted by"));
        sentence.Children.Add(Token(Part.Order, "Order"));

        Assemble(sentence, ClearName);
        Refresh();
    }

    /// <summary>The filters the sentence describes.</summary>
    public VideoFilters Filters => new(_kind, _order);

    private bool IsDefault => _kind is null && _order == VideoOrder.MostWatched && _host.TickedSources.Count == 0;

    private int KindIndex => _kind is { } kind ? (int)kind + 1 : 0;

    /// <summary>Draws "N videos match “query”" and the token values.</summary>
    public void Show(int total, string query)
    {
        ShowLine(
            Fonts.Mono(total.ToString("N0", CultureInfo.InvariantCulture)),
            new Run(total == 1 ? " video" : " videos"),
            new Run(query.Length == 0 ? string.Empty : $" match “{query}”"));
        Refresh();
    }

    /// <summary>Empties the results line, for a search that has not answered.</summary>
    public void ShowNone()
    {
        ShowLine(new Run(string.Empty));
        Refresh();
    }

    protected override void Clear()
    {
        _kind = null;
        _order = VideoOrder.MostWatched;
        _host.SetSources([]);
        Open = null;
        Refresh();
        RaiseChanged();
    }

    protected override void Update()
    {
        var ticked = _host.TickedSources;
        var searched = _host.SearchedSources;

        Values[Part.Kind].Text = KindLabels[KindIndex].ToUpperInvariant();
        Values[Part.Order].Text = OrderLabels[(int)_order].ToUpperInvariant();
        Values[Part.Sources].Text = (searched.Count == 1 ? VideoSources.NameOf(searched[0]) : $"{searched.Count} sources").ToUpperInvariant();

        if (_kindSegment is not null)
        {
            _kindSegment.SelectedIndex = KindIndex;
        }

        if (_orderSegment is not null)
        {
            _orderSegment.SelectedIndex = (int)_order;
        }

        for (var i = 0; i < _boxes.Count; i++)
        {
            _boxes[i].IsChecked = ticked.Contains(VideoSources.All[i], StringComparer.Ordinal);
        }

        ClearShown = !IsDefault;
    }

    protected override Control PanelFor(Part part) => part switch
    {
        Part.Kind => _kindSegment ??= KindPanel(),
        Part.Sources => _sourcePanel ??= SourcePanel(),
        _ => _orderSegment ??= OrderPanel(),
    };

    private Segment KindPanel()
    {
        var segment = new Segment { ItemsSource = KindLabels, SelectedIndex = KindIndex };
        AutomationProperties.SetName(segment, "Kind options");

        segment.SelectionChanged += (_, _) =>
        {
            if (Syncing || segment.SelectedIndex < 0)
            {
                return;
            }

            _kind = segment.SelectedIndex == 0 ? null : (VideoKind)(segment.SelectedIndex - 1);
            Open = null;
            Refresh();
            RaiseChanged();
        };

        return segment;
    }

    private Segment OrderPanel()
    {
        var segment = new Segment { ItemsSource = OrderLabels, SelectedIndex = (int)_order };
        AutomationProperties.SetName(segment, "Order options");

        segment.SelectionChanged += (_, _) =>
        {
            if (Syncing || segment.SelectedIndex < 0)
            {
                return;
            }

            _order = (VideoOrder)segment.SelectedIndex;
            Open = null;
            Refresh();
            RaiseChanged();
        };

        return segment;
    }

    private StackPanel SourcePanel()
    {
        var grid = new TileGrid { Columns = 2 };

        foreach (var source in VideoSources.All)
        {
            var box = LabeledCheckBox.Caps(VideoSources.NameOf(source));

            box.Name = SourceName;
            box.Tag = source;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.IsChecked = _host.TickedSources.Contains(source, StringComparer.Ordinal);
            box.IsCheckedChanged += (_, _) =>
            {
                if (Syncing)
                {
                    return;
                }

                var ticked = _host.TickedSources.Where(id => id != source).ToList();

                if (box.IsChecked == true)
                {
                    ticked.Add(source);
                }

                _host.SetSources(ticked);
                Refresh();
                RaiseChanged();
            };

            _boxes.Add(box);
            grid.Children.Add(box);
        }

        var hint = AdventuresPage.Text(SourcesHint, TypeScale.Small, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 6, 0, 0);

        return new StackPanel { Spacing = Gaps.Tile, Children = { grid, hint } };
    }
}
