using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Books;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Media › Books › Add a book: the .txt and .epub files in the books folder that are not on the shelf.</summary>
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

    private readonly BooksHost _host;
    private readonly PanelNavigator _nav;
    private readonly bool _big;
    private readonly StackPanel _refused = new() { Spacing = 8 };
    private readonly StackPanel _list = new() { Spacing = Gaps.Tile };
    private readonly StatusLine _line = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _add = BooksKit.Tile(AddName, "ADD AND READ", "Add the book and read it");

    private BookFolderListing _listing = BookFolderListing.Empty;
    private string? _selected;
    private int _version;

    public BookAddPage(BooksHost host, PanelNavigator nav, bool big = false)
    {
        _host = host;
        _nav = nav;
        _big = big;

        var body = new StackPanel { Spacing = big ? 8 : 12, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };

        if (!_big)
        {
            body.Children.Add(Typeset.Prose(HelpText, 15, ThemeManager.GreyKey, wrap: true));
        }

        body.Children.Add(FolderSlab());

        if (!_big)
        {
            body.Children.Add(DesktopTiles());
        }

        body.Children.Add(_refused);
        body.Children.Add(_list);
        body.Children.Add(_line);

        _add.IsEnabled = false;
        _add.MinWidth = 120;
        _add.HorizontalAlignment = HorizontalAlignment.Stretch;
        _add.Click += (_, _) => AddSelected();

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

        _add.IsEnabled = _selected is not null;
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
