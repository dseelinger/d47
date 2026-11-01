using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Controls;

/// <summary>
/// Where Find asks the galaxy for names: whether to, the request, and how its answer is ranked with the local results.
/// The pause is how long typing stops before the request is sent.
/// </summary>
public sealed record GalaxyFind(
    Func<bool> Enabled,
    Func<string, CancellationToken, Task<IReadOnlyList<PanelFindName>>> Ask,
    Func<string, IReadOnlyList<PanelFindName>, IReadOnlyList<PanelFindResult>> Rank)
{
    public TimeSpan Pause { get; init; } = NameSuggestions.DefaultPause;
}

/// <summary>The Find box: a field and up to <see cref="MostResults"/> ranked results, drawn at the top of the window (#1474).</summary>
public sealed class FindDialog : ModalDialog
{
    public const int MostResults = 8;

    public const string FieldName = "FindField";

    public const string HintName = "FindHint";

    public const string FooterName = "FindFooter";

    public const string ResultPrefix = "FindResult.";

    public const string AskingMark = " · ASKING SPANSH";

    private readonly Func<string, IReadOnlyList<PanelFindResult>> _search;
    private readonly Action<PanelFindResult> _open;
    private readonly string _keyLabel;

    private readonly TextBox _field;
    private readonly TextBlock _hint;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBlock _help = Mono();
    private readonly TextBlock _count = Mono();

    private readonly GalaxyFind? _galaxy;
    private CancellationTokenSource? _asking;
    private int _generation;

    private IReadOnlyList<PanelFindResult> _shown = [];
    private int _selected;
    private bool _requestOut;

    public FindDialog(
        Func<string, IReadOnlyList<PanelFindResult>> search, Action<PanelFindResult> open, string? keyLabel, GalaxyFind? galaxy = null)
    {
        _search = search;
        _galaxy = galaxy;
        _open = open;
        _keyLabel = (keyLabel ?? string.Empty).ToUpperInvariant();

        Title = "Find";
        Width = Modal.Width;

        _field = new TextBox
        {
            Name = FieldName,
            Height = TypeScale.MinimumTarget,
            VerticalContentAlignment = VerticalAlignment.Center,
            PlaceholderText = "A page, a setting, a help page or a name",
        };
        Avalonia.Automation.AutomationProperties.SetName(_field, "Find");
        _field.TextChanged += (_, _) => Refresh();
        _field.AddHandler(KeyDownEvent, OnFieldKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        var prefix = new TextBlock
        {
            Text = "FIND:",
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0),
        };
        ThemeInk.Bind(prefix, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _field.InnerLeftContent = prefix;

        _hint = new TextBlock
        {
            Name = HintName,
            Text = "Nothing matches that.",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            IsVisible = false,
        };
        ThemeInk.Bind(_hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _count.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_count, 1);
        var footerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        footerRow.Children.Add(_help);
        footerRow.Children.Add(_count);

        var footer = new Border
        {
            Name = FooterName,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 0, 0),
            Child = footerRow,
        };
        ThemeInk.Bind(footer, Border.BorderBrushProperty, ThemeManager.Line2Key);

        var body = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(Modal.Inset - 4),
            Children = { _field, _hint, _list, footer },
        };

        var surface = new Border { Child = body };
        ThemeInk.Bind(surface, Border.BackgroundProperty, ThemeManager.BarKey);
        Content = surface;

        Refresh();

