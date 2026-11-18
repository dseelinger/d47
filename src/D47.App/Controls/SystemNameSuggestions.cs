using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// Lists the names that start with what is typed into a field, inline in <see cref="List"/>, which the
/// host places under the field. Up and Down move through the list, Enter picks the highlighted name, Escape
/// closes it, and clicking a name picks it. Enter with nothing highlighted is left to the field's own handler.
/// From the keystroke that starts a lookup the list shows one row: the wait mark, then the names, "Nothing
/// matches." or a notice that the lookup failed, until the text changes.
/// The host sets the field's text through <see cref="SetText"/>, so its own text asks for nothing.
/// </summary>
public sealed class SystemNameSuggestions<T> : INameSuggestions
{
    /// <summary>The sentence beside the wait mark while names are asked for.</summary>
    public const string Retrieving = "Retrieving results…";

    /// <summary>The row shown when the lookup answers with no names.</summary>
    public const string NothingMatches = "Nothing matches.";

    /// <summary>The notice shown when the lookup fails.</summary>
    public const string CouldNotRetrieve = "Could not retrieve results.";

    /// <summary>Names the status row in the list, for a test to find it.</summary>
    public const string StatusName = "SystemNameSuggestionsStatus";

    private enum ListState
    {
        Hidden,
        Waiting,
        Names,
        Empty,
        Failed,
    }

    private readonly TextBox _field;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<T>>> _lookup;
    private readonly Func<T, string> _nameOf;
    private readonly Func<T, string?>? _tagOf;
    private readonly Func<bool> _enabled;
    private readonly TimeSpan _pause;
    private readonly SystemNameSuggester<T> _suggester;
    private readonly Action<T> _picked;
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly List<IDisposable> _inks = [];

    private IReadOnlyList<T> _shown = [];
    private ListState _state = ListState.Hidden;
    private int _highlighted = -1;
    private bool _quiet;

