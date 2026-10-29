using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;

namespace D47.App.Panel;

/// <summary>
/// The Discoveries page's filter as a sentence of tokens: "Show [status] entries in [category] [sub-category]". A token
/// opens a Segment of its choices inside the page and closes on the pick; the results line carries Clear filters.
/// </summary>
internal sealed class CodexDiscoveryFilters : TokenFilterBar<CodexDiscoveryFilters.Kind>
{
    public const string AllCategories = "All categories";

    public const string AllSubCategories = "All sub-categories";

    internal enum Kind
    {
        Status,
        Category,
        SubCategory,
    }

    private static readonly string[] StatusLabels = ["Missing", "On record", "All"];

    private IReadOnlyList<string> _categories = [];
    private IReadOnlyList<string> _subCategories = [];
    private CodexDiscoveryFilter _filter = new();

    public CodexDiscoveryFilters()
        : base("CodexDiscoveryToken.")
    {
        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Kind.Status, "Status"));
        sentence.Children.Add(Words("entries in"));
        sentence.Children.Add(Token(Kind.Category, "Category"));
        sentence.Children.Add(Token(Kind.SubCategory, "Sub-category"));

        Assemble(sentence, "CodexDiscoveryClear");

        Refresh();
    }

    public CodexDiscoveryFilter Filter => _filter;

    /// <summary>Takes the choices the region offers and draws "N of M entries", and Clear filters while a filter is off its default.</summary>
    public void Show(IReadOnlyList<string> categories, IReadOnlyList<string> subCategories, int shown, int total)
    {
        _categories = categories;
        _subCategories = subCategories;

        ShowCount(shown, total, "entries");

        Refresh();
    }

    private bool SubCategoryShown => _filter.Category is not null && _subCategories.Count > 1;

    protected override void Clear()
    {
        _filter = new CodexDiscoveryFilter();
        Open = null;
        Refresh();
        RaiseChanged();
    }

    protected override void Update()
    {
        Tokens[Kind.SubCategory].IsVisible = SubCategoryShown;

        if (Open == Kind.SubCategory && !SubCategoryShown)
        {
            Open = null;
        }

        Values[Kind.Status].Text = StatusLabels[(int)_filter.Status].ToUpperInvariant();
        Values[Kind.Category].Text = (_filter.Category ?? AllCategories).ToUpperInvariant();
        Values[Kind.SubCategory].Text = (_filter.SubCategory ?? AllSubCategories).ToUpperInvariant();

        ClearShown = !_filter.IsDefault;
    }

    protected override Control PanelFor(Kind kind)
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
            Open = null;
            Refresh();
            RaiseChanged();
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
}
