using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Astrometrics › Bookmarks › Add, or › one bookmark: a place looked up as you type, a name and a note.</summary>
public sealed class BookmarkFormPage : UserControl
{
    public const string NoPlace = "That place's system could not be found.";

    public const int MaxNoteLength = 200;

    private readonly RoutingSurface _surface;
    private readonly PanelNavigator _nav;
    private readonly Bookmark? _existing;
    private readonly StackPanel _body = new() { Spacing = 20 };
    private readonly TextBox _location = Box("Location", 320);
    private readonly TextBox _name = Box("Name", 480);
    private readonly TextBox _note = Box("Note", 480);
    private readonly TextBlock _where;
    private readonly TextBlock _nameProblem;
    private readonly StackPanel _notices = new();
    private readonly Button _save;
    private readonly SystemNameSuggestions<PlaceMatch>? _suggestions;
    private BookmarkPlace? _chosen;

    public BookmarkFormPage(RoutingSurface surface, PanelNavigator nav, Bookmark? existing)
    {
        _surface = surface;
        _nav = nav;
        _existing = existing;

        _save = new Button { Content = existing is null ? "ADD" : "SAVE", Height = TypeScale.MinimumTarget };
        _where = WatchesPage.Text(string.Empty, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey);
        _nameProblem = WatchesPage.Text(string.Empty, Fonts.ProseFamily, TypeScale.Small, ThemeManager.RedKey);
        _nameProblem.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _nameProblem.IsVisible = false;

        _note.MaxLength = MaxNoteLength;
        _note.PlaceholderText = "Why this place matters (optional)";
        _name.PlaceholderText = "Left empty, the place's own name";

        if (existing is not null)
        {
            _name.Text = existing.Name;
            _note.Text = existing.Note;
            _chosen = new BookmarkPlace(
                existing.Kind, existing.Place, existing.System, existing.SystemAddress, existing.Place ?? existing.System, null);
            _location.Text = BookmarkPlaces.Label(_chosen);
        }

        if (surface.StarSystems is { } service)
        {
            _suggestions = new SystemNameSuggestions<PlaceMatch>(
                _location,
                BookmarkPlaces.Lookup(service),
                match => match.Name,
                () => true,
                match => _ = PickedAsync(match),
                surface.SuggestionPause,
                match => BookmarkPlaces.Word(match.Kind));
        }

        _location.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && _chosen is { } chosen && _location.Text != BookmarkPlaces.Label(chosen))
            {
                _chosen = null;
                Refresh();
            }
        };

        Build();
        Refresh();
    }

    private static TextBox Box(string name, double width)
    {
        var box = new TextBox { Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(box, name);

        return box;
    }

    private static Control Row(string label, Control control) =>
        new StackPanel { Spacing = 3, Children = { FormField.Label(label, FieldNeed.Optional), control } };

    private void Build()
    {
        _body.Children.Add(TitleText.Screen(_existing?.Name ?? "Add a bookmark"));

        var use = new Button { Content = "USE MY TARGET", Height = TypeScale.MinimumTarget, VerticalAlignment = VerticalAlignment.Top };
        use.Click += async (_, _) => await UseTargetAsync();

        var field = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { _location, use },
        };

        var location = new StackPanel { Spacing = 3, Children = { FormField.Label("Location", FieldNeed.Required), field } };

        if (_suggestions is not null)
        {
            location.Children.Add(_suggestions.List);
        }

        location.Children.Add(_where);
        location.Children.Add(_notices);

        _body.Children.Add(location);
        _body.Children.Add(Row("Name", _name));
        _body.Children.Add(_nameProblem);
        _body.Children.Add(Row("Note", _note));

        var cancel = new Button { Content = "CANCEL", Height = TypeScale.MinimumTarget };
        cancel.Click += (_, _) => _nav.Back();
        _save.Click += (_, _) => Save();

        _body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { cancel, _save },
        });

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
    }

    private void Refresh()
    {
        _save.IsEnabled = _chosen is not null;

        _where.Text = _chosen switch
        {
            null => string.Empty,
            { Kind: BookmarkKind.Body } place => $"Body in {place.System}",
            { Kind: BookmarkKind.Station } place => $"Station in {place.System}",
            { Kind: BookmarkKind.System } => string.Empty,
            var place => $"In {place.System}",
        };
    }

    private void Choose(BookmarkPlace place)
    {
        _chosen = place;
        _notices.Children.Clear();

        if (_suggestions is not null)
        {
            _suggestions.SetText(BookmarkPlaces.Label(place));
        }
        else
        {
            _location.Text = BookmarkPlaces.Label(place);
        }

        Refresh();
    }

    private void ShowNotice(string label, string text)
    {
        _notices.Children.Clear();
        _notices.Children.Add(new Notice(NoticeLevel.Error) { Label = label, Text = text });
    }

    private async Task PickedAsync(PlaceMatch match)
    {
        _notices.Children.Clear();

        if (match.Kind == BookmarkKind.System)
        {
            Choose(new BookmarkPlace(BookmarkKind.System, null, match.Name, match.SystemAddress, match.Name, null));

            return;
        }

        _chosen = null;
        _suggestions?.SetText(match.Name);
        Refresh();

        string? system = null;

        try
        {
            if (_surface.StarSystems is { } service)
            {
                system = (await service.ProfileAsync(match.SystemAddress, CancellationToken.None))?.Name;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            system = null;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_location.Text != match.Name)
            {
                return;
            }

            if (system is null)
            {
                ShowNotice("Not found", NoPlace);

                return;
            }

            Choose(new BookmarkPlace(match.Kind, match.Name, system, match.SystemAddress, match.Name, null));
        });
    }

    private async Task UseTargetAsync()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            _surface.Status?.Invoke(),
            _surface.Commander?.Invoke()?.Location,
            _surface.StarSystems,
            CancellationToken.None);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (place.Error is { } error)
            {
                ShowNotice("No target", error);

                return;
            }

            Choose(place);
        });
    }

    private void Save()
    {
        if (_chosen is not { } place || _surface.Bookmarks is not { } store)
        {
            return;
        }

        var fid = _surface.Commander?.Invoke()?.Identity.FrontierId;

        if (string.IsNullOrEmpty(fid))
        {
            return;
        }

        var ownName = _existing?.Name ?? string.Empty;

        var others = store.For(fid)
            .Where(other => !string.Equals(other.Name, ownName, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Name)
            .ToList();

        var taken = BookmarkValidation.Less((_surface.BookmarkPhrasesTaken ?? (() => []))(), ownName);
        var typed = (_name.Text ?? string.Empty).Trim();

        var name = typed.Length == 0
            ? BookmarkNaming.FromDestination(BookmarkPlaces.Label(place), others, taken)
            : typed;

        if (BookmarkValidation.Problem(name, others, taken) is { } problem)
        {
            _nameProblem.Text = problem;
            _nameProblem.IsVisible = true;

            return;
        }

        _nameProblem.IsVisible = false;

        var note = (_note.Text ?? string.Empty).Trim();

        var bookmark = new Bookmark(
            name,
            place.System!,
            _existing?.MadeAt ?? DateTimeOffset.Now,
            place.Kind,
            place.Place,
            place.SystemAddress,
            note.Length == 0 ? null : note);

        var saved = _existing is null ? store.Add(fid, bookmark) : store.Update(fid, ownName, bookmark);

        if (!saved)
        {
            _nameProblem.Text = $"There is already a bookmark called \"{name}\".";
            _nameProblem.IsVisible = true;

            return;
        }

        _surface.Reply.Text = _existing is null
            ? $"Added the bookmark \"{name}\"."
            : $"Saved the bookmark \"{name}\".";

        _nav.Back();
    }
}
