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
/// The Discoveries page's filter as a sentence of tokens: "Show [status] entries in [category] [sub-category]". A token
/// opens a Segment of its choices inside the page and closes on the pick; the results line carries Clear filters.
/// </summary>
internal sealed class CodexDiscoveryFilters : StackPanel
{
    public const string AllCategories = "All categories";

    public const string AllSubCategories = "All sub-categories";

    private enum Kind
    {
        Status,
        Category,
        SubCategory,
    }

    private static readonly string[] StatusLabels = ["Missing", "On record", "All"];

    private readonly Dictionary<Kind, Button> _tokens = [];
    private readonly Dictionary<Kind, TextBlock> _values = [];
    private readonly Dictionary<Kind, IDisposable> _inks = [];
    private readonly Border _panelHost;
    private readonly TextBlock _count;
    private readonly Button _clear;
    private IReadOnlyList<string> _categories = [];
    private IReadOnlyList<string> _subCategories = [];
    private CodexDiscoveryFilter _filter = new();
    private Kind? _open;

    public CodexDiscoveryFilters()
    {
        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Kind.Status, "Status"));
        sentence.Children.Add(Words("entries in"));
        sentence.Children.Add(Token(Kind.Category, "Category"));
        sentence.Children.Add(Token(Kind.SubCategory, "Sub-category"));

        var hint = AdventuresPage.Text(CatalogueFilters.TokenHint, TypeScale.Body, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 4, 0, 0);

        _panelHost = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 0), IsVisible = false };
        _panelHost.Bind(Border.BackgroundProperty, Resource(ThemeManager.BarKey));
        _panelHost.Bind(Border.BorderBrushProperty, Resource(ThemeManager.AKey));

        _count = new TextBlock { FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center };
        _count.Bind(TextBlock.ForegroundProperty, Resource(ThemeManager.GreyKey));

        _clear = new Button { Name = "CodexDiscoveryClear", Content = "Clear filters", Height = TypeScale.MinimumTarget, MinHeight = TypeScale.MinimumTarget, IsVisible = false };
        _clear.Click += (_, _) => Clear();

        var results = new DockPanel { Margin = new Thickness(0, 10, 0, 6) };
        DockPanel.SetDock(_clear, Dock.Right);
        results.Children.Add(_clear);
        results.Children.Add(_count);

        Children.Add(sentence);
        Children.Add(hint);
        Children.Add(_panelHost);
        Children.Add(results);

        Refresh();
    }

    /// <summary>Raised after the Commander changes the filter.</summary>
    public event EventHandler? Changed;

    public CodexDiscoveryFilter Filter => _filter;

    /// <summary>Takes the choices the region offers and draws "N of M entries", and Clear filters while a filter is off its default.</summary>
    public void Show(IReadOnlyList<string> categories, IReadOnlyList<string> subCategories, int shown, int total)
    {
        _categories = categories;
        _subCategories = subCategories;

        _count.Inlines ??= [];
        _count.Inlines.Clear();
        _count.Inlines.Add(Fonts.Mono(shown.ToString(CultureInfo.InvariantCulture)));
        _count.Inlines.Add(new Run(" of "));
        _count.Inlines.Add(Fonts.Mono(total.ToString(CultureInfo.InvariantCulture)));
        _count.Inlines.Add(new Run(" entries"));

        Refresh();
    }

    private bool SubCategoryShown => _filter.Category is not null && _subCategories.Count > 1;

    private void Clear()
    {
        _filter = new CodexDiscoveryFilter();
        _open = null;
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static TextBlock Words(string text)
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

    private Button Token(Kind kind, string name)
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
            Name = "CodexDiscoveryToken." + kind,
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
        button.Classes.Add(CatalogueFilters.TokenClass);
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) =>
        {
            _open = _open == kind ? null : kind;
            Refresh();
        };

        _tokens[kind] = button;
        _values[kind] = value;
        return button;
    }

    private void Refresh()
    {
        _tokens[Kind.SubCategory].IsVisible = SubCategoryShown;

        if (_open == Kind.SubCategory && !SubCategoryShown)
        {
            _open = null;
        }

        _values[Kind.Status].Text = StatusLabels[(int)_filter.Status].ToUpperInvariant();
        _values[Kind.Category].Text = (_filter.Category ?? AllCategories).ToUpperInvariant();
        _values[Kind.SubCategory].Text = (_filter.SubCategory ?? AllSubCategories).ToUpperInvariant();

        foreach (var (kind, token) in _tokens)
        {
            var open = _open == kind;

            token.Classes.Set(ListRow.SelectedClass, open);
            _inks.GetValueOrDefault(kind)?.Dispose();
            _inks[kind] = _values[kind].Bind(TextBlock.ForegroundProperty, Resource(open ? ThemeManager.KnockKey : ThemeManager.AKey));
        }

        _panelHost.IsVisible = _open is not null;
        _panelHost.Child = _open is { } opened ? PanelFor(opened) : null;
        _clear.IsVisible = !_filter.IsDefault;
    }

    private Segment PanelFor(Kind kind)
    {
        var (options, selected, name) = kind switch
        {
            Kind.Status => ((IReadOnlyList<string>)StatusLabels, (int)_filter.Status, "Status"),
            Kind.Category => ([AllCategories, .. _categories], IndexOf(_categories, _filter.Category), "Category"),
            _ => ([AllSubCategories, .. _subCategories], IndexOf(_subCategories, _filter.SubCategory), "Sub-category"),
        };

        var segment = new Segment { ItemsSource = options, SelectedIndex = selected };
        AutomationProperties.SetName(segment, name + " options");

        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex < 0)
            {
                return;
            }

            var index = segment.SelectedIndex;

            _filter = kind switch
            {
                Kind.Status => _filter with { Status = (CodexStatusFilter)index },
                Kind.Category => _filter with { Category = index == 0 ? null : _categories[index - 1], SubCategory = null },
                _ => _filter with { SubCategory = index == 0 ? null : _subCategories[index - 1] },
            };
            _open = null;
            Refresh();
            Changed?.Invoke(this, EventArgs.Empty);
        };

        return segment;
    }

    private static int IndexOf(IReadOnlyList<string> options, string? chosen)
    {
        if (chosen is null)
        {
            return 0;
        }

        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i], chosen, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static IObservable<object?> Resource(string key) =>
        Application.Current!.Resources.GetResourceObservable(key);
}
