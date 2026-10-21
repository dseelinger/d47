using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region › an entry: its picture, its facts and each time it was logged in the region.</summary>
public sealed class CodexEntryPage : UserControl, IPageSummary
{
    private readonly Func<CommanderGameState?> _state;
    private readonly long _entryId;
    private readonly int? _regionId;
    private readonly string _fallbackName;
    private readonly CodexPictureSet? _pictures;
    private readonly CodexTable _table;
    private readonly bool _headset;
    private readonly StackPanel _body = new();

    private CodexLedger? _seenLedger;
    private string? _seenSystem;
    private CodexPictureKey? _pending;

    public CodexEntryPage(
        Func<CommanderGameState?> state,
        long entryId,
        int? regionId,
        string name,
        CodexPictureSet? pictures,
        bool headset = false,
        CodexTable? table = null)
    {
        _state = state;
        _entryId = entryId;
        _regionId = regionId;
        _fallbackName = name;
        _pictures = pictures;
        _headset = headset;
        _table = table ?? CodexCatalogue.Table;

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(_body);
        Draw();
    }

    public string Summary => _headset ? CodexPage.HeadsetSummaryText : CodexPage.SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>Redraws when the record or the current system has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger) && _state()?.Location.StarSystem == _seenSystem)
        {
            return false;
        }

        Draw();

        return true;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_pictures is not null)
        {
            _pictures.Store.Arrived += Arrived;
        }

        if (_pending is not null)
        {
            Draw();
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_pictures is not null)
        {
            _pictures.Store.Arrived -= Arrived;
        }
    }

    private double Side => _headset ? 300 : 400;

    private void Arrived(CodexPictureKey key)
    {
        if (key == _pending)
        {
            Dispatcher.UIThread.Post(Draw);
        }
    }

    private void Draw()
    {
        var state = _state();
        _seenLedger = state?.CodexLedger;
        _seenSystem = state?.Location.StarSystem;

        var view = CodexModel.EntryPage(
            state?.CodexLedger ?? CodexLedger.None,
            _table,
            _entryId,
            _regionId,
            _fallbackName,
            state?.Location.StarSystem,
            (address, body) => state?.Scans.For(address, body)?.BodyName);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = _headset ? 20 : 28,
        };

        grid.Children.Add(PictureColumn(view));

        var facts = Facts(view);
        Grid.SetColumn(facts, 1);
        grid.Children.Add(facts);

        _body.Children.Clear();
        _body.Children.Add(grid);
    }

    private StackPanel PictureColumn(CodexEntryView view)
    {
        var column = new StackPanel { Width = Side, Spacing = 10, VerticalAlignment = VerticalAlignment.Top };
        var square = new Border { Width = Side, Height = Side };
        RoutingKit.Themed(square, Border.BackgroundProperty, ThemeManager.SlabKey);
        column.Children.Add(square);

        var choice = _pictures is null
            ? null
            : CodexPictures.Find(_entryId, _regionId, _pictures.Store.LocalKeys(), _pictures.Store.StockRows());

        _pending = null;

        if (choice is not null && _pictures is not null)
        {
            var bytes = choice.Source == CodexPictureSource.Local ? _pictures.Store.Local(choice.Key) : _pictures.Store.Stock(choice.Row!);

            if (Decode(bytes) is { } picture)
            {
                square.Child = Pic("CodexPicture", picture);
                column.Children.Add(CreditsKit.Label(CodexModel.PictureLine(choice)));

                return column;
            }

            _pending = choice.Key;
            column.Children.Add(Hint(CodexModel.FetchingText));

            return column;
        }

        if (view.PlaceholderFile is { } file && _pictures?.Placeholder(file) is { } placeholder && Decode(placeholder) is { } family)
        {
            square.Child = Pic("CodexPlaceholder", family);
        }

        column.Children.Add(Hint(_pictures?.StockOn == false ? CodexModel.StockOffText : CodexModel.NoPictureText));

        return column;
    }

    private Image Pic(string name, Bitmap source) => new()
    {
        Name = name,
        Source = source,
        Stretch = Stretch.Uniform,
        Width = Side,
        Height = Side,
    };

    private static Bitmap? Decode(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);

            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static TextBlock Hint(string text) => RoutingKit.Ink(text, TypeScale.Small, ThemeManager.GreyKey, wrap: true);

    private StackPanel Facts(CodexEntryView view)
    {
        var column = new StackPanel { Spacing = 24 };

        if (view.Category is not null || view.SubCategory is not null)
        {
            var tiles = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = Gaps.Tile };
            Tile(tiles, 0, "Category", view.Category);
            Tile(tiles, 1, "Subcategory", view.SubCategory);
            column.Children.Add(tiles);
        }

        var group = new StackPanel { Spacing = Gaps.Tile };
        group.Children.Add(StarSystemPage.GroupHead(
            CodexModel.LoggedHereHead, CodexKit.Line(string.Empty), view.Loggings.Count > 0 ? CodexModel.Number(view.Loggings.Count) : null));

        if (view.Loggings.Count == 0)
        {
            group.Children.Add(Hint(CodexModel.NotHereText));
        }

        foreach (var logging in view.Loggings)
        {
            group.Children.Add(LoggingView(logging));
        }

        column.Children.Add(group);

        return column;
    }

    private static void Tile(Grid grid, int column, string label, string? value)
    {
        var tile = new Border
        {
            Padding = new Thickness(14, 8),
            Child = new StackPanel
            {
                Children =
                {
                    CreditsKit.Label(label),
                    CreditsKit.Line(value ?? "—", Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey),
                },
            },
        };
        RoutingKit.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);
        CreditsKit.Place(grid, column, tile);
    }

    private static Border LoggingView(CodexEntryRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 12 };

        CreditsKit.Place(grid, 0, CodexRegionPage.SystemCell(row));

        if (row.IsNew)
        {
            var status = CreditsKit.Line("NEW", Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.BlueKey);
            status.FontWeight = FontWeight.SemiBold;
            CreditsKit.Place(grid, 1, status);
        }

        CreditsKit.Place(grid, 2, CreditsKit.Mono(CodexModel.DateTime(row.At), TypeScale.Small, ThemeManager.WhiteKey, right: true));

        var border = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 6), Child = grid };
        RoutingKit.Themed(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }
}
