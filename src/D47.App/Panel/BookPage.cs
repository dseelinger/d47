using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Books;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Media › Books › one book: where it is, how to read it, and its chapters.</summary>
public sealed class BookPage : UserControl, IPageSummary
{
    public const string KeyPrefix = "media-books.book.";

    public const string StartName = "BookStart";

    public const string ReadOnName = "BookReadOn";

    public const string TakeOffName = "BookTakeOff";

    public const string ChapterName = "BookChapter";

    public const string NotOnShelf = "This book is not on the shelf.";

    /// <summary>The chapters the big panel lists at a time.</summary>
    public const int BigChaptersPerPage = 5;

    private readonly BooksHost _host;
    private readonly PanelNavigator _nav;
    private readonly Action<string> _openSettings;
    private readonly BookSource _source;
    private readonly bool _big;
    private readonly StackPanel _body = new() { Spacing = 16, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StatusLine _line = new() { Margin = new Thickness(0, 6, 0, 0) };

    private Book? _text;

    /// <summary>The shelf's download state when <see cref="_text"/> was opened.</summary>
    private BookDownload? _opened;
    private int _first;

    public BookPage(BooksHost host, PanelNavigator nav, BookSource source, Action<string> openSettings, bool big = false)
    {
        _host = host;
        _nav = nav;
        _source = source;
        _openSettings = openSettings;
        _big = big;

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(14), Child = _body },
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        Draw();
    }

    /// <summary>The crumb of one book's page, found again by <see cref="SourceOf"/>.</summary>
    public static NavCrumb CrumbOf(BookSource source, string title) =>
        new(KeyPrefix + source.Id, title) { Help = InterfaceCapability.Id, Whole = true };

    /// <summary>The book a crumb key names, among those on the shelf.</summary>
    public static BookSource? SourceOf(BookShelf shelf, string key) =>
        key.StartsWith(KeyPrefix, StringComparison.Ordinal)
            ? shelf.Books.FirstOrDefault(book => string.Equals(book.Source.Id, key[KeyPrefix.Length..], StringComparison.Ordinal))?.Source
            : null;

