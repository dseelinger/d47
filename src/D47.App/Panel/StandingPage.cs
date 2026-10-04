using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Standing: reputation with the four superpowers and the two navy ranks (#552).</summary>
public sealed class StandingPage : UserControl
{
    public const string RootKey = "standing";

    /// <summary>The footer's phrase; it carries a <c>get_standing</c> keyword.</summary>
    public const string Phrase = "how's my standing with the Empire";

    /// <summary>The top of a navy ladder, as <see cref="NavalRanks"/> numbers it.</summary>
    public const int NavyTop = 14;

    private static readonly (string Key, string Label)[] Powers =
    [
        ("Empire", "Empire"),
        ("Federation", "Federation"),
        ("Alliance", "Alliance"),
        ("Independent", "Independents"),
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<string?> _name;
    private readonly StackPanel _body = new();
    private ReputationState? _reputationSeen;
    private RankState? _ranksSeen;
    private string? _nameSeen;

    public StandingPage(Func<CommanderGameState?> state, Func<string?> name, JournalClock clock)
    {
        _state = state;
        _name = name;

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase, clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        var capped = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 900 }, new ColumnDefinition(GridLength.Auto)],
            Children = { _body },
        };
        root.Children.Add(LoadoutPages.Scrolling(capped));

        Content = root;
        Draw();
    }

    /// <summary>Redraws when the reputation, rank record or name has changed since the last draw.</summary>
    public bool Tick()
    {
        var state = _state();

        if (ReferenceEquals(state?.Reputation, _reputationSeen)
            && ReferenceEquals(state?.Ranks, _ranksSeen)
            && string.Equals(_name(), _nameSeen, StringComparison.Ordinal))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>A reading as the page prints it: rounded, with a true minus.</summary>
    public static string Signed(double reputation)
    {
        var rounded = (int)Math.Round(reputation, MidpointRounding.AwayFromZero);
        return rounded < 0
            ? "−" + (-rounded).ToString(CultureInfo.InvariantCulture)
            : rounded.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The meta line under a navy rank: <c>RANK N OF 14 · N% TO NEXT</c>.</summary>
    public static string NavyMeta(RankStanding standing, Func<int, string?> named)
    {
        var meta = $"RANK {standing.Rank} OF {NavyTop}";

        return standing.Rank < NavyTop && standing.Percent is { } percent && named(standing.Rank + 1) is { } next
            ? $"{meta} · {percent}% TO {next.ToUpperInvariant()}"
            : meta;
    }

    private void Draw()
    {
        var state = _state();
        _reputationSeen = state?.Reputation;
        _ranksSeen = state?.Ranks;
        _nameSeen = _name();

        _body.Children.Clear();

        var title = TitleText.Build(
            _nameSeen is { Length: > 0 } name ? $"CMDR {name}" : "Commander", TypeScale.Title, TitleRank.Screen);
        _body.Children.Add(TitleText.Block(title, TitleText.Context("Commander record")));

        _body.Children.Add(Group("Reputation", "Where you stand with each power, from −100 to 100."));

        var rows = new StackPanel { Spacing = 2 };

        foreach (var (key, label) in Powers)
        {
            rows.Children.Add(ReputationRow(label, (state?.Reputation ?? ReputationState.Empty).Reading(key)));
        }

        _body.Children.Add(rows);

        _body.Children.Add(Group("Navy ranks", "Your rank in each navy and how far you are towards the next one."));

        var ranks = state?.Ranks ?? RankState.Empty;
        var navies = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 2 };
        var imperial = NavyCell("Imperial navy", ranks.For("Empire"), NavalRanks.EmpireName);
        var federal = NavyCell("Federal navy", ranks.For("Federation"), NavalRanks.FederationName);
        Grid.SetColumn(federal, 1);
        navies.Children.Add(imperial);
        navies.Children.Add(federal);

        _body.Children.Add(navies);
    }

    private static Control Group(string title, string help)
    {
        var heading = TitleText.Build(title, TypeScale.Secondary, TitleRank.Group);
        heading.Margin = new Thickness(0, 0, 12, 0);
        heading.VerticalAlignment = VerticalAlignment.Center;

        var note = new TextBlock
        {
            Text = help,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(note, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var words = new WrapPanel { Children = { heading, note } };

        var rule = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 0) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Margin = new Thickness(0, 28, 0, 10), Children = { words, rule } };
    }

    private static Control ReputationRow(string label, FactionReading? reading)
    {
        ReputationBand? band = reading is null ? null : ReputationBands.Of(reading.MyReputation);
        var hostile = band is ReputationBand.Unfriendly or ReputationBand.Hostile;
        var negative = reading is not null && Signed(reading.MyReputation).StartsWith('−');

        var name = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Section,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Section * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var bandText = new TextBlock
        {
            Text = band?.ToString().ToUpperInvariant() ?? string.Empty,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Control * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(bandText, TextBlock.ForegroundProperty, hostile ? ThemeManager.RedKey : ThemeManager.AKey);
        Grid.SetColumn(bandText, 2);

        var number = new TextBlock
        {
            Text = reading is null ? "—" : Signed(reading.MyReputation),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(number, TextBlock.ForegroundProperty, negative ? ThemeManager.RedKey : ThemeManager.WhiteKey);
        Grid.SetColumn(number, 4);

        var gauge = Gauge.CentreTrack(reading is null ? null : reading.MyReputation / 100);
        gauge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(gauge, 6);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,16,120,16,56,16,*"),
            Children = { name, bandText, number, gauge },
        };

        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            Child = grid,
        };
        Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static Control NavyCell(string label, RankStanding? standing, Func<int, string?> named)
    {
        var caption = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
        };
        Themed(caption, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var rank = new TextBlock
        {
            Text = standing is null ? "—" : named(standing.Rank) ?? $"Rank {standing.Rank}",
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Section,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Themed(rank, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var meta = new TextBlock
        {
            Text = standing is null ? string.Empty : NavyMeta(standing, named),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(meta, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var line = new DockPanel();
        DockPanel.SetDock(rank, Dock.Left);
        line.Children.Add(rank);
        line.Children.Add(meta);

        var fill = standing is null ? 0
            : standing.Rank >= NavyTop ? 1
            : (standing.Percent ?? 0) / 100.0;

        var cell = new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 6,
                Children = { caption, line, Gauge.Track(fill, Gauge.FillKey(GaugeFill.Progress)) },
            },
        };
        Themed(cell, Border.BackgroundProperty, ThemeManager.SlabKey);

        return cell;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
