using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Books;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// Media › Books › Add a book: the Project Gutenberg catalogue, where the surface has one, and the .txt and .epub files
/// in the books folder that are not on the shelf.
/// </summary>
public sealed class BookAddPage : UserControl, IPageSummary
{
    public const string Key = "media-books-add";

    public const string PageSummary = "Choose a book to put on your shelf.";

    public const string HelpText = "D47 lists the .txt and .epub files in your books folder. A file you choose is copied there, so it is on the shelf in the headset too.";

    public const string NothingToAdd = "No .txt or .epub files are waiting in your books folder.";

    public const string FolderLabel = "YOUR BOOKS FOLDER";

    public const string ChooseName = "BookChooseFile";

    public const string OpenFolderName = "BookOpenFolder";

    public const string RowName = "BookFileRow";

    public const string AddName = "BookAddAndRead";

    public const string BackName = "BookAddBack";

    public const string RefusalName = "BookFileRefusal";

    public const string SourceName = "BookAddSource";

    public const string GutenbergRowName = "GutenbergBookRow";

    public const string UnreachableName = "GutenbergUnreachable";

    public const string Retrieving = "Retrieving results…";

    public const string UnreachableLabel = "Could not retrieve results";

    public const string UnreachableText = "Books already on your shelf still read. A book cut off part way reads up to where its text stops, and the rest comes in next time Gutenberg answers.";

    public const string OnYourShelf = "ON YOUR SHELF";

    /// <summary>The two places a book comes from, in the order the Segmented shows them.</summary>
    public static IReadOnlyList<string> Sources { get; } = ["Project Gutenberg", "A file on this PC"];

    private readonly BooksHost _host;
    private readonly PanelNavigator _nav;
    private readonly WaitSound? _wait;
    private readonly bool _big;
    private readonly StackPanel _refused = new() { Spacing = 8 };
    private readonly StackPanel _list = new() { Spacing = Gaps.Tile };
    private readonly StatusLine _line = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _add = BooksKit.Tile(AddName, "ADD AND READ", "Add the book and read it");
    private readonly StackPanel _files;
    private readonly StackPanel? _gutenberg;
    private readonly GutenbergFilters _filters = new();
    private readonly StatusLine _retrieving = new();
    private readonly Notice _unreachable = new(NoticeLevel.Error, inline: true)
    {
        Name = UnreachableName,
        Label = UnreachableLabel,
        Text = UnreachableText,
        IsVisible = false,
    };
    private readonly StackPanel _books = new() { Spacing = Gaps.Tile };
    private readonly StatusLine _fetching = new() { Margin = new Thickness(0, 6, 0, 0) };

    private BookFolderListing _listing = BookFolderListing.Empty;
    private string? _selected;
    private int _version;
    private GutenbergCatalogue? _catalogue;
    private bool _asked;
    private int _query;
    private GutenbergResult _result = new([], 0);
    private int? _chosen;
    private bool _adding;

