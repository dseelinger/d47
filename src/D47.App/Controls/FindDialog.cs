using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Controls;

/// <summary>The Find box: a field and up to <see cref="MostResults"/> ranked results, drawn at the top of the window (#1474).</summary>
public sealed class FindDialog : ModalDialog
{
    public const int MostResults = 8;

    public const string FieldName = "FindField";

    public const string HintName = "FindHint";

    public const string FooterName = "FindFooter";

    public const string ResultPrefix = "FindResult.";

    private readonly Func<string, IReadOnlyList<PanelFindResult>> _search;
    private readonly Action<PanelFindResult> _open;
    private readonly string _keyLabel;

    private readonly TextBox _field;
    private readonly TextBlock _hint;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBlock _help = Mono();
    private readonly TextBlock _count = Mono();

    private IReadOnlyList<PanelFindResult> _shown = [];
    private int _selected;

    public FindDialog(Func<string, IReadOnlyList<PanelFindResult>> search, Action<PanelFindResult> open, string? keyLabel)
    {
        _search = search;
        _open = open;
        _keyLabel = (keyLabel ?? string.Empty).ToUpperInvariant();

        Title = "Find";
        Width = Modal.Width;

        _field = new TextBox
        {
            Name = FieldName,
            Height = TypeScale.MinimumTarget,
            VerticalContentAlignment = VerticalAlignment.Center,
            PlaceholderText = "A page, a setting or a help page",
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
            Margin = new Thickness(0, 0, 10, 0),
        };
        Themed(prefix, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var fieldRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        fieldRow.Children.Add(prefix);
        Grid.SetColumn(_field, 1);
        fieldRow.Children.Add(_field);

        _hint = new TextBlock
        {
            Name = HintName,
            Text = "No page, setting or help page matches that.",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            IsVisible = false,
        };
        Themed(_hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

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
        Themed(footer, Border.BorderBrushProperty, ThemeManager.Line2Key);

        var body = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(Modal.Inset - 4),
            Children = { fieldRow, _hint, _list, footer },
        };

        var surface = new Border { Child = body };
        Themed(surface, Border.BackgroundProperty, ThemeManager.BarKey);
        Content = surface;

        Refresh();

        Opened += (_, _) => _field.Focus();
    }

    public override bool AnchoredToTop => true;

    /// <summary>The results drawn, best first.</summary>
    internal IReadOnlyList<PanelFindResult> Shown => _shown;

    internal int SelectedIndex => _selected;

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
        _shown = [.. _search(_field.Text ?? string.Empty).Take(MostResults)];
        _selected = 0;

        var typed = !string.IsNullOrWhiteSpace(_field.Text);

        _hint.IsVisible = typed && _shown.Count == 0;
        _help.Text = typed ? (_shown.Count == 0 ? "ESC CLOSE" : "↑ ↓ MOVE · ENTER OPEN · ESC CLOSE") : "ESC CLOSE";
        _count.Text = typed ? $"{_shown.Count} {(_shown.Count == 1 ? "RESULT" : "RESULTS")}" : _keyLabel;

        _list.Children.Clear();

        for (var i = 0; i < _shown.Count; i++)
        {
            _list.Children.Add(Row(i));
        }

        Paint();
    }

    private Border Row(int index)
    {
        var result = _shown[index];

        var title = new TextBlock { Text = result.Title, FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Tip, FontWeight = FontWeight.Medium };
        var path = new TextBlock
        {
            Text = result.Path,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Small,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
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

            Themed(row, Border.BackgroundProperty, selected ? ThemeManager.AKey : ThemeManager.SlabKey);
            Themed(title, TextBlock.ForegroundProperty, selected ? ThemeManager.KnockKey : ThemeManager.WhiteKey);
            Themed(path, TextBlock.ForegroundProperty, selected ? ThemeManager.BrownKey : ThemeManager.AKey);
            Themed(kind, TextBlock.ForegroundProperty, selected ? ThemeManager.BrownKey : ThemeManager.GreyKey);
        }
    }

    private static string KindWord(PanelFindKind kind) => kind switch
    {
        PanelFindKind.Page => "PAGE",
        PanelFindKind.SettingsPlace => "PLACE",
        PanelFindKind.SettingRow => "SETTING",
        _ => "HELP",
    };

    private static TextBlock Mono()
    {
        var block = new TextBlock { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
