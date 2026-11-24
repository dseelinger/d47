using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>How the search for the nearest material trader of one kind ended.</summary>
public enum TraderFind
{
    Found,

    /// <summary>The search failed, was refused or ran out of time.</summary>
    NoAnswer,

    /// <summary>No trader of the kind within <see cref="MaterialsPage.TraderReach"/> light years.</summary>
    NoneWithin,

    /// <summary>Every trader the search returned is in a system shut to the Commander.</summary>
    NoneOpen,
}

/// <summary>The nearest trader of one kind: its system and distance when found.</summary>
public sealed record TraderAnswer(TraderFind Find, string? System = null, double? Distance = null);

/// <summary>The nearest material trader of each kind, from the galaxy search, in the title line or under the title rule.</summary>
public sealed partial class MaterialsPage
{
    /// <summary>The figure slot's label over one kind's trader.</summary>
    public const string NearestTrader = "Nearest trader";

    public const string SearchingWord = "SEARCHING";

    public const string NoAnswerWord = "NO ANSWER";

    public const string NoneWithinWord = "NONE WITHIN 500 LY";

    public const string NoneOpenWord = "NONE YOU CAN ENTER";

    /// <summary>How far out, in light years, a trader is looked for.</summary>
    public const int TraderReach = 500;

    /// <summary>How many traders one search fetches, nearest first.</summary>
    public const int TraderFetch = 25;

    /// <summary>How long a trader search may run.</summary>
    private static readonly TimeSpan TraderBudget = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, TraderSearch> _traders =
        StationQuery.TraderTypes.ToDictionary(kind => kind, kind => new TraderSearch(kind), StringComparer.OrdinalIgnoreCase);

    /// <summary>One kind's search: the system it ran from, the run in flight, its answer and where it is drawn now.</summary>
    private sealed class TraderSearch(string kind)
    {
        public string Kind { get; } = kind;

        public string? From { get; set; }

        public CancellationTokenSource? Running { get; set; }

        public TraderAnswer? Answer { get; set; }

        public WaitingState? Mark { get; set; }

        public ContentControl? Host { get; set; }

        public bool InSlot { get; set; }
    }

    /// <summary>The accessible name of NONE WITHIN 500 LY: "No raw material trader within 500 light years."</summary>
    public static string NoneWithinSentence(string kind) =>
        $"No {kind.ToLowerInvariant()} material trader within {Count(TraderReach)} light years.";

    /// <summary>The accessible name of NONE YOU CAN ENTER.</summary>
    public static string NoneOpenSentence(string kind) =>
        $"None of the {Count(TraderFetch)} nearest {kind.ToLowerInvariant()} material traders is in a system you can enter.";

    /// <summary>The hint in place of every trader when there is no galaxy search.</summary>
    public static string NoTraderSearch(bool all) =>
        $"The galaxy search is not available, so D47 cannot look for the nearest {(all ? "traders" : "trader")}.";

    /// <summary>The hint in place of every trader when the current system is not known.</summary>
    public static string NoTraderPosition(bool all) =>
        $"Your position is not known yet, so D47 cannot look for the nearest {(all ? "traders" : "trader")}.";

    /// <summary>
    /// The trader kinds a view shows: its own on a trader ledger, all three on Needed by plans and on Farming route
    /// with no filter, the filtered kind on a filtered Farming route, none elsewhere.
    /// </summary>
    public static IReadOnlyList<string> TraderKinds(string view, int farmingFilter)
    {
        if (view == NeededView || (view == FarmingView && farmingFilter <= 0))
        {
            return StationQuery.TraderTypes;
        }

        var kind = view == FarmingView
            ? FarmingFilters[Math.Min(farmingFilter, FarmingFilters.Count - 1)]
            : StationQuery.TraderTypes.FirstOrDefault(type => string.Equals(type, view, StringComparison.OrdinalIgnoreCase));

        return kind is null ? [] : [kind];
    }

    /// <summary>
    /// The nearest trader of <paramref name="kind"/> whose system is open to the Commander: a permit system only at its
    /// navy rank or once <paramref name="visited"/> says it was entered, a starter system never.
    /// </summary>
    public static async Task<TraderAnswer> NearestTraderAsync(
        IGalaxyService galaxy, string here, string kind, RankState ranks, Func<long, VisitState> visited, CancellationToken cancellationToken)
    {
        var result = await galaxy
            .FindStationsAsync(StationQuery.ForTrader(here, kind, TraderReach, TraderFetch), cancellationToken)
            .ConfigureAwait(false);

        if (result.Refusal is not null)
        {
            return new TraderAnswer(TraderFind.NoAnswer);
        }

        if (result.Stations.Count == 0)
        {
            return new TraderAnswer(TraderFind.NoneWithin);
        }

        foreach (var station in result.Stations.OrderBy(station => station.Distance ?? double.MaxValue))
        {
            if (!PermitSystemTable.ClosedTo(
                    station.SystemName, ranks, () => station.SystemAddress is { } address && visited(address) == VisitState.Visited))
            {
                return new TraderAnswer(TraderFind.Found, station.SystemName, station.Distance);
            }
        }

        return new TraderAnswer(TraderFind.NoneOpen);
    }