    public BookAddPage(BooksHost host, PanelNavigator nav, bool big = false, WaitSound? wait = null)
    {
        _host = host;
        _nav = nav;
        _big = big;
        _wait = wait;

        var body = new StackPanel { Spacing = big ? 8 : 12, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };

        _files = new StackPanel { Spacing = big ? 8 : 12 };

        if (!_big)
        {
            _files.Children.Add(Typeset.Prose(HelpText, 15, ThemeManager.GreyKey, wrap: true));
        }

        _files.Children.Add(FolderSlab());

        if (!_big)
        {
            _files.Children.Add(DesktopTiles());
        }

        _files.Children.Add(_refused);
        _files.Children.Add(_list);
        _files.Children.Add(_line);

        if (host.OffersGutenberg)
        {
            var source = new Segment
            {
                Name = SourceName,
                ItemsSource = Sources,
                Statuses =
                [
                    new ChoiceStatus("Free books, read as they arrive", ChoiceTone.Grey),
                    new ChoiceStatus(".txt or .epub · nothing sent", ChoiceTone.Grey),
                ],
                SelectedIndex = 0,
            };
            AutomationProperties.SetName(source, "Where the book comes from");
            source.SelectionChanged += (_, _) =>
            {
                if (source.SelectedIndex >= 0)
                {
                    ShowSource(gutenberg: source.SelectedIndex == 0);
                }
            };

            _filters.Changed += (_, _) => _ = QueryAsync();

            _gutenberg = new StackPanel
            {
                Spacing = big ? 8 : 12,
                Children = { _filters, _retrieving, _unreachable, _books, _fetching },
            };

            body.Children.Add(source);
            body.Children.Add(_gutenberg);
            _files.IsVisible = false;
        }

        body.Children.Add(_files);

        _add.IsEnabled = false;
        _add.MinWidth = 120;
        _add.HorizontalAlignment = HorizontalAlignment.Stretch;
        _add.Click += (_, _) =>
        {
            if (Gutenberg)
            {
                _ = AddChosenAsync();
            }
            else
            {
                AddSelected();
            }
        };

        var back = BooksKit.Tile(BackName, "BACK", "Back");
        back.MinWidth = 120;
        back.Click += (_, _) => _nav.Back();

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { back, _add },
        };
        DockPanel.SetDock(footer, Dock.Bottom);

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children =
            {
                footer,
                new ScrollViewer
                {
                    Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = body },
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                },
            },
        };

        DrawList();
    }

    private bool Gutenberg => _gutenberg is { IsVisible: true };

    /// <summary>The crumb of the Add a book page.</summary>
    public static NavCrumb Crumb { get; } = new(Key, "Add a book") { Help = InterfaceCapability.Id, Whole = true };

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.FilesChanged += OnFilesChanged;
        _ = RefreshAsync();

        if (Gutenberg)
        {
            AskForCatalogue();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.FilesChanged -= OnFilesChanged;
    }

    private void OnFilesChanged() => Dispatcher.UIThread.Post(() => _ = RefreshAsync());

    /// <summary>Reads the folder off the calling thread and draws what it holds. A listing overtaken by a newer one is dropped.</summary>
    internal async Task RefreshAsync()
    {
        if (_host.Folder is not { } folder)
        {
            return;
        }

        var version = ++_version;
        var listing = await Task.Run(folder.List).ConfigureAwait(true);

        if (version != _version)
        {
            return;
        }

        _listing = listing;

        if (_selected is not null && !listing.Books.Any(book => book.FileName == _selected))
        {
            _selected = null;
        }

        DrawList();
    }

    private Control FolderSlab()
    {
        var slab = new Border
        {
            Padding = new Thickness(16, 12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Typeset.Chrome(FolderLabel, 12, ThemeManager.GreyKey),
                    Typeset.Mono(_host.Folder?.Folder ?? string.Empty, 13, ThemeManager.WhiteKey, wrap: true),
                },
            },
        };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private WrapPanel DesktopTiles()
    {
        var choose = BooksKit.Tile(ChooseName, "CHOOSE A FILE", "Choose a file");
        choose.Click += (_, _) => _ = ChooseAsync();

        var open = BooksKit.Tile(OpenFolderName, "OPEN THE FOLDER", "Open the folder");
        open.Click += (_, _) => OpenFolder();

        return new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { choose, open } };
    }

    private void DrawList()
    {
        _refused.Children.Clear();

        foreach (var refusal in _listing.Refused)
        {
            var notice = new Notice(NoticeLevel.Warning, inline: true) { Name = RefusalName, Label = refusal.Label, Text = refusal.Text };
            _refused.Children.Add(notice);
        }

        _list.Children.Clear();

        if (_listing.Books.Count == 0)
        {
            _list.Children.Add(Typeset.Prose(NothingToAdd, 15, ThemeManager.GreyKey, wrap: true, margin: new Thickness(0, 6, 0, 6)));
        }

        foreach (var file in _listing.Books)
        {
            _list.Children.Add(Row(file));
        }

        UpdateAdd();
    }

    private void UpdateAdd() =>
        _add.IsEnabled = Gutenberg ? _chosen is not null && !_adding : _selected is not null;

    private void ShowSource(bool gutenberg)
    {
        _gutenberg!.IsVisible = gutenberg;
        _files.IsVisible = !gutenberg;

        if (gutenberg)
        {
            AskForCatalogue();
        }

        UpdateAdd();
    }

    /// <summary>Asks for the catalogue the first time Project Gutenberg is shown, and again after it could not be had.</summary>
    private void AskForCatalogue()
    {
        if (_asked || _host.Catalogue is null)
        {
            return;
        }

        _asked = true;
        _ = LoadCatalogueAsync();
    }

    private async Task LoadCatalogueAsync()
    {
        _unreachable.IsVisible = false;
        GutenbergCatalogue? catalogue = null;

        await Waiting.While(
            _retrieving,
            Retrieving,
            _wait,
            async () => catalogue = await _host.Catalogue!.GetAsync(CancellationToken.None).ConfigureAwait(true));

        _retrieving.Clear();

        if (catalogue is null)
        {
            _asked = false;
            _unreachable.IsVisible = true;
            return;
        }

        _catalogue = catalogue;
        _filters.SetLanguages(await Task.Run(catalogue.Languages).ConfigureAwait(true));

        await QueryAsync();
    }

    /// <summary>Searches the catalogue off the calling thread and draws the books found. A search overtaken by a newer one is dropped.</summary>
    private async Task QueryAsync()
    {
        if (_catalogue is not { } catalogue)
        {
            return;
        }

        var version = ++_query;
        var query = _filters.Query;
        var result = await Task.Run(() => catalogue.Search(query)).ConfigureAwait(true);

        if (version != _query)
        {
            return;
        }

        _result = result;

        if (_chosen is { } chosen && !result.Books.Any(book => book.Id == chosen))
        {
            _chosen = null;
        }

        DrawBooks();
    }

    private bool OnShelf(GutenbergBook book) => _host.Shelf.Find(BookSource.FromGutenberg(book.Id)) is not null;

    private void DrawBooks()
    {
        _books.Children.Clear();

        foreach (var book in _result.Books)
        {
            _books.Children.Add(BookRow(book));
        }

        _filters.Show(_result.Books.Count, _result.Matched);
        UpdateAdd();
    }

    private Button BookRow(GutenbergBook book)
    {
        var shelved = OnShelf(book);
        var name = ListRow.Name(new TextBlock { Text = book.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };
        var sub = string.Join(
            " · ",
            new[] { book.Author, string.Join(", ", book.Subjects).ToLowerInvariant() }.Where(part => part.Length > 0));

        if (!_big && sub.Length > 0)
        {
            main.Children.Add(ListRow.Sub(new TextBlock { Text = sub, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }));
        }

        var aside = Typeset.Mono(
            shelved ? OnYourShelf : "#" + book.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            13,
            ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center,
            margin: new Thickness(10, 0, 0, 0));
        DockPanel.SetDock(aside, Dock.Right);

        var row = new Button
        {
            Name = GutenbergRowName,
            Tag = book.Id,
            IsEnabled = !shelved,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
            Content = new DockPanel { LastChildFill = true, Children = { aside, main } },
        };
        AutomationProperties.SetName(row, book.Title);
        ListRow.Dress(row, book.Id == _chosen);
        row.Click += (_, _) =>
        {
            _chosen = book.Id;
            _fetching.Clear();
            _unreachable.IsVisible = false;
            DrawBooks();
        };

        return row;
    }

    /// <summary>Adds the chosen book and, once its first lines are on the shelf, goes back to Books with it reading.</summary>
    private async Task AddChosenAsync()
    {
        if (_adding || _result.Books.FirstOrDefault(book => book.Id == _chosen) is not { } book || OnShelf(book))
        {
            return;
        }

        _adding = true;
        _unreachable.IsVisible = false;
        bool? reading = null;

        try
        {
            await Waiting.While(
                _fetching,
                $"Fetching the first lines of {book.Title}…",
                _wait,
                async () => reading = await _host.AddAndReadAsync(book).ConfigureAwait(true),
                _add);
        }
        finally
        {
            _adding = false;
        }

        switch (reading)
        {
            case null:
                _fetching.Clear();
                _unreachable.IsVisible = true;
                break;

            case true:
                _fetching.Clear();
                _chosen = null;

                if (VisualRoot is not null)
                {
                    _nav.Back();
                }

                return;

            default:
                _chosen = null;
                _fetching.Say($"{book.Title} is on your shelf. Press it there to read it.");
                break;
        }

        DrawBooks();
    }

    /// <summary>The count a row shows in mono.</summary>
    internal static string ChaptersOf(BookFile file) => file.Chapters == 1 ? "1 PART" : $"{file.Chapters} CHAPTERS";

    private Button Row(BookFile file)
    {
        var selected = file.FileName == _selected;
        var title = file.FileTitle ?? file.FileName;
        var name = ListRow.Name(new TextBlock { Text = title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        if (!_big)
        {
            var hasAuthor = !string.IsNullOrWhiteSpace(file.Book.Author);
            var sub = file.FileTitle is null && !hasAuthor
                ? "No title or author in the file"
                : hasAuthor ? $"{file.Book.Author} · {file.FileName}" : file.FileName;

            main.Children.Add(ListRow.Sub(new TextBlock { Text = sub, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }));
        }

        var aside = Typeset.Mono(ChaptersOf(file), 13, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, margin: new Thickness(10, 0, 0, 0));
        DockPanel.SetDock(aside, Dock.Right);

        var row = new Button
        {
            Name = RowName,
            Tag = file.FileName,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
            Content = new DockPanel { LastChildFill = true, Children = { aside, main } },
        };
        AutomationProperties.SetName(row, title);
        ListRow.Dress(row, selected);
        row.Click += (_, _) =>
        {
            _selected = file.FileName;
            _line.Clear();
            DrawList();
        };

        return row;
    }

    private void AddSelected()
    {
        if (_listing.Books.FirstOrDefault(book => book.FileName == _selected) is not { } file)
        {
            return;
        }

        var title = file.FileTitle ?? file.FileName;

        if (!_host.AddAndRead(file, out var reading))
        {
            _line.Fail($"{title} could not be put on your shelf.");
            return;
        }

        _selected = null;

        if (reading)
        {
            _nav.Back();
            return;
        }

        _line.Say($"{title} is on your shelf. Press it there to read it.");
        _ = RefreshAsync();
    }

    private async Task ChooseAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage || _host.Folder is not { } folder)
        {
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a book",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Books") { Patterns = [$"*{BookFolder.TextExtension}", $"*{BookFolder.EpubExtension}"] }],
        });

        if (picked.Count == 0 || picked[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        await CopyInAsync(folder, path);
    }

    /// <summary>Copies a file into the books folder, selects it in the list and tells every page the folder changed.</summary>
    internal async Task CopyInAsync(BookFolder folder, string path)
    {
        var name = await Task.Run(() => folder.CopyIn(path)).ConfigureAwait(true);

        if (name is null)
        {
            _line.Fail($"{Path.GetFileName(path)} could not be copied into your books folder.");
            return;
        }

        _line.Clear();
        _selected = name;
        _host.NotifyFilesChanged();
        await RefreshAsync();

        if (_host.Shelf.Find(BookSource.FromFile(name)) is not null)
        {
            _line.Say($"{name} is already on your shelf.");
        }
    }

    private void OpenFolder()
    {
        if (_host.Folder is not { } folder)
        {
            return;
        }

        try
        {
            folder.Ensure();

            Process.Start(new ProcessStartInfo { FileName = folder.Folder, UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _line.Fail($"I could not open {folder.Folder}: {ex.Message}");
        }
    }
}
