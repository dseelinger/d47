using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Search › All results: every result Find held when it was chosen, grouped by kind.</summary>
internal sealed class AllResultsPage : UserControl
{
    public const string PageKey = "search.allresults";

    public const string RowClass = "AllResultsRow";

    public const string RootName = "AllResultsPage";

    /// <summary>The most rows one group shows.</summary>
    public const int MostPerGroup = 20;

    private const double ColumnWidth = 900;

    private readonly List<Button> _rows = [];

    public AllResultsPage(string query, IReadOnlyList<PanelFindResult> results, Action<PanelFindResult> open)
    {
        Name = RootName;

        var body = new StackPanel { MaxWidth = ColumnWidth, HorizontalAlignment = HorizontalAlignment.Left };

        var summary = new TextBlock
        {
            Text = $"{results.Count} {(results.Count == 1 ? "result" : "results")} for “{query}”, as Find held them.",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(summary, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        body.Children.Add(summary);

        foreach (var group in Groups)
        {
            var rows = results.Where(group.Holds).Take(MostPerGroup).ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            var count = new TextBlock
            {
                Text = rows.Count.ToString(CultureInfo.InvariantCulture),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Meta,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            ThemeInk.Bind(count, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

            body.Children.Add(RoutingKit.Section(group.Head, count));

            var list = new StackPanel { Spacing = 2 };

            foreach (var result in rows)
            {
                var row = Row(result, group.Tall, open);
                _rows.Add(row);
                list.Children.Add(row);
            }

            body.Children.Add(list);
        }

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = body,
        };

        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => _rows.FirstOrDefault()?.Focus());
    }

    internal IReadOnlyList<Button> Rows => _rows;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up) || _rows.Count == 0)
        {
            return;
        }

        var at = _rows.FindIndex(row => row.IsFocused);
        var next = Math.Clamp(at < 0 ? 0 : at + (e.Key == Key.Down ? 1 : -1), 0, _rows.Count - 1);

        e.Handled = true;
        _rows[next].Focus();
        _rows[next].BringIntoView();
    }

    private static Button Row(PanelFindResult result, bool tall, Action<PanelFindResult> open)
    {
        var title = new TextBlock { Text = result.Title, FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Tip, FontWeight = FontWeight.Medium };
        ThemeInk.Bind(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title } };

        if (tall)
        {
            var path = new TextBlock { Text = result.Path, FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Small, TextTrimming = TextTrimming.CharacterEllipsis };
            ThemeInk.Bind(path, TextBlock.ForegroundProperty, ThemeManager.AKey);
            lines.Children.Add(path);
        }

        var dock = new DockPanel { LastChildFill = true };
        Control? trailing = null;

        if (tall && Kind(result.Kind) is { } word && result.Kind is not PanelFindKind.Bookmark)
        {
            trailing = new TextBlock { Text = word, FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Caption, FontWeight = FontWeight.Medium, LetterSpacing = TypeScale.Caption * 0.06 };
            ThemeInk.Bind(trailing, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        }
        else if (result.LightYears is { } lightYears && result.Kind is PanelFindKind.Station or PanelFindKind.Carrier)
        {
            trailing = new TextBlock { Text = lightYears.ToString("N1", CultureInfo.InvariantCulture) + " LY", FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta };
            ThemeInk.Bind(trailing, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        }

        if (trailing is not null)
        {
            trailing.VerticalAlignment = VerticalAlignment.Center;
            trailing.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(trailing, Dock.Right);
            dock.Children.Add(trailing);
        }

        dock.Children.Add(lines);

        var row = new Button
        {
            Tag = result.Title,
            MinHeight = tall ? 50 : TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 4),
            Content = dock,
        };
        row.Classes.Add(RowClass);
        row.Click += (_, _) => open(result);

        return row;
    }

    private static string? Kind(PanelFindKind kind) => kind switch
    {
        PanelFindKind.Page => "PAGE",
        PanelFindKind.SettingsPlace => "PLACE",
        PanelFindKind.SettingRow => "SETTING",
        PanelFindKind.HelpPage => "HELP",
        _ => null,
    };

    private sealed record Group(string Head, bool Tall, Func<PanelFindResult, bool> Holds);

    private static readonly Group[] Groups =
    [
        new("PAGES, SETTINGS AND HELP", true, r => r.Kind is PanelFindKind.Page or PanelFindKind.SettingsPlace or PanelFindKind.SettingRow or PanelFindKind.HelpPage),
        new("STAR SYSTEMS", false, r => r.Kind == PanelFindKind.StarSystem),
        new("STATIONS", false, r => r.Kind == PanelFindKind.Station),
        new("FLEET CARRIERS", false, r => r.Kind == PanelFindKind.Carrier),
        new("MINOR FACTIONS", false, r => r.Kind == PanelFindKind.Faction),
        new("ENGINEERS", false, r => r.Kind == PanelFindKind.Engineer),
        new("BOOKMARKS", true, r => r.Kind == PanelFindKind.Bookmark),
    ];
}
