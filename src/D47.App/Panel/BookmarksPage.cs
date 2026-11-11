using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Navigation › Bookmarks: the places a Commander has named, with a note on each.</summary>
public sealed class BookmarksPage : UserControl
{
    public const string RootKey = RoutingPages.BookmarksRoot;

    public const string AddKey = "routing.bookmarks.add";

    public const string EditPrefix = "routing.bookmarks.edit.";

    public const string Summary = "Places you have named, newest first. Say “set course for” and the name.";

    public const string AddHint = "Look up a system, body or station as you type, or start from your Elite target.";

    public const string NoNote = "No note. Edit to say why this place matters.";

    private readonly RoutingSurface _surface;
    private readonly PanelNavigator _nav;
    private readonly StackPanel _body = new() { Spacing = 20 };

    public BookmarksPage(RoutingSurface surface, PanelNavigator nav)
    {
        _surface = surface;
        _nav = nav;

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children =
            {
                LoadoutPages.Scrolling(new Grid
                {
                    ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 900 }],
                    Children = { _body },
                }),
            },
        };

        Build();
    }

    /// <summary>The key of the page that edits <paramref name="bookmark"/>.</summary>
    public static string EditKey(Bookmark bookmark) => EditPrefix + bookmark.Name;

    /// <summary>The name of the bookmark an edit page's key names, or null.</summary>
    public static string? EditOf(string key) =>
        key.StartsWith(EditPrefix, StringComparison.Ordinal) ? key[EditPrefix.Length..] : null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _surface.Bookmarks!.Changed += Refresh;
        Build();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _surface.Bookmarks!.Changed -= Refresh;
    }

    /// <summary>Redraws after a change to the bookmarks, from the panel or from a bookmark made out loud.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _body.Children.Clear();
        _body.Children.Add(TitleText.Screen("Bookmarks"));
        _body.Children.Add(RoutingKit.Prose(Summary, TypeScale.Body));

        var fid = _surface.Commander?.Invoke()?.Identity.FrontierId;

        if (string.IsNullOrEmpty(fid))
        {
            _body.Children.Add(RoutingKit.Section("Nobody is flying"));
            _body.Children.Add(RoutingKit.Prose(BookmarksCapability.NoCommander, TypeScale.Body));

            return;
        }

        var bookmarks = _surface.Bookmarks!.For(fid);

        if (_surface.Registry is not null)
        {
            _body.Children.Add(AddBlock());
        }

        if (bookmarks.Count == 0)
        {
            _body.Children.Add(RoutingKit.Section("No bookmarks yet"));

            _body.Children.Add(RoutingKit.Prose(
                _surface.Registry is null
                    ? BookmarksCapability.HowToMakeOne
                    : $"{BookmarksCapability.HowToMakeOne} Or press Add.",
                TypeScale.Body));

            return;
        }

        var rows = new StackPanel { Spacing = 2 };

        foreach (var bookmark in bookmarks)
        {
            rows.Children.Add(Row(fid, bookmark));
        }

        _body.Children.Add(RoutingKit.Section("Your bookmarks"));
        _body.Children.Add(rows);
    }

    private Control AddBlock()
    {
        var block = new StackPanel { Spacing = 8 };

        var add = Tile("ADD", "Add a bookmark");
        add.Click += (_, _) =>
        {
            _surface.Reply.Text = null;
            _nav.Drill(Crumb(AddKey, "Add"));
        };

        var hint = RoutingKit.Prose(AddHint, TypeScale.Small);
        hint.VerticalAlignment = VerticalAlignment.Center;

        block.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { add, hint },
        });

        if (_surface.Reply.Text is { Length: > 0 } reply)
        {
            block.Children.Add(RoutingKit.Prose(reply, TypeScale.Body));
        }

        return block;
    }

    private static NavCrumb Crumb(string key, string word) =>
        new(key, word) { Help = BookmarksCapability.Id, Whole = true };

    private static Button Tile(string content, string name)
    {
        var tile = new Button { Content = content, Height = TypeScale.MinimumTarget };
        AutomationProperties.SetName(tile, name);

        return tile;
    }

    private Control Row(string frontierId, Bookmark bookmark)
    {
        var tiles = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };

        if (_surface.Registry is { } registry)
        {
            var course = Tile("SET COURSE", $"Set course for {bookmark.Name}");
            course.Click += (_, _) => _ = SetCourseAsync(registry, bookmark);
            tiles.Children.Add(course);
        }

        var edit = Tile("EDIT", $"Edit {bookmark.Name}");
        edit.Click += (_, _) =>
        {
            _surface.Reply.Text = null;
            _nav.Drill(Crumb(EditKey(bookmark), bookmark.Name));
        };
        tiles.Children.Add(edit);

        var delete = Tile("DELETE", $"Delete {bookmark.Name}");
        delete.Classes.Add("destructive");
        delete.Click += (_, _) => _surface.Bookmarks!.Delete(frontierId, bookmark.Name);
        tiles.Children.Add(delete);

        var first = new WrapPanel { ItemSpacing = 10 };
        first.Children.Add(ListRow.Name(WatchesPage.Text(bookmark.Name, Fonts.ChromeFamily, TypeScale.Body, ThemeManager.WhiteKey)));

        if (bookmark.Kind is { } kind)
        {
            first.Children.Add(WatchesPage.Text(
                BookmarkPlaces.Word(kind).ToUpperInvariant(), Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.GreyKey));
        }

        var place = string.IsNullOrEmpty(bookmark.Place) ? bookmark.System : $"{bookmark.Place} · {bookmark.System}";
        first.Children.Add(WatchesPage.Text(place, Fonts.ChromeFamily, TypeScale.Body, ThemeManager.AKey));

        if (RoutingPages.Copy(_surface) is { } copy)
        {
            first.Children.Add(CopyGlyph.For(bookmark.System, copy));
        }

        first.Children.Add(WatchesPage.Text(
            bookmark.MadeAt.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
            Fonts.MonoFamily,
            TypeScale.Secondary,
            ThemeManager.GreyKey));

        var note = string.IsNullOrWhiteSpace(bookmark.Note)
            ? WatchesPage.Text(NoNote, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey)
            : WatchesPage.Text(bookmark.Note, Fonts.ProseFamily, TypeScale.Small, ThemeManager.WhiteKey);
        note.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        note.TextTrimming = Avalonia.Media.TextTrimming.None;

        var words = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { first, note },
        };

        var row = new DockPanel { Margin = new Thickness(0, 4) };
        DockPanel.SetDock(tiles, Dock.Right);
        row.Children.Add(tiles);
        row.Children.Add(words);

        return ListRow.Dress(new Border { Child = row });
    }

    private async Task SetCourseAsync(CapabilityRegistry registry, Bookmark bookmark)
    {
        var result = await registry.InvokeAsync(
            "plot_course",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = bookmark.System }),
            CancellationToken.None);

        _surface.Reply.Text = result.Spoken;
        Refresh();
    }
}
