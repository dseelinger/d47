using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Statistics: every section of the <c>Statistics</c> event, one at a time, with a search across them (#553).</summary>
public sealed class StatisticsPage : UserControl, IFilterablePage
{
    public const string RootKey = "statistics";

    /// <summary>The footer's phrase; it carries a <c>get_commander_statistics</c> keyword.</summary>
    public const string Phrase = "what are my career statistics";

    public const string NoMatch = "No figure in any section matches that.";

    public const string Hint = "Career totals from the game's own record. They change when the game next writes them.";

    private readonly Func<CommanderGameState?> _state;
    private readonly Action _clearSearch;
    private readonly ContentControl _sidebar = new();
    private readonly StackPanel _body = new();
    private CareerStatistics? _seen;
    private string? _section;
    private string _query = string.Empty;

    public StatisticsPage(Func<CommanderGameState?> state, JournalClock clock, Action clearSearch)
    {
        _state = state;
        _clearSearch = clearSearch;

        Content = PageFooter.Shell(_sidebar, _body, Phrase, clock, 900);
        Draw();
    }

    public bool Filters => (_state()?.Statistics ?? CareerStatistics.Empty).IsKnown;

    public string FilterPlaceholder => "Find a figure in any section";

    public void Filter(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (string.Equals(trimmed, _query, StringComparison.Ordinal))
        {
            return;
        }

        _query = trimmed;
        DrawBody();
    }

    /// <summary>The section showing, by its journal name.</summary>
    public string? Section => _section;

    /// <summary>Redraws when the game has written a new <c>Statistics</c> event since the last draw.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.Statistics, _seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>Shows one section, by its journal name, and drops the search.</summary>
    public void Open(string section)
    {
        _section = section;

        if (_sidebar.Content is Sidebar sidebar)
        {
            sidebar.Selected = section;
        }

        if (_query.Length > 0)
        {
            _query = string.Empty;
            _clearSearch();
        }

        DrawBody();
    }

    private CareerStatistics Statistics => _seen ?? CareerStatistics.Empty;

    private void Draw()
    {
        _seen = _state()?.Statistics;

        var sections = Statistics.Sections;

        if (_section is null || !sections.Contains(_section, StringComparer.Ordinal))
        {
            _section = sections.Count > 0 ? sections[0] : null;
        }

        _sidebar.Content = sections.Count == 0
            ? null
            : new Sidebar(
                [new SidebarGroup(null, [.. sections.Select(name => new SidebarItem(
                    name,
                    CareerStatistics.Label(name),
                    Statistics.Section(name).Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))])],
                _section,
                Open,
                "The game's sections, in the order it writes them.");

        DrawBody();
    }

    private void DrawBody()
    {
        _body.Children.Clear();

        if (!Statistics.IsKnown)
        {
            _body.Children.Add(TitleText.Block(
                TitleText.Build("Career statistics", TypeScale.Title, TitleRank.Screen)));
            _body.Children.Add(CreditsKit.Note("The game has not reported your career statistics yet. It writes them when you log in.", 18, full: true));
            return;
        }

        if (_query.Length > 0)
        {
            DrawMatches();
            return;
        }

        if (_section is not { } section)
        {
            return;
        }

        _body.Children.Add(TitleText.Block(
            TitleText.Build(CareerStatistics.Label(section), TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Career statistics ›")));

        var tiles = Statistics.Section(section)
            .Select(figure => (Control)StatTile.Build(
                CareerStatistics.Label(figure.Key),
                CareerStatistics.Format(figure.Key, figure.Value).ToUpperInvariant(),
                StatInk.Number))
            .ToList();

        var grid = StatTile.Grid(tiles, maxColumns: 3);
        grid.Margin = new Thickness(0, 18, 0, 0);
        _body.Children.Add(grid);
        _body.Children.Add(CreditsKit.Note(Hint, 18, full: true));
    }

    private void DrawMatches()
    {
        _body.Children.Add(TitleText.Block(
            TitleText.Build($"Figures matching “{_query}”", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Career statistics ›")));

        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 18, 0, 0) };

        foreach (var section in Statistics.Sections)
        {
            var sectionLabel = CareerStatistics.Label(section);
            var sectionMatches = sectionLabel.Contains(_query, StringComparison.OrdinalIgnoreCase);

            foreach (var (key, value) in Statistics.Section(section))
            {
                var label = CareerStatistics.Label(key);

                if (sectionMatches || label.Contains(_query, StringComparison.OrdinalIgnoreCase))
                {
                    rows.Children.Add(MatchRow(section, sectionLabel, label, CareerStatistics.Format(key, value)));
                }
            }
        }

        _body.Children.Add(rows.Children.Count > 0 ? rows : CreditsKit.Note(NoMatch, 18, full: true));
    }

    private Border MatchRow(string section, string sectionLabel, string label, string value)
    {
        var where = new TextBlock
        {
            Text = sectionLabel.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(where, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var name = new TextBlock
        {
            Text = label,
            FontSize = TypeScale.Control,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        Grid.SetColumn(name, 2);

        var figure = new TextBlock
        {
            Text = value.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Control,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);
        Grid.SetColumn(figure, 4);

        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("180,12,*,12,Auto"),
                Children = { where, name, figure },
            },
        };
        MaterialsPage.Hover(row, ThemeManager.SlabKey);
        ListRow.Opens(row, () => Open(section));

        return row;
    }

}
