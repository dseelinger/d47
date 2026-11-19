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
    private readonly string _key;
    private readonly int? _regionId;
    private readonly string _fallbackName;
    private readonly CodexPictureSet? _pictures;
    private readonly CodexTable _table;
    private readonly bool _headset;
    private readonly PanelNavigator? _nav;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly Func<string, Task<bool>> _confirm;
    private readonly Func<BodySearchLink?>? _bodySearch;
    private readonly D47.App.Voice.WaitSound? _waitSound;
    private readonly WaitingState _waiting = new(WaitForm.Line) { IsVisible = false };
    private readonly StackPanel _body = new();

    private CodexLedger? _seenLedger;
    private string? _seenSystem;
    private CodexPictureKey? _pending;
    private string? _failure;
    private bool _seenSearch;
    private readonly RegionFirstBook? _regionFirsts;
    private long _seenFirsts;
    private readonly RegionFirstPress? _press;
    private long _seenPress;

    public CodexEntryPage(
        Func<CommanderGameState?> state,
        string key,
        int? regionId,
        string name,
        CodexPictureSet? pictures,
        bool headset = false,
        CodexTable? table = null,
        PanelNavigator? nav = null,
        Func<string, Task<bool>>? confirm = null,
        Func<BodySearchLink?>? bodySearch = null,
        Func<string, Task<bool>>? copy = null,
        D47.App.Voice.WaitSound? waitSound = null,
        RegionFirstBook? regionFirsts = null,
        RegionFirstPress? press = null)
    {
        _press = press;
        _regionFirsts = regionFirsts;
        _waitSound = waitSound;
        _copy = copy;
        _nav = nav;
        _bodySearch = bodySearch;
        _confirm = confirm ?? AskAsync;
        _state = state;
        _key = key;
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
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger)
            && _state()?.Location.StarSystem == _seenSystem
            && (_bodySearch?.Invoke() is not null) == _seenSearch
            && (_regionFirsts?.Version ?? 0) == _seenFirsts
            && (_press?.Version ?? 0) == _seenPress)
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

            if (_pictures.Publisher is { } publisher)
            {
                publisher.Changed += PublishChanged;
            }
        }

        Draw();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_pictures is not null)
        {
            _pictures.Store.Arrived -= Arrived;

            if (_pictures.Publisher is { } publisher)
            {
                publisher.Changed -= PublishChanged;
            }
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

    private void PublishChanged() => Dispatcher.UIThread.Post(Draw);

    private void Draw()
    {
        var state = _state();
        _seenLedger = state?.CodexLedger;
        _seenSystem = state?.Location.StarSystem;
        _seenSearch = _bodySearch?.Invoke() is not null;
        _seenFirsts = _regionFirsts?.Version ?? 0;
        _seenPress = _press?.Version ?? 0;

        var view = CodexModel.EntryPage(
            state?.CodexLedger ?? CodexLedger.None,
            _table,
            _key,
            _regionId,
            _fallbackName,
            state?.Location.StarSystem,
            (address, body) => state?.Scans.For(address, body)?.BodyName,
            CodexModel.RegionFirstLookup(_regionFirsts, state));

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

        if (view.Record is { } record)
        {
            _body.Children.Add(RegionFirstBand(record, view.Name));

            if (_press?.Failure(record, inline: false) is { } failed)
            {
                failed.Margin = new Thickness(0, -(_headset ? 6 : 12), 0, _headset ? 14 : 20);
                failed.MaxWidth = 900;
                failed.HorizontalAlignment = HorizontalAlignment.Left;
                _body.Children.Add(failed);
            }
        }

        _body.Children.Add(grid);
    }

    /// <summary>The band above the picture and facts for an entry with a region-first record.</summary>
    private Border RegionFirstBand(RegionFirstRecord record, string entry)
    {
        var word = CreditsKit.Line(CodexModel.RegionFirstWord(record.First), Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.CyanKey);
        word.FontWeight = FontWeight.SemiBold;

        var text = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(12, 10, 14, 10),
            Children =
            {
                word,
                RoutingKit.Ink(CodexModel.RegionFirstSentence(record, entry), TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true),
                CreditsKit.Mono(CodexModel.RegionFirstMeta(record), TypeScale.Small, ThemeManager.GreyKey, right: false),
            },
        };

        var inside = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { text } };

        if (_press?.Slot(record) is { } slot)
        {
            slot.Margin = new Thickness(0, 0, 12, 0);
            slot.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(slot, 1);
            inside.Children.Add(slot);
        }

        var band = new Border
        {
            Name = "CodexRegionFirst",
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, _headset ? 14 : 20),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = inside,
        };
        ThemeInk.Bind(band, Border.BorderBrushProperty, ThemeManager.CyanKey);
        ThemeInk.Bind(band, Border.BackgroundProperty, ThemeManager.CyanGroundKey);

        return band;
    }

    /// <summary>NEW, ALREADY LOGGED, or the kind of a recorded first.</summary>
    internal static string StatusWord(CodexEntryRow row) =>
        row.Possible != CodexFirst.None ? CodexModel.RegionFirstWord(row.Possible) : row.IsNew ? "NEW" : "ALREADY LOGGED";

    internal static string StatusInk(CodexEntryRow row) =>
        row.Possible != CodexFirst.None ? ThemeManager.CyanKey : row.IsNew ? ThemeManager.BlueKey : ThemeManager.GreyKey;

    private StackPanel PictureColumn(CodexEntryView view)
    {
        var column = PictureOf(view);
        AddActions(column, view);
        AddSearch(column);

        return column;
    }

    private StackPanel PictureOf(CodexEntryView view)
    {
        var column = new StackPanel { Width = Side, Spacing = 10, VerticalAlignment = VerticalAlignment.Top };
        var square = new Border { Width = Side, Height = Side };
        ThemeInk.Bind(square, Border.BackgroundProperty, ThemeManager.SlabKey);
        column.Children.Add(square);

        var choice = _pictures is null
            ? null
            : CodexPictures.Find(_key, _regionId, _pictures.Store.LocalKeys(), _pictures.Store.StockRows());

        _pending = null;

        if (choice is not null && _pictures is not null)
        {
            var bytes = choice.Source == CodexPictureSource.Local ? _pictures.Store.Local(choice.Key) : _pictures.Store.Stock(choice.Row!);

            if (Decode(bytes) is { } picture)
            {
                var shown = Pic("CodexPicture", picture);
                SpeakerPictureViewer.Enlarges(shown, picture);
                square.Child = shown;
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

    /// <summary>The Search tile: shown for an entry the codex table lists, other than a star or planet type, on the main window and the headset.</summary>
    private void AddSearch(StackPanel column)
    {
        if (_bodySearch?.Invoke() is not { } link || _table.Entry(_key) is not { Body: null } entry)
        {
            return;
        }

        var button = new Button { Name = "CodexSearch", Content = "Search" };
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["codex_entry"] = entry.Name };

        if (_regionId is { } region && GalacticRegions.NameOf(region) is { } regionName)
        {
            arguments["region"] = regionName;
        }

        button.Click += (_, _) => link.Run(arguments);
        column.Children.Add(button);
    }

    /// <summary>The picture actions: main window only, and only for a region D47 can name.</summary>
    private void AddActions(StackPanel column, CodexEntryView view)
    {
        if (_headset || _nav is not { } nav || _pictures is not { } pictures || _regionId is not { } region)
        {
            return;
        }

        var key = new CodexPictureKey(_key, region);
        var store = pictures.Store;
        var own = store.HasLocal(key);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };

        Button Tile(string name, string label, Action press)
        {
            var button = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, Gaps.Tile, Gaps.Tile) };
            button.Click += (_, _) => press();
            actions.Children.Add(button);

            return button;
        }

        void Choose() => nav.Drill(CodexPage.ChooseCrumb(key));

        var stock = store.StockRows().Any(row => row.Key == key);

        if (!own)
        {
            if (store.CanSave)
            {
                Tile("CodexChoose", "Choose a picture", Choose);
            }
        }
        else
        {
            if (store.HasLocalSource(key))
            {
                Tile("CodexRecrop", "Re-crop", () => nav.Drill(CodexPage.CropCrumb(key, kept: true)));
            }

            Tile("CodexReplace", "Replace", Choose);

            Tile(
                stock ? "CodexUseStock" : "CodexRemoveMine",
                stock ? "Use the stock picture" : "Remove my picture",
                () =>
                {
                    try
                    {
                        store.RemoveLocal(key);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _failure = $"Your picture could not be removed: {ex.Message}";
                    }

                    Draw();
                });
        }

        var publisher = pictures.Publisher is { HasToken: true } present ? present : null;

        if (publisher is not null)
        {
            var idle = publisher.Doing is null;

            if (own)
            {
                Tile("CodexPublish", "Publish", () => _ = Wait(() => publisher.Publish(key), CodexPicturePublisher.PublishingText)).IsEnabled = idle;
            }

            if (stock)
            {
                Tile("CodexUnpublish", "Unpublish", () => _ = UnpublishAsync(publisher, key, view.Name)).IsEnabled = idle;
            }
        }

        column.Children.Add(actions);

        if (publisher?.Doing is not null || _waiting.IsVisible)
        {
            (_waiting.Parent as Avalonia.Controls.Panel)?.Children.Remove(_waiting);
            column.Children.Add(_waiting);
        }
        else if (publisher?.Last is { Failure: { } failed } last && last.Key == key)
        {
            column.Children.Add(Hint(failed));
        }

        if (_failure is not null)
        {
            column.Children.Add(Hint(_failure));
            _failure = null;
        }
    }

    /// <summary>The question Unpublish asks before it sends anything.</summary>
    public static string UnpublishQuestion(string entry, int regionId) =>
        $"Remove the stock picture of {entry} in {GalacticRegions.NameOf(regionId) ?? CodexModel.NoRegionName} for every Commander? "
        + "Pictures they saved themselves are kept.";

    private async Task UnpublishAsync(CodexPicturePublisher publisher, CodexPictureKey key, string entry)
    {
        if (await _confirm(UnpublishQuestion(entry, key.RegionId)).ConfigureAwait(true))
        {
            _ = Wait(() => publisher.Unpublish(key), CodexPicturePublisher.UnpublishingText);
        }
    }

    /// <summary>Starts a publish or unpublish and shows the wait mark here, the only page it was pressed on.</summary>
    private async Task Wait(Func<Task<CodexPublishResult>?> start, string sentence)
    {
        if (start() is not { } running)
        {
            return;
        }

        _waiting.SetSentence(sentence);
        await Waiting.While(_waiting, _waitSound, () => running).ConfigureAwait(true);
    }

    private async Task<bool> AskAsync(string question) =>
        TopLevel.GetTopLevel(this) is Window owner
        && await new ConfirmDialog("Unpublish", question, confirmLabel: "Unpublish", declineLabel: "Keep it", destructive: true)
            .AskAsync(owner)
            .ConfigureAwait(true);

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
            group.Children.Add(LoggingView(logging, _copy));
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
        ThemeInk.Bind(tile, Border.BackgroundProperty, ThemeManager.SlabKey);
        CreditsKit.Place(grid, column, tile);
    }

    private static Border LoggingView(CodexEntryRow row, Func<string, Task<bool>>? copy)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 12 };

        CreditsKit.Place(grid, 0, CodexRegionPage.SystemCell(row, copy));

        if (row.IsNew || row.Possible != CodexFirst.None)
        {
            var status = CreditsKit.Line(StatusWord(row), Fonts.ChromeFamily, TypeScale.Meta, StatusInk(row));
            status.FontWeight = FontWeight.SemiBold;
            CreditsKit.Place(grid, 1, status);
        }

        CreditsKit.Place(grid, 2, CreditsKit.Mono(CodexModel.DateTime(row.At), TypeScale.Small, ThemeManager.WhiteKey, right: true));

        var border = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 6), Child = grid };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }
}