    /// <summary>
    /// Starts a search for each of <paramref name="kinds"/> not already answered or running from the current system,
    /// and cancels every running search for a kind not in it, or every one when nothing can be searched.
    /// </summary>
    private void SeekTraders(IReadOnlyList<string> kinds)
    {
        var galaxy = _gap.Galaxy();
        var state = _gap.State();
        var here = state?.Location.StarSystem;

        foreach (var search in _traders.Values)
        {
            if (galaxy is null || state is null || here is not { Length: > 0 } || !kinds.Contains(search.Kind, StringComparer.OrdinalIgnoreCase))
            {
                StopTrader(search);
                continue;
            }

            if (string.Equals(search.From, here, StringComparison.Ordinal) && (search.Answer is not null || search.Running is not null))
            {
                continue;
            }

            StartTrader(search, galaxy, state, here);
        }
    }

    private void StartTrader(TraderSearch search, IGalaxyService galaxy, CommanderGameState state, string here)
    {
        StopTrader(search);

        var running = search.Running = new CancellationTokenSource(TraderBudget);
        var mark = search.Mark = SearchingMark();

        search.From = here;
        search.Answer = null;

        var ranks = state.Ranks;
        var commander = state.Identity.FrontierId;
        var gap = _gap;

        _ = TraderAsync(search, running, mark, galaxy, here, ranks, address => gap.Visit(commander, address));
    }

    private async Task TraderAsync(
        TraderSearch search, CancellationTokenSource running, WaitingState mark, IGalaxyService galaxy, string here, RankState ranks, Func<long, VisitState> visited)
    {
        TraderAnswer? found = null;

        try
        {
            await Waiting.While(
                mark,
                null,
                () => Task.Run(async () =>
                    found = await NearestTraderAsync(galaxy, here, search.Kind, ranks, visited, running.Token).ConfigureAwait(false)))
                .ConfigureAwait(true);
        }
        catch (Exception)
        {
            found = null;
        }

        running.Dispose();

        if (!ReferenceEquals(running, search.Running))
        {
            return;
        }

        search.Running = null;
        search.Answer = found ?? new TraderAnswer(TraderFind.NoAnswer);

        if (search.Host is { } host)
        {
            host.Content = TraderEntry(search);
        }
    }

    /// <summary>Cancels a running search; an answer already given stays for the system it was searched from.</summary>
    private static void StopTrader(TraderSearch search)
    {
        if (search.Running is not { } running)
        {
            return;
        }

        search.Running = null;
        search.From = null;
        running.Cancel();
    }

    private static WaitingState SearchingMark()
    {
        var mark = new WaitingState(WaitForm.Inline) { IsVisible = false };
        mark.Children.Add(TraderWord(SearchingWord, ThemeManager.GreyKey));

        return mark;
    }

    /// <summary>
    /// What the title block and the head line carry for <paramref name="kinds"/>: one kind in the figure slot, three in
    /// a line under the title rule, or the sentence that stops every search.
    /// </summary>
    private (Control? Figure, Control? Line, string? Stopped) Traders(IReadOnlyList<string> kinds)
    {
        foreach (var search in _traders.Values)
        {
            search.Host = null;
        }

        if (kinds.Count == 0)
        {
            return (null, null, null);
        }

        var all = kinds.Count > 1;

        if (_gap.Galaxy() is null)
        {
            return (null, null, NoTraderSearch(all));
        }

        if (_gap.State()?.Location.StarSystem is not { Length: > 0 })
        {
            return (null, null, NoTraderPosition(all));
        }

        return all ? (null, TraderLine(kinds), null) : (TraderSlot(kinds[0]), null, null);
    }

    /// <summary>A title block with <paramref name="figure"/>, which takes at most half the title line.</summary>
    private static Control TitleWith(TextBlock title, TextBlock context, Control? figure)
    {
        var block = TitleText.Block(title, context, figure);

        if (figure is not null)
        {
            block.SizeChanged += (_, e) => figure.MaxWidth = Math.Max(0, e.NewSize.Width / 2);
        }

        return block;
    }

    /// <summary>NEAREST TRADER over one kind's system, distance and copy glyph, aligned right.</summary>
    private Control TraderSlot(string kind)
    {
        var search = _traders[kind];
        var host = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right };

        search.Host = host;
        search.InSlot = true;
        host.Content = TraderEntry(search);

        var label = TraderLabel(NearestTrader);
        label.HorizontalAlignment = HorizontalAlignment.Right;

