using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A state filter the NEARBY POWERPLAY SYSTEMS list offers.</summary>
public sealed record PowerplayStateFilter(string Label, string? State)
{
    public static readonly IReadOnlyList<PowerplayStateFilter> All =
    [
        new("All", null),
        new("Exploited", "Exploited"),
        new("Fortified", "Fortified"),
        new("Stronghold", "Stronghold"),
    ];

    public bool Holds(PowerplayNeighbour system) =>
        State is null || string.Equals(system.State, State, StringComparison.OrdinalIgnoreCase);
}

public sealed partial class StarSystemPage
{
    public const string NoPowerplay = "This system has no Powerplay presence.";

    public const string NoNeighbours = "No Powerplay system is listed here.";

    private const string NeighbourColumns = "*,110,120,*,170";

    private enum Nearby
    {
        None,
        Asking,
        Ready,
        Failed,
    }

    private Nearby _nearby;
    private PowerplayNeighbourhood? _neighbours;
    private string? _neighbourFailure;
    private double _reach;
    private int _stateFilter;
    private CancellationTokenSource? _nearbyRequest;
    private Border? _neighbourList;

    /// <summary>"6.12 ly", two decimals.</summary>
    public static string DistanceText(double lightYears) =>
        lightYears.ToString("0.00", CultureInfo.InvariantCulture) + " ly";

    /// <summary>"53.6%", one decimal.</summary>
    public static string ProgressText(double? progress) => InfluenceText(progress);

    private static string Number(long? value) =>
        value is { } figure ? figure.ToString("N0", CultureInfo.InvariantCulture) : Dash;

    private void ResetNearby()
    {
        _nearbyRequest?.Cancel();
        _nearbyRequest?.Dispose();
        _nearbyRequest = null;
        _nearby = Nearby.None;
        _neighbours = null;
        _neighbourFailure = null;
        _stateFilter = 0;
    }

    /// <summary>Asks for the neighbours when none are held or in flight for this system.</summary>
    private void AskNearby(StarSystemProfile profile)
    {
        if (_nearby is Nearby.Asking or Nearby.Ready)
        {
            return;
        }

        _reach = PowerplayNeighbourhood.PowerplayReach(profile.Powerplay?.State);
        _nearby = Nearby.Asking;
        _neighbourFailure = null;

        var request = _nearbyRequest = new CancellationTokenSource();
        _ = FetchNearby(profile.Name, _reach, request);
    }

    private async Task FetchNearby(string system, double reach, CancellationTokenSource request)
    {
        var token = request.Token;
        PowerplayNeighbourhood? found = null;
        string? failure = null;

        try
        {
            found = await Task.Run(() => _surface.Systems.PowerplayNearAsync(system, reach, token), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (GalaxyUnavailableException ex)
        {
            failure = ex.Message;
        }
        catch (Exception)
        {
            failure = Unreadable;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(request, _nearbyRequest))
            {
                return;
            }

            _nearbyRequest = null;
            request.Dispose();
            _neighbours = found;
            _neighbourFailure = failure;
            _nearby = failure is null ? Nearby.Ready : Nearby.Failed;

            if (_section == Section.Powerplay && _view == View.Ready)
            {
                Draw();
            }
        });
    }

    private StackPanel Powerplay(StarSystemProfile profile)
    {
        var page = new StackPanel { Spacing = 24 };

        page.Children.Add(profile.Powerplay is { } standing
            ? PowerplayFigures(standing)
            : Sentence(NoPowerplay, ThemeManager.WhiteKey));
        page.Children.Add(NearbyGroup());

        return page;
    }

