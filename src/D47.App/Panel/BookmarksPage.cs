using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The systems a Commander has named: adding one from the Elite target, renaming and deleting (#488, #489, #490, #540).</summary>
public sealed class BookmarksPage : UserControl
{
    /// <summary>The Routing tab's newest root.</summary>
    public const string RootKey = RoutingPages.BookmarksRoot;

    private const string RenameKey = "routing.bookmarks.rename";
    private const string AddKey = "routing.bookmarks.add";

    private readonly BookmarkStore _store;
    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<IReadOnlyCollection<string>> _takenPhrases;
    private readonly PanelPrompts _prompts;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly CapabilityRegistry? _registry;
    private string? _reply;

    private readonly StackPanel _body = new();

    public BookmarksPage(
        BookmarkStore store,
        Func<CommanderGameState?> commander,
        Func<IReadOnlyCollection<string>> takenPhrases,
        PanelPrompts prompts,
        Func<string, Task<bool>>? copy = null,
        CapabilityRegistry? registry = null)
    {
        _copy = copy;
        _registry = registry;
        _store = store;
        _commander = commander;
        _takenPhrases = takenPhrases;
        _prompts = prompts;

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _body,
        };

        Build();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _store.Changed += Refresh;
        Build();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _store.Changed -= Refresh;
    }

    /// <summary>Redraws after a rename or a delete, from the panel or from a bookmark made out loud.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _body.Children.Clear();

        var fid = _commander()?.Identity.FrontierId;

        if (string.IsNullOrEmpty(fid))
        {
            _body.Children.Add(RoutingKit.Section("Nobody is flying"));
            _body.Children.Add(Text(BookmarksCapability.NoCommander, TypeScale.Body, ThemeManager.GreyKey, wrap: true));
            return;
        }

        var bookmarks = _store.For(fid);

        if (bookmarks.Count == 0)
        {
            _body.Children.Add(RoutingKit.Section("No bookmarks yet"));

            var howTo = _registry is null
                ? BookmarksCapability.HowToMakeOne
                : $"{BookmarksCapability.HowToMakeOne} Or press Add.";

            _body.Children.Add(Text(howTo, TypeScale.Body, ThemeManager.GreyKey, wrap: true));
            AddAddControls(fid);
            return;
        }

        AddAddControls(fid);

        var rows = new StackPanel { Spacing = 2 };

        foreach (var bookmark in bookmarks)
        {
            rows.Children.Add(Row(fid, bookmark));
        }

        _body.Children.Add(RoutingKit.Section("Your bookmarks"));
        _body.Children.Add(rows);
    }

    private void AddAddControls(string frontierId)
    {
        if (_registry is null)
        {
            return;
        }

        var add = new Button
        {
            Content = "Add",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
        };

        add.Click += (_, _) => Add(frontierId);
        _body.Children.Add(add);

        if (_reply is { } reply)
        {
            _body.Children.Add(Text(reply, TypeScale.Body, ThemeManager.GreyKey, wrap: true));
        }
    }

    /// <summary>Asks for a name, empty meaning Elite's, then bookmarks the current target through the tool.</summary>
    private void Add(string frontierId)
    {
        _prompts.Enter(
            new EntryRequest(
                AddKey,
                "Add",
                "Bookmark the target",
                "Bookmarks what you have targeted in Elite. Leave the name empty to use Elite's name for it.",
                string.Empty,
                EntrySurface.Voice,
                value => value.Trim().Length == 0 ? EntryVerdict.Ok : Validate(frontierId, string.Empty, value)),
            value => _ = AddTargetAsync(value.Trim()));
    }

    private async Task AddTargetAsync(string name)
    {
        var arguments = name.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = name };

        var result = await _registry!.InvokeAsync(
            BookmarksCapability.BookmarkTool, new ToolArguments(arguments), CancellationToken.None);

        _reply = result.Spoken;
        Dispatcher.UIThread.Post(Build);
    }

    private Control Row(string frontierId, Bookmark bookmark)
    {
        var rename = new Button
        {
            Content = "Rename",
            VerticalAlignment = VerticalAlignment.Center,
        };

        rename.Click += (_, _) =>
        {
            _reply = null;
            Rename(frontierId, bookmark);
        };

        var delete = new Button
        {
            Content = "Delete",
            Classes = { "destructive" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        delete.Click += (_, _) => _store.Delete(frontierId, bookmark.Name);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Children = { rename, delete },
        };

        var words = new WrapPanel
        {
            ItemSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ListRow.Name(Text(bookmark.Name, TypeScale.Body, null)),
                ListRow.Sub(Text(bookmark.System, TypeScale.Body, null)),
                Text(
                    bookmark.MadeAt.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
                    TypeScale.Secondary,
                    ThemeManager.GreyKey),
            },
        };

        if (_copy is { } copy)
        {
            words.Children.Insert(2, CopyGlyph.For(bookmark.System, copy));
        }

        var row = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(words);

        return ListRow.Dress(new Border { Child = row });
    }

    /// <summary>Opens the rename prompt, refused when the new name collides with a bookmark or a taken phrase.</summary>
    private void Rename(string frontierId, Bookmark bookmark)
    {
        _prompts.Enter(
            new EntryRequest(
                RenameKey,
                "Rename",
                "Rename this bookmark",
                $"\"{bookmark.Name}\" points at {bookmark.System}. The system it points at does not change.",
                bookmark.Name,
                EntrySurface.Voice,
                value => Validate(frontierId, bookmark.Name, value)),
            value => _store.Rename(frontierId, bookmark.Name, value));
    }

    private EntryVerdict Validate(string frontierId, string ownName, string newName)
    {
        var existingNames = _store.For(frontierId)
            .Where(other => !string.Equals(other.Name, ownName, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Name)
            .ToList();

        var taken = BookmarkValidation.Less(_takenPhrases(), ownName);

        return BookmarkValidation.Problem(newName, existingNames, taken) is { } problem
            ? EntryVerdict.No(problem)
            : EntryVerdict.Ok;
    }

    private static TextBlock Text(string text, double size, string? colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (colourKey is not null)
        {
            block.Bind(
                TextBlock.ForegroundProperty,
                Application.Current!.Resources.GetResourceObservable(colourKey));
        }

        return block;
    }
}
