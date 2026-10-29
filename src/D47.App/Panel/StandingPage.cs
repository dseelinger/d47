using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Standing: reputation with the four superpowers and the two navy ranks (#552).</summary>
public sealed class StandingPage : UserControl
{
    public const string RootKey = "standing";

    /// <summary>The footer's phrase; it carries a <c>get_standing</c> keyword.</summary>
    public const string Phrase = "how's my standing with the Empire";

    /// <summary>The widest the Commander's picture is drawn.</summary>
    public const int PictureSize = 104;

    /// <summary>The top of a navy ladder, as <see cref="NavalRanks"/> numbers it.</summary>
    public const int NavyTop = 14;

    private const double UnlockRowHeight = 36;

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
    private string? _pictureSeen;
    private readonly SpeakerPictures? _pictures;
    private readonly Func<string?>? _picture;

    public StandingPage(
        Func<CommanderGameState?> state,
        Func<string?> name,
        JournalClock clock,
        SpeakerPictures? pictures = null,
        Func<string?>? picture = null)
    {
        _state = state;
        _name = name;
        _pictures = pictures;
        _picture = picture;

        Content = PageFooter.Shell(_body, Phrase, clock, 900);
        Draw();
    }

    /// <summary>Redraws when the reputation, rank record or name has changed since the last draw.</summary>
    public bool Tick()
    {
        var state = _state();

        if (ReferenceEquals(state?.Reputation, _reputationSeen)
            && ReferenceEquals(state?.Ranks, _ranksSeen)
            && string.Equals(_name(), _nameSeen, StringComparison.Ordinal)
            && string.Equals(_picture?.Invoke(), _pictureSeen, StringComparison.Ordinal))
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
        _pictureSeen = _picture?.Invoke();

        _body.Children.Clear();

        var title = TitleText.Build(
            _nameSeen is { Length: > 0 } name ? $"CMDR {name}" : "Commander", TypeScale.Title, TitleRank.Screen);
        var heading = TitleText.Block(title, TitleText.Context("Commander record"));

        if (_pictures is null)
        {
            _body.Children.Add(heading);
        }
        else
        {
            var chooser = new PictureChooser(_pictures, _pictureSeen, PictureSize, "Fly once with D47 running to set your picture.");
            _body.Children.Add(new StackPanel { Spacing = 10, Children = { heading, chooser } });
        }

        _body.Children.Add(Group("Reputation", "Where you stand with each power, from −100 to 100."));

        var rows = new StackPanel { Spacing = 2 };

        foreach (var (key, label) in Powers)
        {
            rows.Children.Add(ReputationRow(label, (state?.Reputation ?? ReputationState.Empty).Reading(key)));
        }

        _body.Children.Add(rows);

        _body.Children.Add(Group("Navy ranks", "Your rank in each navy, what it has unlocked and what the next ranks unlock."));

        var ranks = state?.Ranks ?? RankState.Empty;
        var navies = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 2 };
        var imperial = NavyCell("Imperial navy", Navy.Empire, ranks.For("Empire"));
        var federal = NavyCell("Federal navy", Navy.Federation, ranks.For("Federation"));
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

    private static Control NavyCell(string label, Navy navy, RankStanding? standing)
    {
        Func<int, string?> named = rank => NavalRanks.Name(navy, rank);

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

        var content = new StackPanel
        {
            Spacing = 6,
            Children = { caption, line, Gauge.Track(fill, Gauge.FillKey(GaugeFill.Progress)) },
        };

        if (standing is not null)
        {
            content.Children.Add(UnlockLists(navy, standing.Rank));
        }

        var cell = new Border { Padding = new Thickness(14, 12), Child = content };
        Themed(cell, Border.BackgroundProperty, ThemeManager.SlabKey);

        return cell;
    }

    /// <summary>The unlocked, next and later lists under a navy's bar.</summary>
    private static Control UnlockLists(Navy navy, int rank)
    {
        var unlocks = NavalRanks.UnlocksFor(navy, rank);
        var rankName = (int at) => NavalRanks.Name(navy, at) ?? $"Rank {at}";
        var lists = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 2 };
        Grid.SetIsSharedSizeScope(lists, true);

        lists.Children.Add(UnlockHeading("UNLOCKED", unlocks.Unlocked.Count, ThemeManager.GreyKey, null));

        if (unlocks.Unlocked.Count == 0)
        {
            var first = unlocks.NextRank is { } at ? rankName(at) : rankName(NavyTop);
            lists.Children.Add(Prose($"Nothing yet. {first} unlocks the first one."));
        }

        foreach (var unlock in unlocks.Unlocked)
        {
            lists.Children.Add(UnlockRow(unlock, rankName(unlock.Rank), true, true));
        }

        if (rank >= NavyTop)
        {
            return lists;
        }

        if (unlocks.NextRank is { } nextRank)
        {
            var away = nextRank - rank;
            lists.Children.Add(UnlockHeading(
                $"NEXT · {rankName(nextRank).ToUpperInvariant()}",
                null,
                ThemeManager.AKey,
                $"{away} {(away == 1 ? "RANK" : "RANKS")} AWAY"));

            foreach (var unlock in unlocks.Next)
            {
                lists.Children.Add(UnlockRow(unlock, null, false, true));
            }
        }

        lists.Children.Add(UnlockHeading("LATER", unlocks.Later.Count, ThemeManager.GreyKey, null));

        if (unlocks.Later.Count == 0 && unlocks.LastRank is { } last)
        {
            lists.Children.Add(Prose($"{rankName(last)} is the last rank that unlocks anything."));
        }

        foreach (var unlock in unlocks.Later)
        {
            lists.Children.Add(UnlockRow(unlock, rankName(unlock.Rank), false, false));
        }

        return lists;
    }

    private static Control UnlockHeading(string text, int? count, string key, string? right)
    {
        var heading = new TextBlock
        {
            Text = count is { } n ? $"{text} · {n}" : text,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(heading, TextBlock.ForegroundProperty, key);

        var line = new DockPanel { Margin = new Thickness(0, 10, 0, 2) };

        if (right is not null)
        {
            var away = new TextBlock
            {
                Text = right,
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Meta,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Themed(away, TextBlock.ForegroundProperty, ThemeManager.AKey);
            DockPanel.SetDock(away, Dock.Right);
            line.Children.Add(away);
        }

        line.Children.Add(heading);

        return line;
    }

    private static Control Prose(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return block;
    }

    private static Control UnlockRow(NavyUnlock unlock, string? rankName, bool held, bool named)
    {
        var grid = new Grid
        {
            Height = UnlockRowHeight,
            ColumnDefinitions =
            [
                new ColumnDefinition(held ? 22 : 0, GridUnitType.Pixel),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto) { MinWidth = 64 },
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "rank" },
            ],
        };

        TextBlock Cell(string text, string key, int column, bool mono = false, bool right = false)
        {
            var block = new TextBlock
            {
                Text = text,
                FontFamily = mono ? new FontFamily(Fonts.MonoFamily) : Fonts.ChromeFamily,
                FontSize = mono ? TypeScale.Meta : TypeScale.Control,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
                Margin = column >= 2 ? new Thickness(12, 0, 0, 0) : default,
            };
            Themed(block, TextBlock.ForegroundProperty, key);
            Grid.SetColumn(block, column);
            grid.Children.Add(block);

            return block;
        }

        if (held)
        {
            Cell("✓", ThemeManager.BlueKey, 0);
        }

        Cell(unlock.Name, named ? ThemeManager.WhiteKey : ThemeManager.GreyKey, 1).TextTrimming = TextTrimming.CharacterEllipsis;
        Cell(unlock.Kind.ToString().ToUpperInvariant(), ThemeManager.GreyKey, 2, mono: true);

        if (rankName is not null)
        {
            Cell($"{rankName.ToUpperInvariant()} · {unlock.Rank}", ThemeManager.GreyKey, 3, mono: true, right: true);
        }

        return grid;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