        Opened += (_, _) => _field.Focus();
        Closed += (_, _) => Cancel();
    }

    public override bool AnchoredToTop => true;

    /// <summary>The results drawn, best first.</summary>
    internal IReadOnlyList<PanelFindResult> Shown => _shown;

    internal int SelectedIndex => _selected;

    /// <summary>The footer's count, such as "5 RESULTS · ASKING SPANSH".</summary>
    internal string? Count => _count.Text;

    internal bool HintShown => _hint.IsVisible;

    /// <summary>The path line of the row at <paramref name="index"/>, run by run.</summary>
    internal IReadOnlyList<Run> PathRuns(int index) =>
        _list.Children[index] is Border { Tag: (TextBlock _, TextBlock path, TextBlock _) }
            ? [.. path.Inlines!.OfType<Run>()]
            : [];

    internal string Query
    {
        get => _field.Text ?? string.Empty;
        set => _field.Text = value;
    }

    /// <summary>Moves the selection by <paramref name="step"/>, wrapping at either end.</summary>
    internal void Move(int step)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        _selected = (_selected + step + _shown.Count) % _shown.Count;
        Paint();
    }

    /// <summary>Closes the box and opens the selected result, if there is one.</summary>
    internal void OpenSelected()
    {
        if (_selected >= _shown.Count)
        {
            return;
        }

        Choose(_shown[_selected]);
    }

    private void Choose(PanelFindResult result)
    {
        Close();
        _open(result);
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                Move(1);
                break;

            case Key.Up:
                e.Handled = true;
                Move(-1);
                break;

            case Key.Enter:
                e.Handled = true;
                OpenSelected();
                break;
        }
    }

    private void Refresh()
    {
        var text = _field.Text ?? string.Empty;

        Cancel();
        _shown = [.. _search(text).Take(MostResults)];
        _selected = 0;

        Redraw();
        AskGalaxy(text);
    }

    private void Redraw()
    {
        var typed = !string.IsNullOrWhiteSpace(_field.Text);

        _hint.IsVisible = typed && _shown.Count == 0 && !_requestOut;
        _help.Text = typed ? (_shown.Count == 0 ? "ESC CLOSE" : "↑ ↓ MOVE · ENTER OPEN · ESC CLOSE") : "ESC CLOSE";
        _count.Text = typed
            ? $"{_shown.Count} {(_shown.Count == 1 ? "RESULT" : "RESULTS")}{(_requestOut ? AskingMark : string.Empty)}"
            : _keyLabel;

        _list.Children.Clear();

        for (var i = 0; i < _shown.Count; i++)
        {
            _list.Children.Add(Row(i));
        }

        Paint();
    }

    private void Cancel()
    {
        _generation++;
        _requestOut = false;
        _asking?.Cancel();
        _asking?.Dispose();
        _asking = null;
    }

    private async void AskGalaxy(string text)
    {
        var query = text.Trim();

        if (_galaxy is not { } galaxy || query.Length < NameSuggestions.MinimumLength || !galaxy.Enabled())
        {
            return;
        }

        var generation = _generation;
        var source = _asking = new CancellationTokenSource();

        try
        {
            if (galaxy.Pause > TimeSpan.Zero)
            {
                await Task.Delay(galaxy.Pause, source.Token);
            }

            if (generation != _generation)
            {
                return;
            }

            _requestOut = true;
            Redraw();

            var names = await galaxy.Ask(query, source.Token);

            if (generation != _generation)
            {
                return;
            }

            _requestOut = false;

            var kept = _selected < _shown.Count ? _shown[_selected] : null;

            _shown = [.. galaxy.Rank(query, names).Take(MostResults)];
            _selected = kept is null ? 0 : Math.Max(0, IndexOf(kept));
            Redraw();
        }
        catch (Exception)
        {
            if (generation == _generation && _requestOut)
            {
                _requestOut = false;
                Redraw();
            }
        }
    }

    private int IndexOf(PanelFindResult kept)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            if (_shown[i].Kind == kept.Kind && _shown[i].Target == kept.Target)
            {
                return i;
            }
        }

        return -1;
    }

    private Border Row(int index)
    {
        var result = _shown[index];

        var title = new TextBlock { Text = result.Title, FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Tip, FontWeight = FontWeight.Medium };
        var path = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Small,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        path.Inlines!.Add(new Run(result.Path));

        if (result.LightYears is { } lightYears)
        {
            path.Inlines.Add(new Run(" · "));
            path.Inlines.Add(new Run(lightYears.ToString("N1", CultureInfo.InvariantCulture) + " LY") { FontFamily = new FontFamily(Fonts.MonoFamily) });
        }
        var kind = new TextBlock
        {
            Text = KindWord(result.Kind),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Caption * 0.06,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        DockPanel.SetDock(kind, Dock.Right);

        var row = new Border
        {
            Name = ResultPrefix + index,
            Padding = new Thickness(12, 4),
            Tag = (title, path, kind),
            Child = new DockPanel { Children = { kind, new StackPanel { Children = { title, path } } } },
        };
        row.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Choose(result);
        };
        row.PointerEntered += (_, _) =>
        {
            _selected = index;
            Paint();
        };

        return row;
    }

    private void Paint()
    {
        for (var i = 0; i < _list.Children.Count; i++)
        {
            if (_list.Children[i] is not Border { Tag: (TextBlock title, TextBlock path, TextBlock kind) } row)
            {
                continue;
            }

            var selected = i == _selected;

            ThemeInk.Bind(row, Border.BackgroundProperty, selected ? ThemeManager.AKey : ThemeManager.TileKey);
            ThemeInk.Bind(title, TextBlock.ForegroundProperty, selected ? ThemeManager.KnockKey : ThemeManager.WhiteKey);
            ThemeInk.Bind(path, TextBlock.ForegroundProperty, selected ? ThemeManager.BrownKey : ThemeManager.AKey);
            ThemeInk.Bind(kind, TextBlock.ForegroundProperty, selected ? ThemeManager.BrownKey : ThemeManager.GreyKey);
        }
    }

    private static string KindWord(PanelFindKind kind) => kind switch
    {
        PanelFindKind.Page => "PAGE",
        PanelFindKind.SettingsPlace => "PLACE",
        PanelFindKind.SettingRow => "SETTING",
        PanelFindKind.StarSystem => "SYSTEM",
        PanelFindKind.Station => "STATION",
        PanelFindKind.Carrier => "CARRIER",
        PanelFindKind.Faction => "FACTION",
        PanelFindKind.Engineer => "ENGINEER",
        PanelFindKind.Bookmark => "BOOKMARK",
        _ => "HELP",
    };

    private static TextBlock Mono()
    {
        var block = new TextBlock { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

}