    public SystemNameSuggestions(
        TextBox field,
        Func<string, CancellationToken, Task<IReadOnlyList<T>>> lookup,
        Func<T, string> nameOf,
        Func<bool> enabled,
        Action<T> picked,
        TimeSpan? pause = null,
        Func<T, string?>? tagOf = null)
    {
        _field = field;
        _lookup = lookup;
        _nameOf = nameOf;
        _tagOf = tagOf;
        _enabled = enabled;
        _pause = pause ?? NameSuggestions.DefaultPause;
        _picked = picked;

        _suggester = NewSuggester();
        _suggester.Asking += () => Enter(ListState.Waiting, []);
        _suggester.Answered += matches => Enter(matches.Count == 0 ? ListState.Empty : ListState.Names, matches);
        _suggester.Failed += () => Enter(ListState.Failed, []);
        _suggester.Cleared += () => Enter(ListState.Hidden, []);
        field.SetValue(NameSuggestions.OfProperty, this);

        List = new Border
        {
            Name = NameSuggestions.ListName,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            IsVisible = false,
            Child = _rows,
        };
        ThemeInk.Bind(List, Border.BackgroundProperty, ThemeManager.BarKey);
        ThemeInk.Bind(List, Border.BorderBrushProperty, ThemeManager.AKey);

        field.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_quiet)
            {
                _ = _suggester.Typed(field.Text ?? string.Empty);
            }
        };
        field.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised when the list opens, closes or changes.</summary>
    public event EventHandler? Changed;

    /// <summary>The list; hidden while there is nothing to suggest.</summary>
    public Border List { get; }

    /// <summary>The names listed, none while the list is closed.</summary>
    public IReadOnlyList<T> Shown => _shown;

    /// <summary>Sets the field's text without asking for names, and closes the list.</summary>
    public void SetText(string text)
    {
        _quiet = true;

        try
        {
            _field.Text = text;
        }
        finally
        {
            _quiet = false;
        }

        Close();
    }

    /// <summary>A suggester with this list's lookup, setting and pause, for a surface that draws its own names.</summary>
    public SystemNameSuggester<T> NewSuggester() =>
        new(
            (typed, token) => Task.Run(() => _lookup(typed, token), token),
            _enabled,
            action => Dispatcher.UIThread.Post(action),
            _pause);

    /// <summary>Does what picking <paramref name="match"/> in the list does.</summary>
    public void Choose(T match) => Pick(match);

    /// <inheritdoc />
    public NameTiles NewTiles()
    {
        var suggester = NewSuggester();
        IReadOnlyList<T> last = [];

        void Offer(IReadOnlyList<T> matches, Action<IReadOnlyList<string>> raise)
        {
            last = [.. matches.Take(NameSuggestions.Most)];
            raise([.. last.Select(_nameOf)]);
        }

        return new NameTiles(
            suggester.Typed,
            suggester.Cancel,
            index => Choose(last[index]),
            raise =>
            {
                suggester.Answered += matches => Offer(matches, raise);
                suggester.Failed += () => Offer([], raise);
                suggester.Cleared += () => Offer([], raise);
            });
    }

    /// <summary>Drops any pending request and closes the list.</summary>
    public void Close()
    {
        _suggester.Cancel();
        Enter(ListState.Hidden, []);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _highlighted < 0)
        {
            Close();
            return;
        }

        if (e.Key == Key.Escape && _state != ListState.Hidden)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (_shown.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                Highlight(Math.Min(_highlighted + 1, _shown.Count - 1));
                break;

            case Key.Up:
                Highlight(Math.Max(_highlighted - 1, -1));
                break;

            case Key.Enter:
                Pick(_shown[_highlighted]);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void Pick(T match)
    {
        Close();
        _picked(match);
    }

    private void Enter(ListState state, IReadOnlyList<T> matches)
    {
        if (state == ListState.Hidden && _state == ListState.Hidden)
        {
            return;
        }

        _state = state;
        _shown = [.. matches.Take(NameSuggestions.Most)];
        _highlighted = -1;
        Draw();
    }

    private void Highlight(int index)
    {
        if (index == _highlighted)
        {
            return;
        }

        _highlighted = index;
        Draw();
    }

    private void Draw()
    {
        _inks.ForEach(ink => ink.Dispose());
        _inks.Clear();
        _rows.Children.Clear();

        for (var i = 0; i < _shown.Count; i++)
        {
            _rows.Children.Add(Row(_shown[i], i == _highlighted));
        }

        switch (_state)
        {
            case ListState.Waiting:
                _rows.Children.Add(StatusRow(new WaitingState(WaitForm.Line, Retrieving)));
                break;

            case ListState.Empty:
                var none = new TextBlock
                {
                    Text = NothingMatches,
                    FontFamily = new FontFamily(Fonts.ChromeFamily),
                    FontSize = TypeScale.ControlLarge,
                    FontWeight = FontWeight.Medium,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _inks.Add(ThemeInk.Bind(none, TextBlock.ForegroundProperty, ThemeManager.GreyKey));
                _rows.Children.Add(StatusRow(none));
                break;

            case ListState.Failed:
                _rows.Children.Add(StatusRow(new Notice(inline: true) { Text = CouldNotRetrieve }));
                break;
        }

        List.IsVisible = _state != ListState.Hidden;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static Border StatusRow(Control content)
    {
        content.VerticalAlignment = VerticalAlignment.Center;

        return new()
        {
            Name = StatusName,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            Child = content,
            Focusable = false,
        };
    }

    private Button Row(T match, bool highlighted)
    {
        var name = new TextBlock
        {
            Text = _nameOf(match),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.ControlLarge,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // The highlighted row takes the selected row's ink from the button.
        if (!highlighted)
        {
            _inks.Add(ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.AKey));
        }

        Control content = name;

        if (_tagOf?.Invoke(match) is { } tag)
        {
            var kind = new TextBlock
            {
                Text = tag.ToUpperInvariant(),
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.Caption,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };

            if (!highlighted)
            {
                _inks.Add(ThemeInk.Bind(kind, TextBlock.ForegroundProperty, ThemeManager.GreyKey));
            }

            Grid.SetColumn(kind, 1);
            content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { name, kind } };
        }

        var row = new Button
        {
            Content = content,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ListRow.Dress(row, highlighted);
        AutomationProperties.SetName(row, _nameOf(match));
        row.Click += (_, _) => Pick(match);

        return row;
    }

}
