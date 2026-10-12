using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Text pieces the search forms' fields share.</summary>
internal static class SearchText
{
    /// <summary>A grey Chrome prefix drawn inside a box.</summary>
    internal static TextBlock Prefix(string text)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0),
        };
        Ink(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    /// <summary>A grey Prose line under a field.</summary>
    internal static TextBlock Line(string? text = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Tip,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = text is not null,
        };
        Ink(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    internal static void Ink(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}

/// <summary>The line under a pair of range boxes that says what the range will do.</summary>
internal sealed class RangeReadout
{
    private readonly TextBox _from;
    private readonly TextBox _to;
    private readonly Func<double?, double?, (string Lead, string? Reference, string Tail)?> _say;

    public RangeReadout(TextBox from, TextBox to, Func<double?, double?, (string Lead, string? Reference, string Tail)?> say)
    {
        _from = from;
        _to = to;
        _say = say;

        from.PropertyChanged += Changed;
        to.PropertyChanged += Changed;
        Update();
    }

    public TextBlock Line { get; } = SearchText.Line();

    /// <summary>Redraws the line from the boxes as they stand.</summary>
    public void Update()
    {
        Line.Inlines ??= [];
        Line.Inlines.Clear();

        var said = SystemSearchPage.Range(_from.Text, _to.Text) is { } range && GalaxyCriteria.TryParseRange(range, out var min, out var max)
            ? _say(min, max)
            : null;

        Line.IsVisible = said is not null;

        if (said is not { } words)
        {
            return;
        }

        Line.Inlines.Add(new Run(words.Lead));

        if (words.Reference is { } reference)
        {
            var name = new Run(reference);
            name.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.CyanKey));
            Line.Inlines.Add(name);
        }

        Line.Inlines.Add(new Run(words.Tail));
    }

    private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
        {
            Update();
        }
    }

    /// <summary>The line for a distance range, in the words the answer head uses.</summary>
    internal static (string Lead, string? Reference, string Tail)? Distance(double? min, double? max, string from) => (min, max) switch
    {
        ({ } low, { } high) => ($"Systems {SystemSearchPage.Number(low)}–{SystemSearchPage.Number(high)} LY from ", from.ToUpperInvariant(), ", nearest first."),
        (null, { } high) => ($"Systems within {SystemSearchPage.Number(high)} LY of ", from.ToUpperInvariant(), ", nearest first."),
        ({ } low, null) => ($"Systems beyond {SystemSearchPage.Number(low)} LY of ", from.ToUpperInvariant(), ", nearest first."),
        _ => null,
    };

    /// <summary>The line for a measurement range such as gravity or temperature.</summary>
    internal static (string Lead, string? Reference, string Tail)? Measure(double? min, double? max, string subject, string unit) => (min, max) switch
    {
        ({ } low, { } high) => ($"{subject} {SystemSearchPage.Number(low)}–{SystemSearchPage.Number(high)} {unit}.", null, string.Empty),
        (null, { } high) => ($"{subject} up to {SystemSearchPage.Number(high)} {unit}.", null, string.Empty),
        ({ } low, null) => ($"{subject} above {SystemSearchPage.Number(low)} {unit}.", null, string.Empty),
        _ => null,
    };
}

/// <summary>The SHOW UP TO dropdown: how many results a search returns.</summary>
internal sealed class ShowUpTo
{
    internal const int Default = 25;

    internal const int Max = 100;

    internal static readonly IReadOnlyList<int> Choices = [1, 5, 10, 25, 50, 100];

    private readonly InlinePicker _picker = new() { Label = "show up to", Mono = true };
    private int _value = Default;

    public ShowUpTo()
    {
        _picker.Picked += (_, id) => Value = int.Parse(id, CultureInfo.InvariantCulture);
        Draw();
    }

    /// <summary>The count shown; one outside <see cref="Choices"/> is shown as it is.</summary>
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            Draw();
        }
    }

    /// <summary>The labelled field, with a line naming what is counted.</summary>
    public Control Field(string noun) =>
        SystemSearchPage.Labelled("Show up to", new StackPanel { Spacing = 4, Children = { _picker, SearchText.Line($"{noun} in the answer.") } });

    private void Draw()
    {
        var shown = _value.ToString(CultureInfo.InvariantCulture);

        _picker.Show(
            [.. Choices.Select(choice => new InlinePickerOption(choice.ToString(CultureInfo.InvariantCulture), choice.ToString(CultureInfo.InvariantCulture), ThemeManager.WhiteKey))],
            shown,
            shown,
            ThemeManager.WhiteKey,
            null);
    }
}

