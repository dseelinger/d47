using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Books;

namespace D47.App.Panel;

/// <summary>
/// Add a book › Project Gutenberg's filter: a title or author field, then the sentence
/// "Show [order] books about [subjects] in [language]", and the results line with Clear filters.
/// </summary>
internal sealed class GutenbergFilters : TokenFilterBar<GutenbergFilters.Kind>
{
    public const string SubjectHint = "Tick any. With none ticked, every subject shows.";

    public const string AnySubject = "any subject";

    public const string TextName = "GutenbergText";

    public const string ClearName = "GutenbergClear";

    public const string SubjectName = "GutenbergSubject";

    public const string LanguageName = "GutenbergLanguage";

    /// <summary>The language shown first, and chosen until another is.</summary>
    public const string English = "en";

    internal enum Kind
    {
        Order,
        Subject,
        Language,
    }

    private static readonly string[] OrderLabels = ["Popular", "A to Z", "Newest"];

    private readonly TextBox _text;
    private readonly bool _fill;
    private readonly HashSet<string> _subjects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CheckBox> _subjectBoxes = [];
    private Control? _subjectPanel;
    private Segment? _orderSegment;
    private IReadOnlyList<GutenbergLanguage> _languages = [];
    private GutenbergOrder _order;
    private string _language = English;
    private bool _clearingText;

