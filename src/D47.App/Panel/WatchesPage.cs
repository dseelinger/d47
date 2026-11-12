using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Watches;

namespace D47.App.Panel;

/// <summary>Commander › Watches: the background watches, with an Enabled checkbox and a Delete tile on each.</summary>
public sealed class WatchesPage : UserControl
{
    public const string RootKey = "watches";

    public const string NewStockKey = "watches.new-stock";

    public const string NewGoalKey = "watches.new-goal";

    public const string NewNextKey = "watches.new-next";

    public const string EditPrefix = "watches.edit.";

    public const string Intro =
        "D47 checks each enabled watch while it is running. A stock or Community Goal watch speaks once per BGS tick. "
        + "A next-system watch speaks when you arrive in a system that has what it names, then turns itself off.";

    public const string NextNote =
        "A watch that has spoken shows where and when, and its Enabled box is cleared. Tick it to watch for the next one.";

    public const string SectionsName = "Watch kinds";

    public static IReadOnlyList<string> Sections { get; } = ["Stock", "Community Goal", "Next system with"];

    public const string GoalSub = "Reads GalNet launch articles · can miss or misread one";

    private readonly WatchesDesk _desk;
    private readonly PanelNavigator _nav;
    private readonly StackPanel _body = new();
    private readonly bool _headset;
    private readonly Segment _segment;
    private int _section;
    private string[] _statusTexts = [];
    private (IReadOnlyList<Watch> Watches, IReadOnlyList<WatchProblem> Problems) _seen;