        var slot = new StackPanel { Children = { label, host } };
        AutomationProperties.SetName(slot, NearestTrader);

        return slot;
    }

    /// <summary>The three kinds in three columns under the title rule, over a Line2 rule.</summary>
    private Border TraderLine(IReadOnlyList<string> kinds)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(',', kinds.Select(_ => "*"))), ColumnSpacing = 16 };

        for (var i = 0; i < kinds.Count; i++)
        {
            var search = _traders[kinds[i]];
            var host = new ContentControl();

            search.Host = host;
            search.InSlot = false;
            host.Content = TraderEntry(search);

            var column = new StackPanel { Children = { TraderLabel($"{kinds[i]} trader"), host } };
            Grid.SetColumn(column, i);
            grid.Children.Add(column);
        }

        var line = new Border
        {
            Padding = new Thickness(0, 8, 0, 10),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        };
        ThemeInk.Bind(line, Border.BorderBrushProperty, ThemeManager.Line2Key);
        AutomationProperties.SetName(line, "Nearest traders");

        return line;
    }

    /// <summary>One kind's system and distance, or the word for its state with the sentence as its accessible name.</summary>
    private Control TraderEntry(TraderSearch search)
    {
        if (search.Answer is not { } answer)
        {
            var mark = search.Mark ??= SearchingMark();
            (mark.Parent as Decorator)?.Child = null;

            var waiting = new Border { MinHeight = TypeScale.MinimumTarget, Child = mark };
            mark.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(waiting, MaterialDetailPage.Searching);

            return waiting;
        }

        return answer.Find switch
        {
            TraderFind.Found when answer.System is { } system => TraderFound(search.InSlot, system, answer.Distance),
            TraderFind.NoneWithin => TraderState(NoneWithinWord, ThemeManager.GreyKey, NoneWithinSentence(search.Kind)),
            TraderFind.NoneOpen => TraderState(NoneOpenWord, ThemeManager.GreyKey, NoneOpenSentence(search.Kind)),
            _ => TraderState(NoAnswerWord, ThemeManager.RedKey, MaterialDetailPage.Unanswered),
        };
    }

    /// <summary>
    /// The system as a link with its copy glyph, and the distance or <c>HERE</c>: on one line in the slot, the distance
    /// under the name in the line.
    /// </summary>
    private Control TraderFound(bool inSlot, string system, double? distance)
    {
        var current = string.Equals(system, _gap.State()?.Location.StarSystem, StringComparison.OrdinalIgnoreCase);
        var ink = current ? ThemeManager.CyanKey : ThemeManager.AKey;

        var name = Typeset.Chrome(
            system.ToUpperInvariant(), TypeScale.ControlLarge, ink, weight: FontWeight.SemiBold, tracking: 0, wrap: true,
            vertical: VerticalAlignment.Center);
        var link = SystemLink.Of(name, system, keepFont: true, flush: !inSlot);

        var reading = Typeset.Mono(
            current ? Here : distance is { } ly ? ly.ToString("N1", CultureInfo.InvariantCulture) + " LY" : "—",
            TypeScale.ControlLarge, ink, vertical: VerticalAlignment.Center);

        var parts = new List<Control> { link };

        if (inSlot)
        {
            parts.Add(reading);
        }

        if (_copy is { } copy)
        {
            var glyph = CopyGlyph.For(system, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            parts.Add(glyph);
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(',', parts.Select((_, i) => i == 0 ? "*" : "Auto"))),
            ColumnSpacing = 6,
            HorizontalAlignment = inSlot ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        for (var i = 0; i < parts.Count; i++)
        {
            Grid.SetColumn(parts[i], i);
            row.Children.Add(parts[i]);
        }

        if (inSlot)
        {
            return row;
        }

        reading.HorizontalAlignment = HorizontalAlignment.Left;

        return new StackPanel { Children = { row, reading } };
    }

    private static Border TraderState(string word, string key, string sentence)
    {
        var state = new Border { MinHeight = TypeScale.MinimumTarget, Child = TraderWord(word, key) };
        AutomationProperties.SetName(state, sentence);

        return state;
    }

    /// <summary>A trader state's word: chrome 13px 600, upper case and tracked.</summary>
    private static TextBlock TraderWord(string word, string key) =>
        Typeset.Chrome(
            word, TypeScale.Control, key, weight: FontWeight.SemiBold, tracking: TypeScale.Control * Fonts.ChromeTracking,
            vertical: VerticalAlignment.Center);

    /// <summary>A trader's label: chrome 12px 500 Grey, upper case and tracked.</summary>
    private static TextBlock TraderLabel(string text) =>
        Typeset.Chrome(
            text.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey, weight: FontWeight.Medium,
            tracking: TypeScale.Meta * Fonts.ChromeTracking);
}
