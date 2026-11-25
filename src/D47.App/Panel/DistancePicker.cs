using System.Globalization;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The distance dropdown on a Search tab: a fixed list of light-year steps, plus Any where the page offers it, plus the
/// stored value while it is not one of the steps.
/// </summary>
public sealed class DistancePicker
{
    public const string PickerName = "SearchDistance";

    public const string AnyId = "any";

    public static IReadOnlyList<double> Steps { get; } = [10, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, GalaxyQuery.FarthestDistance];

    private readonly int _default;
    private readonly bool _offersAny;
    private double? _extra;

    public DistancePicker(int defaultDistance, bool offersAny = false)
    {
        _default = defaultDistance;
        _offersAny = offersAny;
        Picker = new InlinePicker { Name = PickerName, Label = "distance", Mono = true, ListMaxHeight = 240 };
        Picker.Picked += (_, id) =>
        {
            Chosen = true;
            Select(id == AnyId ? null : double.Parse(id, CultureInfo.InvariantCulture));
        };
        Select(defaultDistance);
    }

    public InlinePicker Picker { get; }

    /// <summary>The distance shown, or null for Any.</summary>
    public double? Value { get; private set; }

    /// <summary>Whether the Commander, or a stored search, set the distance rather than the page's default.</summary>
    public bool Chosen { get; private set; }

    /// <summary>The <c>max_distance</c> to send: none for Any, and none for the default unless it was chosen and <paramref name="sendChosenDefault"/>.</summary>
    public string? Argument(bool sendChosenDefault) =>
        Value is { } value && (value != _default || (sendChosenDefault && Chosen))
            ? value.ToString("0.##", CultureInfo.InvariantCulture)
            : null;

    /// <summary>Shows <paramref name="asked"/>, held to 1 and the farthest distance, adding it to the list when it is not a step.</summary>
    public void Set(double asked)
    {
        Chosen = true;
        Select(Math.Clamp(asked, 1, GalaxyQuery.FarthestDistance));
    }

    /// <summary>Back to the default, or to Any, with nothing chosen.</summary>
    public void Reset(bool any = false)
    {
        Chosen = false;
        Select(any ? null : _default);
    }

    /// <summary>Shows Any or the default until something is chosen.</summary>
    public void ShowDefault(bool any)
    {
        if (!Chosen)
        {
            Select(any ? null : _default);
        }
    }

    private void Select(double? value)
    {
        Value = value;
        _extra = value is { } shown && !Steps.Contains(shown) ? shown : null;

        var numbers = _extra is { } extra ? Steps.Append(extra).Order().ToList() : [.. Steps];
        var entries = new List<InlinePickerEntry>();

        if (_offersAny)
        {
            entries.Add(new InlinePickerOption(AnyId, "Any", ThemeManager.GreyKey));
        }

        entries.AddRange(numbers.Select(number => new InlinePickerOption(Id(number), Label(number), ThemeManager.WhiteKey)));

        Picker.Show(
            entries,
            value is { } current ? Id(current) : AnyId,
            value is { } text ? Label(text) : "Any",
            value is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            null);
    }

    private static string Id(double number) => number.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Label(double number) => number.ToString("#,0.##", CultureInfo.InvariantCulture);
}