    /// <summary>Given <paramref name="fill"/>, the language panel takes whatever height its host is given rather than at most 240.</summary>
    public GutenbergFilters(bool fill = false)
        : base("GutenbergToken.")
    {
        _fill = fill;
        _text = new TextBox
        {
            Name = TextName,
            PlaceholderText = "Title or author",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 12),
        };
        AutomationProperties.SetName(_text, "Filter the catalogue by title or author");
        _text.TextChanged += (_, _) =>
        {
            if (Syncing || _clearingText)
            {
                return;
            }

            Refresh();
            RaiseChanged();
        };

        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Kind.Order, "Order"));
        sentence.Children.Add(Words("books about"));
        sentence.Children.Add(Token(Kind.Subject, "Subjects"));
        sentence.Children.Add(Words("in"));
        sentence.Children.Add(Token(Kind.Language, "Language"));

        Children.Add(_text);
        Assemble(sentence, ClearName);

        Refresh();
    }

    /// <summary>The search the filter describes.</summary>
    public GutenbergQuery Query => new(
        (_text.Text ?? string.Empty).Trim(),
        GutenbergCatalogue.Subjects.Where(_subjects.Contains).ToList() is { Count: > 0 } subjects ? subjects : null,
        _language,
        _order);

    /// <summary>The language chosen until another is: English, else the language with the most books.</summary>
    private string DefaultLanguage =>
        _languages.Count == 0 || _languages.Any(language => IsEnglish(language.Code)) ? English : _languages[0].Code;

    private bool IsDefault =>
        string.IsNullOrWhiteSpace(_text.Text)
        && _order == GutenbergOrder.Popular
        && _subjects.Count == 0
        && string.Equals(_language, DefaultLanguage, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sets the languages the token lists, English first and then by book count.</summary>
    public void SetLanguages(IReadOnlyList<GutenbergLanguage> languages)
    {
        var wasDefault = string.Equals(_language, DefaultLanguage, StringComparison.OrdinalIgnoreCase);

        _languages = [.. languages.Where(language => IsEnglish(language.Code)), .. languages.Where(language => !IsEnglish(language.Code))];

        if (wasDefault)
        {
            _language = DefaultLanguage;
        }

        Refresh();
    }

    /// <summary>Draws "N of M books" and the token values.</summary>
    public void Show(int shown, int matched)
    {
        ShowDrawn(shown, matched);
        Refresh();
    }

    /// <summary>Draws "N of M books" alone, leaving an open panel as it is.</summary>
    public void ShowDrawn(int shown, int matched) => ShowCount(shown, matched, "books");

    /// <summary>Draws "M books" and the token values.</summary>
    public void ShowMatched(int matched)
    {
        ShowLine(Fonts.Mono(matched.ToString(CultureInfo.InvariantCulture)), new Avalonia.Controls.Documents.Run(" books"));
        Refresh();
    }

    protected override void Clear()
    {
        ClearText();
        _order = GutenbergOrder.Popular;
        _subjects.Clear();
        _language = DefaultLanguage;
        Open = null;
        Refresh();
        RaiseChanged();
    }

    protected override void Update()
    {
        Values[Kind.Order].Text = OrderLabels[(int)_order].ToUpperInvariant();
        Values[Kind.Subject].Text = SubjectText().ToUpperInvariant();
        Values[Kind.Language].Text = GutenbergLanguages.NameOf(_language).ToUpperInvariant();

        if (_orderSegment is not null)
        {
            _orderSegment.SelectedIndex = (int)_order;
        }

        for (var i = 0; i < _subjectBoxes.Count; i++)
        {
            _subjectBoxes[i].IsChecked = _subjects.Contains(GutenbergCatalogue.Subjects[i]);
        }

        ClearShown = !IsDefault;
    }

    protected override Control PanelFor(Kind kind) => kind switch
    {
        Kind.Order => _orderSegment ??= OrderPanel(),
        Kind.Subject => _subjectPanel ??= SubjectPanel(),
        _ => LanguagePanel(),
    };

    private static bool IsEnglish(string code) => string.Equals(code, English, StringComparison.OrdinalIgnoreCase);

    private string SubjectText()
    {
        var ticked = GutenbergCatalogue.Subjects.Where(_subjects.Contains).ToList();

        return ticked.Count switch
        {
            0 => AnySubject,
            1 or 2 => string.Join(" and ", ticked),
            _ => $"{ticked.Count} subjects",
        };
    }

    /// <summary>Empties the field without the change counting as the Commander's.</summary>
    private void ClearText()
    {
        _clearingText = true;

        try
        {
            _text.Text = string.Empty;
        }
        finally
        {
            _clearingText = false;
        }
    }

    private Segment OrderPanel()
    {
        var segment = new Segment { ItemsSource = OrderLabels, SelectedIndex = (int)_order };
        AutomationProperties.SetName(segment, "Order options");

        segment.SelectionChanged += (_, _) =>
        {
            if (Syncing || segment.SelectedIndex < 0)
            {
                return;
            }

            _order = (GutenbergOrder)segment.SelectedIndex;
            Open = null;
            Refresh();
            RaiseChanged();
        };

        return segment;
    }

    private StackPanel SubjectPanel()
    {
        var grid = new TileGrid { Columns = 4 };

        foreach (var subject in GutenbergCatalogue.Subjects)
        {
            var box = LabeledCheckBox.Caps(subject);

            box.Name = SubjectName;
            box.Tag = subject;
            box.IsChecked = _subjects.Contains(subject);
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.IsCheckedChanged += (_, _) =>
            {
                if (Syncing)
                {
                    return;
                }

                if (box.IsChecked == true)
                {
                    _subjects.Add(subject);
                }
                else
                {
                    _subjects.Remove(subject);
                }

                Refresh();
                RaiseChanged();
            };

            _subjectBoxes.Add(box);
            grid.Children.Add(box);
        }

        var hint = AdventuresPage.Text(SubjectHint, TypeScale.Small, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 6, 0, 0);

        return new StackPanel { Spacing = Gaps.Tile, Children = { grid, hint } };
    }

    private ScrollViewer LanguagePanel()
    {
        var grid = new TileGrid { Columns = 4 };

        foreach (var language in _languages)
        {
            var count = Typeset.Mono(language.Books.ToString("N0", CultureInfo.InvariantCulture), 13, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, margin: new Thickness(8, 0, 0, 0));
            DockPanel.SetDock(count, Dock.Right);

            var name = ListRow.Name(new TextBlock { Text = language.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });

            var row = new Button
            {
                Name = LanguageName,
                Tag = language.Code,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = TypeScale.MinimumTarget,
                Content = new DockPanel { LastChildFill = true, Children = { count, name } },
            };
            AutomationProperties.SetName(row, language.Name);
            ListRow.Dress(row, string.Equals(language.Code, _language, StringComparison.OrdinalIgnoreCase));
            row.Click += (_, _) =>
            {
                _language = language.Code;
                Open = null;
                Refresh();
                RaiseChanged();
            };

            grid.Children.Add(row);
        }

        return new ScrollViewer
        {
            MaxHeight = _fill ? double.PositiveInfinity : 240,
            Content = grid,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }
}
