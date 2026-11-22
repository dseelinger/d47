using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Viewing;

namespace D47.App.Panel;

/// <summary>Media › Movies: find a video or add a file, carry on watching and Your list.</summary>
public sealed class WatchPage : UserControl, IPageSummary, IPageChrome, IFilterablePage, ISubmitSearchPage
{
    public const string RootKey = MediaFurnishing.MoviesRoot;

    public const string CatalogueKey = "media-movies.catalogue";

    public const string PageSummary = "Free films and footage to watch on the panels, with D47's captions.";

    public const string FindName = "WatchFind";

    public const string AddFileName = "WatchAddFile";

    public const string RowName = "WatchRow";

    public const string CarryOnHead = "Carry on watching";

    public const string YourListHead = "Your list";

    public const string Note = "Videos play from their source; only your place is kept on this computer.";

    public const string NothingYet = "Nothing yet. Find a video, or add a file from this computer.";

    private readonly WatchHost _host;
    private readonly PanelNavigator _nav;
    private readonly bool _big;
    private readonly StackPanel _body = new() { Spacing = 24, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _lists = new() { Spacing = 24 };
    private readonly Button _find = Tile(FindName, "FIND A VIDEO", "Find a video");
    private readonly Button _addFile = Tile(AddFileName, "ADD A FILE FROM THIS COMPUTER", "Add a file from this computer");

    public WatchPage(WatchHost host, PanelNavigator nav, Control? settingsStrip = null, bool big = false)
    {
        _host = host;
        _nav = nav;
        _big = big;
        BarTool = PageChrome.ToolOf(settingsStrip);

        _body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { _find, _addFile },
        });
        _body.Children.Add(_lists);

        _find.Click += (_, _) => OpenCatalogue();
        _addFile.Click += async (_, _) => await AddFileAsync();

        var root = new DockPanel { Margin = new Thickness(14) };

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

        root.Children.Add(new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = root;
        Draw();
    }

    /// <summary>Replaces the file chooser; the panel's own chooser when null.</summary>
    internal Func<Task<string?>>? ChooseFile { get; set; }

    public Control? BarTool { get; }

    public string? HelpTopic => InterfaceCapability.Id;

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => !_big;

    public string FilterPlaceholder => "Search the catalogue";

    public void Filter(string? query)
    {
    }

    public bool Submit(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !_host.HasCatalogue)
        {
            return false;
        }

        _host.CatalogueQuery = query.Trim();

        return _nav.Drill(CatalogueCrumb);
    }

    /// <summary>The crumb of the catalogue page, one level below Movies.</summary>
    public static NavCrumb CatalogueCrumb { get; } = new(CatalogueKey, "Catalogue") { Help = InterfaceCapability.Id, Whole = true };

    /// <summary>Opens the player page for <paramref name="title"/> directly below Movies, however the Commander got there.</summary>
    public static bool OpenPlayer(PanelNavigator nav, WatchHost host, VideoTitle title, bool start, bool headset = false)
    {
        host.Open(title);

        if (start)
        {
            host.PlaysHere(headset);
            host.Play(title);
        }

        nav.SelectRoot(PanelTab.Media, RootKey);

        return nav.GoTo(PanelTab.Media, VideoPlayerPage.CrumbOf(title));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Changed += OnChanged;
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Draw);

    private void OpenCatalogue()
    {
        if (_host.HasCatalogue)
        {
            _host.CatalogueQuery = null;
            _nav.Drill(CatalogueCrumb);
        }
    }

    private async Task AddFileAsync()
    {
        var path = ChooseFile is { } choose ? await choose() : await PickAsync();

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var title = _host.FileTitle(path);

        _host.Store.Add(title.Source, title.Id, DateTimeOffset.UtcNow, title);
        OpenPlayer(_nav, _host, title, start: true, headset: _big);
    }

    private async Task<string?> PickAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add a video file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video files") { Patterns = ["*.mp4", "*.m4v", "*.mov", "*.wmv", "*.avi", "*.mkv"] },
            ],
        });

        return picked.Count == 1 ? picked[0].TryGetLocalPath() : null;
    }

    private void Draw()
    {
        _find.IsEnabled = _host.HasCatalogue;
        _addFile.IsEnabled = !_big;

        _lists.Children.Clear();

        var carryOn = _host.CarryOn();
        var list = _host.YourList();

        if (carryOn.Count > 0)
        {
            _lists.Children.Add(Rows(CarryOnHead, carryOn, carryingOn: true));
        }

        if (list.Count > 0)
        {
            _lists.Children.Add(Rows(YourListHead, list, carryingOn: false));
        }

        if (carryOn.Count == 0 && list.Count == 0)
        {
            _lists.Children.Add(Typeset.Prose(NothingYet, 15, ThemeManager.GreyKey, wrap: true));
        }

        _lists.Children.Add(Typeset.Prose(Note, 15, ThemeManager.GreyKey, wrap: true));
    }

    private StackPanel Rows(string head, IReadOnlyList<(WatchEntry Entry, VideoTitle Title)> rows, bool carryingOn)
    {
        var column = new StackPanel { Spacing = Gaps.Tile };
        AutomationProperties.SetName(column, head);
        column.Children.Add(ListRow.Head(head));

        foreach (var (entry, title) in rows)
        {
            var main = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    ListRow.Name(new TextBlock { Text = title.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }),
                    ListRow.Sub(new TextBlock
                    {
                        Text = $"{WatchHost.SourceOf(title)} · {WatchHost.CaptionsOf(title)}",
                        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    }),
                },
            };

            var aside = ListRow.Aside(new TextBlock
            {
                Text = Reading(entry, title, carryingOn),
                FontFamily = new Avalonia.Media.FontFamily(Fonts.MonoFamily),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            });
            DockPanel.SetDock(aside, Dock.Right);

            var row = new Button
            {
                Name = RowName,
                Tag = $"{title.Source}.{title.Id}",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = TypeScale.MinimumTarget,
                Content = new DockPanel { LastChildFill = true, Children = { aside, main } },
            };
            AutomationProperties.SetName(row, title.Title);
            ListRow.Dress(row);
            row.Click += (_, _) => OpenPlayer(_nav, _host, title, start: title.Playable, headset: _big);
            column.Children.Add(row);
        }

        return column;
    }

    /// <summary>The place and the length for a title being carried on, the length for one in the list.</summary>
    internal static string Reading(WatchEntry entry, VideoTitle title, bool carryingOn) =>
        carryingOn
            ? title.Length is { } length ? $"{Clock(entry.Place)} of {Clock(length)}" : Clock(entry.Place)
            : title.Length is { } only ? Clock(only) : string.Empty;

    /// <summary>A time as <c>hh:mm:ss</c>.</summary>
    internal static string Clock(TimeSpan time) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}");

    private static Button Tile(string name, string text, string accessibleName)
    {
        var tile = new Button
        {
            Name = name,
            Content = text,
            MinHeight = TypeScale.MinimumTarget,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(tile, accessibleName);

        return tile;
    }
}