/// <summary>The MINIMUM POPULATION field: three tiles and a box, sent as a population range.</summary>
internal sealed class MinimumPopulation : StackPanel
{
    internal const long Populated = 1;

    internal const long High = 1_000_000;

    private readonly CheckBox _unpopulated = Tile("Unpopulated");
    private readonly CheckBox _populated = Tile("Populated");
    private readonly CheckBox _high = Tile("High");
    private readonly TextBox _box;
    private readonly TextBlock _line = SearchText.Line();
    private bool _setting;

    public MinimumPopulation(TextBox box)
    {
        _box = box;
        box.Width = 150;
        box.PlaceholderText = null;
        Spacing = 6;

        Children.Add(new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _unpopulated, _populated, _high, box } });
        Children.Add(_line);

        _unpopulated.IsCheckedChanged += (_, _) => Chose(_unpopulated, null);
        _populated.IsCheckedChanged += (_, _) => Chose(_populated, Populated);
        _high.IsCheckedChanged += (_, _) => Chose(_high, High);
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_setting)
            {
                _unpopulated.IsChecked = false;
                Sync();
            }
        };
    }

    /// <summary>The box the minimum is typed in.</summary>
    public TextBox Box => _box;

    /// <summary>The population argument the field sends, or null where it asks for nothing.</summary>
    public string? Asked => Argument(_unpopulated.IsChecked == true, _box.Text);

    /// <summary>The argument for the unpopulated tile and the box: "0-0", "N-", or none for an empty box or a typed 0.</summary>
    public static string? Argument(bool unpopulated, string? text)
    {
        if (unpopulated)
        {
            return "0-0";
        }

        var typed = text?.Replace(",", string.Empty).Trim() ?? string.Empty;

        if (typed.Length == 0)
        {
            return null;
        }

        return long.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var people)
            ? people == 0 ? null : $"{people}-"
            : $"{typed}-";
    }

    /// <summary>Fills the field from a population argument; a range with an upper bound keeps its minimum.</summary>
    public void Set(string? population)
    {
        _setting = true;
        _unpopulated.IsChecked = false;
        _box.Text = string.Empty;

        if (population is not null && GalaxyCriteria.TryParseRange(population, out var min, out var max))
        {
            if (max == 0 && min is null or 0)
            {
                _unpopulated.IsChecked = true;
            }
            else if (min is > 0)
            {
                _box.Text = ((long)min).ToString("N0", CultureInfo.InvariantCulture);
            }
        }

        _setting = false;
        Sync();
    }

    private static CheckBox Tile(string name)
    {
        var tile = LabeledCheckBox.Caps(name);
        tile.MinHeight = TypeScale.MinimumTarget;
        return tile;
    }

    private long? Typed() =>
        long.TryParse(_box.Text?.Replace(",", string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var people) ? people : null;

    private void Chose(CheckBox tile, long? people)
    {
        if (_setting)
        {
            return;
        }

        _setting = true;

        if (tile.IsChecked == true)
        {
            foreach (var other in (CheckBox[])[_unpopulated, _populated, _high])
            {
                if (!ReferenceEquals(other, tile))
                {
                    other.IsChecked = false;
                }
            }

            _box.Text = people is { } count ? count.ToString("N0", CultureInfo.InvariantCulture) : string.Empty;
        }
        else if (people is not null && Typed() == people)
        {
            _box.Text = string.Empty;
        }

        _setting = false;
        Sync();
    }

    private void Sync()
    {
        var wasSetting = _setting;
        _setting = true;
        _populated.IsChecked = Typed() == Populated;
        _high.IsChecked = Typed() == High;
        _setting = wasSetting;

        var people = Typed();

        _line.Text = _unpopulated.IsChecked == true
            ? "Unpopulated systems only."
            : people is > 0 ? $"Systems with {people.Value.ToString("N0", CultureInfo.InvariantCulture)} people or more." : null;
        _line.IsVisible = _line.Text is not null;
    }
}
