using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Watches;

namespace D47.App.Panel;

/// <summary>Commander › Watches: the background watches, with an Enabled checkbox and a Delete tile on each.</summary>
public sealed class WatchesPage : UserControl
{
    public const string RootKey = "watches";

    public const string NewStockKey = "watches.new-stock";

    public const string NewGoalKey = "watches.new-goal";

    public const string EditPrefix = "watches.edit.";

    public const string Intro =
        "D47 checks each enabled watch whenever it is running, with Elite open or closed. "
        + "When a watch holds, D47 says so once, and not again until the next BGS tick.";

    public const string GoalSub = "Reads GalNet launch articles · can miss or misread one";

    public const string BgsTickLabel = "BGS tick time is off";

    public const string BgsTickText =
        "Every watch counts from the BGS tick, so none of them is checked until BGS tick time is on under Outside sources.";

    public const string GalaxyLabel = "Galaxy search is off";

    public const string GalaxyText =
        "A stock watch reads the station's market through Galaxy search, so it is not checked until Galaxy search is on under Outside sources.";

    public const string GalNetLabel = "GalNet news is off";

    public const string GalNetText =
        "This watch reads GalNet articles, so it hears nothing until GalNet news is on under Outside sources.";

    public const string TurnOn = "TURN ON";

    private readonly WatchesDesk _desk;
    private readonly PanelNavigator _nav;
    private readonly StackPanel _body = new();
    private (IReadOnlyList<Watch> Watches, IReadOnlyList<WatchProblem> Problems, bool Galaxy, bool Bgs, bool GalNet) _seen;

    public WatchesPage(WatchesDesk desk, PanelNavigator nav)
    {
        _desk = desk;
        _nav = nav;

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
    public static string Sub(Watch watch) => watch.Kind == WatchKind.NewCommunityGoal
        ? GoalSub
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{(watch.Comparison == StockComparison.Below ? "Below" : "Above")} {watch.Threshold} · {watch.Station}, {watch.System}");

    /// <summary>Redraws when the watches or a gate have changed since the last draw.</summary>
    public bool Tick()
    {
        if (Seen().Equals(_seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    internal static Notice Gate(string label, string text, Action turnOn, Action redraw)
    {
        var notice = new Notice(NoticeLevel.Warning) { Label = label, Text = text };
        var tile = new Button { Content = TurnOn, Height = TypeScale.MinimumTarget };
        tile.Click += (_, _) =>
        {
            turnOn();
            redraw();
        };
        notice.Actions.Add(tile);

        return notice;
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

    private (IReadOnlyList<Watch>, IReadOnlyList<WatchProblem>, bool, bool, bool) Seen() =>
        (_desk.Store.Watches, _desk.Store.Problems, _desk.GalaxySearch, _desk.BgsTick, _desk.GalNet);

    private void Draw()
    {
        _seen = Seen();
        _body.Children.Clear();
        _body.Spacing = 20;

        var watches = _desk.Store.Watches;
        var stock = watches.Where(watch => watch.Kind == WatchKind.Stock).ToList();
        var goals = watches.Where(watch => watch.Kind == WatchKind.NewCommunityGoal).ToList();

        _body.Children.Add(TitleText.Screen("Watches"));

        if (!_desk.BgsTick)
        {
            _body.Children.Add(Gate(BgsTickLabel, BgsTickText, _desk.TurnOnBgsTick, Draw));
        }

        var intro = Text(Intro, Fonts.ProseFamily, TypeScale.Body, ThemeManager.GreyKey);
        intro.TextWrapping = TextWrapping.Wrap;
        _body.Children.Add(intro);

        foreach (var problem in _desk.Store.Problems)
        {
            _body.Children.Add(new Notice(NoticeLevel.Error) { Label = "A watch was not read", Text = $"{problem.Where}: {problem.Reason}" });
        }

        _body.Children.Add(Section("Stock", stock, _desk.GalaxySearch ? null : (GalaxyLabel, GalaxyText, _desk.TurnOnGalaxySearch)));
        _body.Children.Add(Section("New Community Goal", goals, _desk.GalNet ? null : (GalNetLabel, GalNetText, _desk.TurnOnGalNet)));

        var full = watches.Count >= WatchStore.MaxWatches;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };
        footer.Children.Add(AddTile("ADD STOCK WATCH", "Add stock watch", NewStockKey, "New stock watch", full));
        footer.Children.Add(AddTile("ADD COMMUNITY GOAL WATCH", "Add Community Goal watch", NewGoalKey, "New Community Goal watch", full));

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

    private StackPanel Section(string heading, List<Watch> watches, (string Label, string Text, Action TurnOn)? gate)
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

        if (gate is { } off && watches.Count > 0)
        {
            section.Children.Add(Gate(off.Label, off.Text, off.TurnOn, Draw));
        }

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

        var fired = WatchFired.Text(watch.FiredForTick, TimeZoneInfo.Local);
        var aside = Text(fired, Fonts.MonoFamily, TypeScale.Meta, watch.FiredForTick is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
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
