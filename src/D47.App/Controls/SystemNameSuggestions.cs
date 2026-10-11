using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Controls;

/// <summary>
/// Lists the system names that start with what is typed into a field, inline in <see cref="List"/>, which the
/// host places under the field. Up and Down move through the list, Enter picks the highlighted name, Escape
/// closes it, and clicking a name picks it. Enter with nothing highlighted is left to the field's own handler.
/// The host sets the field's text through <see cref="SetText"/>, so its own text asks for nothing.
/// </summary>
public sealed class SystemNameSuggestions
{
    /// <summary>The most names listed.</summary>
    public const int Most = 8;

    /// <summary>Marks the list, for a test to find it.</summary>
    public const string ListName = "SystemNameSuggestions";

    private readonly TextBox _field;
    private readonly SystemNameSuggester _suggester;
    private readonly Action<SystemNameMatch> _picked;
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly List<IDisposable> _inks = [];

    private IReadOnlyList<SystemNameMatch> _shown = [];
    private int _highlighted = -1;
    private bool _quiet;

    public SystemNameSuggestions(
        TextBox field,
        Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>> lookup,
        Func<bool> enabled,
        Action<SystemNameMatch> picked,
        TimeSpan? pause = null)
    {
        _field = field;
        _picked = picked;

        _suggester = new SystemNameSuggester(
            (typed, token) => Task.Run(() => lookup(typed, token), token),
            enabled,
            action => Dispatcher.UIThread.Post(action),
            pause ?? SystemNameSuggester.DefaultPause);
        _suggester.Answered += Show;

        List = new Border
        {
            Name = ListName,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            IsVisible = false,
            Child = _rows,
        };
        Ink(List, Border.BackgroundProperty, ThemeManager.BarKey);
        Ink(List, Border.BorderBrushProperty, ThemeManager.AKey);

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
    public IReadOnlyList<SystemNameMatch> Shown => _shown;

    /// <summary>The highlighted name's place in <see cref="Shown"/>, or -1 for none.</summary>
    public int Highlighted => _highlighted;

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

    /// <summary>Drops any pending request and closes the list.</summary>
    public void Close()
    {
        _suggester.Cancel();
        Show([]);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _highlighted < 0)
        {
            Close();
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

            case Key.Escape:
                Close();
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void Pick(SystemNameMatch match)
    {
        Close();
        _picked(match);
    }

    private void Show(IReadOnlyList<SystemNameMatch> matches)
    {
        if (matches.Count == 0 && _shown.Count == 0)
        {
            return;
        }

        _shown = [.. matches.Take(Most)];
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

        List.IsVisible = _shown.Count > 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Button Row(SystemNameMatch match, bool highlighted)
    {
        var name = new TextBlock
        {
            Text = match.Name,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.ControlLarge,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // The highlighted row takes the selected row's ink from the button.
        if (!highlighted)
        {
            _inks.Add(Ink(name, TextBlock.ForegroundProperty, ThemeManager.AKey));
        }

        var row = new Button
        {
            Content = name,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ListRow.Dress(row, highlighted);
        AutomationProperties.SetName(row, match.Name);
        row.Click += (_, _) => Pick(match);

        return row;
    }

    private static IDisposable Ink(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