    private static Control PowerplayFigures(PowerplayStanding standing)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = Gaps.Tile,
            RowSpacing = Gaps.Tile,
        };

        Place(grid, 0, 0, 1, StatTile.Build("Controlling power", standing.ControllingPower ?? Dash, StatInk.Name));
        Place(grid, 0, 1, 1, StatTile.Build("State", standing.State ?? Dash));
        Place(grid, 0, 2, 1, Progress(standing.ControlProgress));
        Place(grid, 1, 0, 1, StatTile.Build("Reinforcement", Number(standing.Reinforcement), StatInk.Number));
        Place(grid, 1, 1, 1, StatTile.Build("Undermining", Number(standing.Undermining), StatInk.Number));
        Place(grid, 1, 2, 1, StatTile.Build(
            "Powers present",
            standing.Powers.Count > 0 ? string.Join(" · ", standing.Powers) : Dash,
            StatInk.Name));

        return grid;
    }

    private static Border Progress(double? progress)
    {
        var figure = new TextBlock
        {
            Text = ProgressText(progress),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = StatTile.ValueSize,
            FontWeight = FontWeight.Medium,
        };
        RoutingKit.Themed(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var track = Gauge.Track(progress ?? 0, ThemeManager.AKey);
        track.Margin = new Thickness(0, 4, 0, 0);

        var tile = new Border
        {
            Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 2, Children = { Caption("Control progress", ThemeManager.GreyKey), figure, track } },
        };
        RoutingKit.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    private StackPanel NearbyGroup()
    {
        var total = _neighbours?.Total;
        var head = GroupHead(
            "Nearby Powerplay systems",
            $"Within {_reach.ToString("0", CultureInfo.InvariantCulture)} ly, nearest first. Choose one to open it here.",
            total?.ToString(CultureInfo.InvariantCulture));
        var group = new StackPanel { Spacing = 10, Children = { head } };

        switch (_nearby)
        {
            case Nearby.Failed:
                group.Children.Add(new Notice
                {
                    Label = "Spansh didn't answer",
                    Text = _neighbourFailure,
                    Detail = D47.Knowledge.SpanshStarSystemService.Host,
                    MaxWidth = 760,
                    HorizontalAlignment = HorizontalAlignment.Left,
                });
                break;

            case Nearby.Ready when _neighbours is { } near:
                group.Children.Add(StateSwitch(near));
                _neighbourList = new Border { Child = NeighbourTable(near) };
                group.Children.Add(_neighbourList);
                break;

            default:
                group.Children.Add(Sentence(
                    $"Asking Spansh for Powerplay systems near {_opened?.Name}.", ThemeManager.WhiteKey));
                break;
        }

        return group;
    }

    private Segment StateSwitch(PowerplayNeighbourhood near)
    {
        var filters = PowerplayStateFilter.All;

        var segment = new Segment
        {
            ItemsSource =
            [
                .. filters.Select(filter =>
                    $"{filter.Label} {near.Systems.Count(filter.Holds).ToString(CultureInfo.InvariantCulture)}"),
            ],
            SelectedIndex = _stateFilter,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Avalonia.Automation.AutomationProperties.SetName(segment, "Powerplay state");
        segment.SelectionChanged += (_, _) =>
        {
            _stateFilter = segment.SelectedIndex;

            if (_neighbourList is not null)
            {
                _neighbourList.Child = NeighbourTable(near);
                _redrawn = true;
            }
        };

        return segment;
    }

    private StackPanel NeighbourTable(PowerplayNeighbourhood near)
    {
        var shown = near.Systems.Where(PowerplayStateFilter.All[_stateFilter].Holds).ToList();
        var table = new StackPanel { Spacing = Gaps.Tile };

        var header = NeighbourGrid();
        header.Height = 28;
        header.Margin = new Thickness(12, 0);
        string[] titles = ["System", "Distance", "State", "Controlling power", "Control progress"];

        for (var i = 0; i < titles.Length; i++)
        {
            var title = Caption(titles[i], ThemeManager.GreyKey);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(title, i);
            header.Children.Add(title);
        }

        table.Children.Add(header);

        foreach (var system in shown)
        {
            table.Children.Add(NeighbourRow(system));
        }

        if (shown.Count == 0)
        {
            var none = Sentence(NoNeighbours, ThemeManager.WhiteKey);
            none.Margin = new Thickness(0, 10, 0, 0);
            table.Children.Add(none);
        }

        var showing = Caption(
            $"Showing {shown.Count} of {near.Total}. At most {PowerplayNeighbourhood.Limit} are listed.", ThemeManager.GreyKey);
        showing.Margin = new Thickness(0, 8, 0, 0);
        Avalonia.Automation.AutomationProperties.SetName(showing, $"Showing {shown.Count} of {near.Total}");
        table.Children.Add(showing);

        return table;
    }

    private static Grid NeighbourGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(NeighbourColumns),
        ColumnSpacing = 12,
    };

    private Border NeighbourRow(PowerplayNeighbour system)
    {
        var grid = NeighbourGrid();
        grid.VerticalAlignment = VerticalAlignment.Center;

        Cell(grid, 0, Ellipsed(system.Name, TypeScale.ControlLarge, FontWeight.SemiBold, ThemeManager.AKey));
        Cell(grid, 1, Mono(DistanceText(system.Distance), ThemeManager.AKey));
        Cell(grid, 2, Ellipsed(system.State ?? Dash, TypeScale.Control, FontWeight.Medium, ThemeManager.AKey));
        Cell(grid, 3, Ellipsed(system.ControllingPower ?? Dash, TypeScale.Control, FontWeight.Medium, ThemeManager.WhiteKey));
        Cell(grid, 4, Influence(system.ControlProgress));

        var row = new Border
        {
            MinHeight = 40,
            Padding = new Thickness(12, 0),
            Child = grid,
        };
        MaterialsPage.Hover(row);
        MaterialsPage.Pressable(row, () => Look(system.Name));
        Avalonia.Automation.AutomationProperties.SetName(row, system.Name);

        return row;
    }
}
