using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>
/// A filter written as a sentence of tokens. A token opens a panel of its choices inside the page under the sentence;
/// the results line carries the count and Clear filters.
/// </summary>
internal abstract class TokenFilterBar<TKind> : StackPanel
    where TKind : struct, Enum
{
    public const string TokenClass = "catalogue-token";

    public const string TokenHint = "Press an orange word to change it.";

    private readonly Dictionary<TKind, Button> _tokens = [];
    private readonly Dictionary<TKind, IDisposable> _inks = [];
    private readonly string? _tokenNamePrefix;
    private Border? _panelHost;
    private TextBlock? _count;
    private Button? _clear;

    protected TokenFilterBar(string? tokenNamePrefix)
    {
        _tokenNamePrefix = tokenNamePrefix;
    }

    /// <summary>Raised after the Commander changes the filter.</summary>
    public event EventHandler? Changed;

    protected Dictionary<TKind, TextBlock> Values { get; } = [];

    protected TKind? Open { get; set; }

    /// <summary>True while <see cref="Refresh"/> sets controls, so their change events are ignored.</summary>
    protected bool Syncing { get; private set; }

    protected bool ClearShown
    {
        set => _clear!.IsVisible = value;
    }

    protected IReadOnlyDictionary<TKind, Button> Tokens => _tokens;

    protected abstract void Clear();

    /// <summary>Sets token visibility, closes a panel that no longer applies and writes each token's value.</summary>
    protected abstract void Update();

    protected abstract Control PanelFor(TKind kind);

    /// <summary>Adds the sentence, the hint, the panel host and the results line.</summary>
    protected void Assemble(WrapPanel sentence, string? clearName)
    {
        var hint = AdventuresPage.Text(TokenHint, TypeScale.Body, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 4, 0, 0);

        _panelHost = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 0), IsVisible = false };
        _panelHost.Bind(Border.BackgroundProperty, Resource(ThemeManager.BarKey));
        _panelHost.Bind(Border.BorderBrushProperty, Resource(ThemeManager.AKey));

        _count = new TextBlock { FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center };
        _count.Bind(TextBlock.ForegroundProperty, Resource(ThemeManager.GreyKey));

        _clear = new Button { Name = clearName, Content = "Clear filters", Height = TypeScale.MinimumTarget, MinHeight = TypeScale.MinimumTarget, IsVisible = false };
        _clear.Click += (_, _) => Clear();

        var results = new DockPanel { Margin = new Thickness(0, 10, 0, 6) };
        DockPanel.SetDock(_clear, Dock.Right);
        results.Children.Add(_clear);
        results.Children.Add(_count);

        Children.Add(sentence);
        Children.Add(hint);
        Children.Add(_panelHost);
        Children.Add(results);
    }

    /// <summary>Draws "N of M" followed by <paramref name="noun"/>.</summary>
    protected void ShowCount(int shown, int total, string noun)
    {
        _count!.Inlines ??= [];
        _count.Inlines.Clear();
        _count.Inlines.Add(Fonts.Mono(shown.ToString(CultureInfo.InvariantCulture)));
        _count.Inlines.Add(new Run(" of "));
        _count.Inlines.Add(Fonts.Mono(total.ToString(CultureInfo.InvariantCulture)));
        _count.Inlines.Add(new Run(" " + noun));
    }

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    protected bool IsOpen(TKind kind) => Open is { } current && EqualityComparer<TKind>.Default.Equals(current, kind);

    protected void Refresh()
    {
        Syncing = true;

        Update();

        foreach (var (kind, token) in _tokens)
        {
            var open = IsOpen(kind);

            token.Classes.Set(ListRow.SelectedClass, open);
            _inks.GetValueOrDefault(kind)?.Dispose();
            _inks[kind] = Values[kind].Bind(TextBlock.ForegroundProperty, Resource(open ? ThemeManager.KnockKey : ThemeManager.AKey));
        }

        _panelHost!.IsVisible = Open is not null;
        _panelHost.Child = Open is { } opened ? PanelFor(opened) : null;

        Syncing = false;
    }

    protected static TextBlock Words(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Sentence,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        block.Bind(TextBlock.ForegroundProperty, Resource(ThemeManager.WhiteKey));
        return block;
    }

    protected Button Token(TKind kind, string name)
    {
        var value = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Section,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Section * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var caret = new TextBlock
        {
            Text = "▼",
            FontSize = TypeScale.Caption,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        caret.Bind(TextBlock.ForegroundProperty, value.GetObservable(TextBlock.ForegroundProperty));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(value);
        row.Children.Add(caret);

        var button = new Button
        {
            Name = _tokenNamePrefix is null ? null : _tokenNamePrefix + kind,
            Content = row,
            Height = TypeScale.MinimumTarget,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            Margin = new Thickness(0, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        button.Classes.Add(ListRow.Class);
        button.Classes.Add(TokenClass);
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) =>
        {
            Open = IsOpen(kind) ? null : kind;
            Refresh();
        };

        _tokens[kind] = button;
        Values[kind] = value;
        return button;
    }

    protected static IObservable<object?> Resource(string key) =>
        Application.Current!.Resources.GetResourceObservable(key);
}