    public string Summary => _host.Shelf.Find(_source) is { } entry ? $"{entry.Title}, chapter by chapter." : NotOnShelf;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Player.Changed += OnPlayerChanged;
        _host.Changed += OnPlayerChanged;
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Player.Changed -= OnPlayerChanged;
        _host.Changed -= OnPlayerChanged;
    }

    private void OnPlayerChanged() => Dispatcher.UIThread.Post(Draw);

    private void OpenVoiceSettings() => _openSettings(BooksKit.VoicePlace);

    internal void Draw()
    {
        _body.Children.Clear();

        if (_host.Shelf.Find(_source) is not { } entry)
        {
            _body.Children.Add(Typeset.Prose(NotOnShelf, 15, ThemeManager.GreyKey, wrap: true));
            return;
        }

        _host.Player.RefreshText();

        var playing = _host.Player.Now is { } now && now.Book.Source == _source ? now : null;

        if (_text is null || entry.Download != _opened)
        {
            _opened = entry.Download;
            _text = _host.Shelf.Open(_source);
            entry = _host.Shelf.Find(_source) ?? entry;
        }

        _body.Children.Add(Typeset.Prose(
            $"{entry.Author} · {BooksKit.SourceLabel(_source)} · {(entry.Chapters == 1 ? "1 chapter" : $"{entry.Chapters} chapters")}",
            15,
            ThemeManager.GreyKey,
            wrap: true));

        if (playing is { Problem: not null })
        {
            _body.Children.Add(BooksKit.Stopped(_host, playing, OpenVoiceSettings));
        }

        _body.Children.Add(Block(entry, playing));
        _body.Children.Add(Tiles(entry, playing));

        BooksKit.Say(_line, _host, playing);
        _body.Children.Add(_line);

        _body.Children.Add(Chapters(entry, playing));
    }

    private Control Block(ShelfBook entry, BookPlayback? playing)
    {
        if (playing is not null)
        {
            return BooksKit.NowBlock(_host, playing, titled: false, _big);
        }

        var stack = new StackPanel { Spacing = 8 };
        var place = BooksKit.PlaceOf(entry, null);

        if (entry.Finished)
        {
            place = $"✓ FINISHED · {(entry.Chapters == 1 ? "1 CHAPTER" : $"{entry.Chapters} CHAPTERS")}";
        }
        else if (_text is { } text && entry.Place != new BookPlace(0, 0))
        {
            place = BooksKit.WithPlace(ChapterNumbering.Place(entry.Place.Chapter, entry.Chapters, entry.FrontMatter), $"{BooksKit.Percent(text, entry.Place)}%");
        }

        stack.Children.Add(Typeset.Mono(place, 12, entry.Finished ? ThemeManager.BlueKey : ThemeManager.GreyKey));

        var slab = new Border { Name = BooksKit.NowName, Padding = new Thickness(16, 12), Child = stack };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private Control Tiles(ShelfBook entry, BookPlayback? playing)
    {
        var tiles = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };

        if (playing is not null)
        {
            var pause = BooksKit.Tile(BooksKit.PauseName, BooksKit.PauseText(playing), "Pause or resume the book");
            pause.Click += (_, _) => BooksKit.Pause(_host);
            tiles.Children.Add(pause);

            var back = BooksKit.Tile(BooksKit.BackName, "BACK A PARAGRAPH", "Back a paragraph");
            back.Click += (_, _) => _host.BackAParagraph();
            tiles.Children.Add(back);

            var next = BooksKit.Tile(BooksKit.NextName, "NEXT CHAPTER", "Next chapter");
            next.IsEnabled = _host.HasNextChapter;
            next.Click += (_, _) => _host.NextChapter();
            tiles.Children.Add(next);
        }
        else if (!entry.Finished && entry.Place != new BookPlace(0, 0))
        {
            var readOn = BooksKit.Tile(ReadOnName, "READ ON", "Read on from where you left off");
            readOn.Click += (_, _) => Report(_host.ReadOn(_source));
            tiles.Children.Add(readOn);
        }

        var start = BooksKit.Tile(StartName, "READ FROM THE START", "Read from the start");
        start.Click += (_, _) => Report(_host.ReadFrom(_source, 0));
        tiles.Children.Add(start);

        var takeOff = BooksKit.Tile(TakeOffName, "TAKE OFF THE SHELF", "Take off the shelf");
        takeOff.Classes.Add("destructive");
        takeOff.Click += (_, _) =>
        {
            _host.TakeOff(_source);
            _nav.Back();
        };
        tiles.Children.Add(takeOff);

        return tiles;
    }

    private void Report(bool started)
    {
        if (!started)
        {
            _line.Say(_host.Player.StoryPlaying
                ? D47.Core.Books.BookDesk.StoryPlaying
                : "The book could not be read. Its text may be missing from the shelf.");
        }
    }

    private Control Chapters(ShelfBook entry, BookPlayback? playing)
    {
        var column = new StackPanel { Spacing = Gaps.Tile };
        var chapters = _text?.Chapters ?? [];
        var count = Math.Max(chapters.Count, entry.Chapters);
        var current = playing?.Place.Chapter ?? (entry.Finished || entry.Place == new BookPlace(0, 0) ? -1 : entry.Place.Chapter);

        AutomationProperties.SetName(column, "Chapters");
        column.Children.Add(ListRow.Head($"Chapters · {count}"));

        _first = Math.Clamp(_first, 0, Math.Max(0, (count - 1) / BigChaptersPerPage * BigChaptersPerPage));

        var last = _big ? Math.Min(count, _first + BigChaptersPerPage) : count;

        for (var index = _big ? _first : 0; index < last; index++)
        {
            column.Children.Add(ChapterRow(index, index < chapters.Count ? chapters[index].Title : $"Chapter {index + 1}", index == current, playing));
        }

        if (_big && count > BigChaptersPerPage)
        {
            column.Children.Add(BooksKit.Pager(_first, BigChaptersPerPage, count, first =>
            {
                _first = first;
                Draw();
            }));
        }

        return column;
    }

    private Button ChapterRow(int index, string title, bool current, BookPlayback? playing)
    {
        var name = ListRow.Name(new TextBlock { Text = title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        if (current)
        {
            ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

            if (!_big)
            {
                var sub = ListRow.Sub(new TextBlock { Text = playing is null ? "Left off here" : "Reading now" });
                ThemeInk.Bind(sub, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
                main.Children.Add(sub);
            }
        }

        var aside = Typeset.Mono(
            current && playing is not null ? $"{BooksKit.Percent(playing.Book, playing.Place)}%" : "—",
            13,
            current ? ThemeManager.CyanKey : ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center,
            margin: new Thickness(10, 0, 0, 0));
        DockPanel.SetDock(aside, Dock.Right);

        var row = new Button
        {
            Name = ChapterName,
            Tag = index,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
            Content = new DockPanel { LastChildFill = true, Children = { aside, main } },
        };
        AutomationProperties.SetName(row, title);
        ListRow.Dress(row, current);
        row.Click += (_, _) => Report(_host.ReadFrom(_source, index));

        return row;
    }
}
