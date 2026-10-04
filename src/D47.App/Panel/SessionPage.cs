using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › This session: what the session has earned and done since the game started (#554).</summary>
public sealed class SessionPage : UserControl
{
    public const string RootKey = "session";

    /// <summary>The footer's phrase; it carries a <c>get_session_summary</c> keyword.</summary>
    public const string Phrase = "how have I done this session";

    public const string Hint = "The session starts again by itself when the game starts.";

    public const string NotStarted = "The session has not started. It starts when the game loads your commander.";

    private readonly Func<CommanderGameState?> _state;
    private readonly StackPanel _body = new();
    private SessionSummary? _seen;

    public SessionPage(Func<CommanderGameState?> state, JournalClock clock)
    {
        _state = state;

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase, clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(LoadoutPages.Scrolling(new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 900 }, new ColumnDefinition(GridLength.Auto)],
            Children = { _body },
        }));

        Content = root;
        Draw();
    }

    /// <summary>Redraws when the session has changed since the last draw.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.Session, _seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>Time played as <c>H:MM:SS</c>.</summary>
    public static string Played(TimeSpan? elapsed)
    {
        var span = elapsed ?? TimeSpan.Zero;
        return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}");
    }

    /// <summary>An amount of credits as the page prints it: <c>1,234 CR</c>.</summary>
    public static string Credits(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + " CR";

    private void Draw()
    {
        var session = _state()?.Session;
        _seen = session;

        _body.Children.Clear();

        if (session is not { IsKnown: true })
        {
            _body.Children.Add(TitleText.Block(
                TitleText.Build("This session", TypeScale.Title, TitleRank.Screen),
                TitleText.Context("Commander record")));
            _body.Children.Add(Note(NotStarted));
            return;
        }

        _body.Children.Add(TitleText.Block(
            TitleText.Build("This session", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Commander record · since the game started"),
            Earned(session.TotalEarnings)));

        var count = (int value) => value.ToString("N0", CultureInfo.InvariantCulture);
        var grid = StatTile.Grid(
            [
                StatTile.Build("Time played", Played(session.Elapsed), StatInk.Number),
                StatTile.Build("Jumps", count(session.Jumps), StatInk.Number),
                StatTile.Build("Distance", session.DistanceTravelled.ToString("N1", CultureInfo.InvariantCulture) + " LY", StatInk.Number),
                StatTile.Build("Bodies scanned", count(session.BodiesScanned), StatInk.Number),
                StatTile.Build("Materials gained", count(session.MaterialsGained), StatInk.Number),
                StatTile.Build("Interdictions", count(session.Interdictions), StatInk.Number),
                StatTile.Build("Deaths", count(session.Deaths), StatInk.Number),
                StatTile.Build("Balance at start", session.Balance is { } balance ? Credits(balance) : "—", StatInk.Number),
            ],
            maxColumns: 4);
        grid.Margin = new Thickness(0, 28, 0, 0);
        _body.Children.Add(grid);

        _body.Children.Add(Group("Earnings by source", "What each kind of work has paid this session."));

        (string Label, long Amount)[] sources =
        [
            ("Bounties", session.BountyEarnings),
            ("Combat bonds", session.CombatBondEarnings),
            ("Trade", session.TradeEarnings),
            ("Exploration", session.ExplorationEarnings),
            ("Missions", session.MissionEarnings),
            ("Vouchers", session.VoucherEarnings),
        ];

        var largest = sources.Max(source => source.Amount);
        var rows = new StackPanel { Spacing = 2 };

        foreach (var (label, amount) in sources)
        {
            rows.Children.Add(SourceRow(label, amount, largest > 0 ? (double)amount / largest : 0));
        }

        _body.Children.Add(rows);
        _body.Children.Add(Note(Hint));
    }

    private static Control Earned(long total)
    {
        var name = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = "EARNED",
        };
        Themed(name, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var figure = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Figure,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = Credits(total),
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return new StackPanel { Spacing = 2, Children = { name, figure } };
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

    private static Control SourceRow(string label, long amount, double fill)
    {
        var name = new TextBlock
        {
            Text = label,
            FontSize = TypeScale.Control,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var gauge = Gauge.Track(fill, Gauge.FillKey(GaugeFill.Progress));
        gauge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(gauge, 1);

        var figure = new TextBlock
        {
            Text = Credits(amount),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(figure, TextBlock.ForegroundProperty, amount == 0 ? ThemeManager.GreyKey : ThemeManager.AKey);
        Grid.SetColumn(figure, 2);

        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("200,*,170"),
                Children = { name, gauge, figure },
            },
        };
        Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Note(string text)
    {
        var note = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 0),
        };
        Themed(note, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return note;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