    public WatchesPage(WatchesDesk desk, PanelNavigator nav, bool headset = false)
    {
        _desk = desk;
        _nav = nav;
        _headset = headset;

        _segment = new Segment
        {
            Name = SectionsName,
            ItemsSource = Sections,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = headset ? TypeScale.MinimumTarget : Segment.StatusHeight,
        };
        AutomationProperties.SetName(_segment, SectionsName);
        _segment.SelectionChanged += (_, _) =>
        {
            if (_segment.SelectedIndex is >= 0 and var chosen && chosen != _section)
            {
                _section = chosen;
                Draw();
            }
        };

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children = { LoadoutPages.Scrolling(new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 900 }], Children = { _body } }) },
        };

        Draw();
    }

    /// <summary>The key of the page that edits <paramref name="watch"/>.</summary>
    public static string EditKey(Watch watch) => EditPrefix + watch.Id.ToString("N", CultureInfo.InvariantCulture);

    /// <summary>The watch an edit page's key names, or null.</summary>
    public static Guid? EditOf(string key) =>
        key.StartsWith(EditPrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(key[EditPrefix.Length..], "N", out var id)
            ? id
            : null;

    /// <summary>"3 OF 32".</summary>
    public static string Tally(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} OF {WatchStore.MaxWatches}");

    /// <summary>"Above 200 · Cheranovsky City, Ngurii".</summary>
    public static string Sub(Watch watch) => watch.Kind switch
    {
        WatchKind.NewCommunityGoal => GoalSub,
        WatchKind.NextSystem => NextSub(watch),
        _ => string.Create(
            CultureInfo.InvariantCulture,
            $"{(watch.Comparison == StockComparison.Below ? "Below" : "Above")} {watch.Threshold} · {watch.Station}, {watch.System}"),
    };

    /// <summary>"Material Trader · Manufactured · any pad · on arrival".</summary>
    public static string NextSub(Watch watch)
    {
        var parts = new List<string> { watch.Service ?? string.Empty };

        if (!string.IsNullOrWhiteSpace(watch.ServiceKind))
        {
            parts.Add(watch.ServiceKind);
        }

        parts.Add(watch.MinPad switch
        {
            PadSize.Large => "large pad",
            PadSize.Medium => "medium or large pad",
            _ => "any pad",
        });
        parts.Add(watch.CheckRoute ? "on arrival or on a plotted route" : "on arrival");

        return string.Join(" · ", parts);
    }

    /// <summary>"3 watches · 2 on"; the count of those on is left out when all are.</summary>
    public static string Status(IReadOnlyList<Watch> watches)
    {
        var on = watches.Count(watch => watch.Enabled);
        var count = string.Create(CultureInfo.InvariantCulture, $"{watches.Count} {(watches.Count == 1 ? "watch" : "watches")}");

        return on < watches.Count ? count + string.Create(CultureInfo.InvariantCulture, $" · {on} on") : count;
    }

    /// <summary>Redraws when the watches have changed since the last draw.</summary>
    public bool Tick()
    {
        if (Seen().Equals(_seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    internal static TextBlock Text(string text, string family, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private (IReadOnlyList<Watch>, IReadOnlyList<WatchProblem>) Seen() =>
        (_desk.Store.Watches, _desk.Store.Problems);

    private void Draw()
    {
        _seen = Seen();
        _body.Children.Clear();
        _body.Spacing = 20;

        var watches = _desk.Store.Watches;
        var stock = watches.Where(watch => watch.Kind == WatchKind.Stock).ToList();
        var goals = watches.Where(watch => watch.Kind == WatchKind.NewCommunityGoal).ToList();
        var next = watches.Where(watch => watch.Kind == WatchKind.NextSystem).ToList();

        _body.Children.Add(TitleText.Screen("Watches"));

        var intro = Text(Intro, Fonts.ProseFamily, TypeScale.Body, ThemeManager.GreyKey);
        intro.TextWrapping = TextWrapping.Wrap;
        _body.Children.Add(intro);

        foreach (var problem in _desk.Store.Problems)
        {
            _body.Children.Add(new Notice(NoticeLevel.Error) { Label = "A watch was not read", Text = $"{problem.Where}: {problem.Reason}" });
        }

        var texts = new[] { Status(stock), Status(goals), Status(next) };

        if (!_headset && !texts.SequenceEqual(_statusTexts))
        {
            _statusTexts = texts;
            _segment.Statuses = [.. texts.Select(text => (ChoiceStatus?)new ChoiceStatus(text, ChoiceTone.Grey))];
        }

        _body.Children.Add(_segment);

        switch (_section)
        {
            case 0:
                _body.Children.Add(Section("Stock", stock));
                break;
            case 1:
                _body.Children.Add(Section("New Community Goal", goals));
                break;
            default:
                _body.Children.Add(Section("Next system with", next));

                var note = Text(NextNote, Fonts.ProseFamily, TypeScale.Body, ThemeManager.GreyKey);
                note.TextWrapping = TextWrapping.Wrap;
                _body.Children.Add(note);
                break;
        }

        var full = watches.Count >= WatchStore.MaxWatches;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };
        footer.Children.Add(AddTile("ADD STOCK WATCH", "Add stock watch", NewStockKey, "New stock watch", full));
        footer.Children.Add(AddTile("ADD COMMUNITY GOAL WATCH", "Add Community Goal watch", NewGoalKey, "New Community Goal watch", full));

        footer.Children.Add(AddTile("ADD NEXT-SYSTEM WATCH", "Add next-system watch", NewNextKey, "New next-system watch", full));

        var tally = Text(Tally(watches.Count), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey);
        tally.Margin = new Thickness(14, 0, 0, 0);
        footer.Children.Add(tally);
        _body.Children.Add(footer);
    }

    private Button AddTile(string content, string name, string key, string word, bool disabled)
    {
        var tile = new Button { Content = content, Height = TypeScale.MinimumTarget, IsEnabled = !disabled };
        AutomationProperties.SetName(tile, name);
        tile.Click += (_, _) => _nav.Drill(new NavCrumb(key, word) { Help = D47.Core.Capabilities.Builtin.WatchesCapability.Id, Whole = true });

        return tile;
    }

    private StackPanel Section(string heading, List<Watch> watches)
    {
        var section = new StackPanel { Spacing = Gaps.Tile };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
        var name = Text(heading.ToUpperInvariant(), Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.GreyKey);
        name.FontWeight = FontWeight.SemiBold;
        name.LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking;
        head.Children.Add(name);

        var count = Text(watches.Count.ToString(CultureInfo.InvariantCulture), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey);
        Grid.SetColumn(count, 1);
        head.Children.Add(count);
        section.Children.Add(head);

        foreach (var watch in watches)
        {
            section.Children.Add(Row(watch));
        }

        return section;
    }

    private Grid Row(Watch watch)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,180,160"), ColumnSpacing = Gaps.Tile };

        var main = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var name = Text(watch.Name, Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);
        name.FontWeight = FontWeight.SemiBold;
        main.Children.Add(name);
        main.Children.Add(Text(Sub(watch), Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));

        var fired = WatchFired.Of(watch, TimeZoneInfo.Local);
        var never = fired == WatchFired.NotYet;
        var waiting = watch.Kind == WatchKind.NextSystem && never;
        var aside = Text(
            waiting ? (watch.Enabled ? "Watching" : "Off") : fired,
            waiting ? Fonts.ChromeFamily : Fonts.MonoFamily,
            TypeScale.Meta,
            never ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
        aside.HorizontalAlignment = HorizontalAlignment.Right;

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        content.Children.Add(main);
        Grid.SetColumn(aside, 1);
        content.Children.Add(aside);

        var open = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 52,
        };
        AutomationProperties.SetName(open, watch.Name);
        open.Click += (_, _) => _nav.Drill(new NavCrumb(EditKey(watch), watch.Name) { Help = D47.Core.Capabilities.Builtin.WatchesCapability.Id, Whole = true });

        var enabled = LabeledCheckBox.Caps("Enabled");
        enabled.IsChecked = watch.Enabled;
        AutomationProperties.SetName(enabled, $"{watch.Name} enabled");
        enabled.IsCheckedChanged += (_, _) =>
        {
            if ((enabled.IsChecked == true) != watch.Enabled)
            {
                _desk.Store.Update(watch with { Enabled = enabled.IsChecked == true });
            }
        };

        var delete = new Button { Content = "DELETE", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        delete.Classes.Add("destructive");
        AutomationProperties.SetName(delete, $"Delete {watch.Name}");
        delete.Click += (_, _) =>
        {
            _desk.Store.Delete(watch.Id);
            Draw();
        };

        grid.Children.Add(open);
        Grid.SetColumn(enabled, 1);
        grid.Children.Add(enabled);
        Grid.SetColumn(delete, 2);
        grid.Children.Add(delete);

        return grid;
    }
}
