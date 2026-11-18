using System.Globalization;
using D47.App.Theming;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>One page of the Every charge list, with the figures its results line reads.</summary>
/// <param name="Rows">The page's charges, in order.</param>
/// <param name="Listed">How many charges the filter and the spoken-lines choice leave in the list.</param>
/// <param name="Spoken">The window's spoken lines, whether or not they are listed.</param>
/// <param name="Purpose">What the purpose the list is filtered to came to; null when it is not filtered.</param>
public sealed record SpendChargePage(
    IReadOnlyList<SpendEntry> Rows,
    int Listed,
    int Page,
    int PageSize,
    SpendPurposeShare Spoken,
    SpendPurposeShare? Purpose)
{
    public int Pages => Math.Max(1, (Listed + PageSize - 1) / PageSize);

    public bool HasPrevious => Page > 0;

    public bool HasNext => Page < Pages - 1;

    /// <summary>"1–100 of 2,684"; empty when nothing is listed.</summary>
    public string Readout =>
        Listed == 0
            ? string.Empty
            : $"{Count((Page * PageSize) + 1)}–{Count(Math.Min(Listed, (Page + 1) * PageSize))} of {Count(Listed)}";

    private static string Count(int count) => count.ToString("N0", CultureInfo.CurrentCulture);
}

/// <summary>The Every charge list's filter, sort and page over one window's charges. Every change but a page step returns to the first page.</summary>
public sealed class SpendChargeList
{
    public const int DesktopPageSize = 100;

    public const int HeadsetPageSize = 25;

    private bool _headset;
    private bool _showSpoken;

    public int Page { get; private set; }

    public int PageSize => _headset ? HeadsetPageSize : DesktopPageSize;

    /// <summary>The chosen sort; null is cost, most first.</summary>
    public SortOrder? Order { get; private set; }

    /// <summary>The purpose and kind the list is filtered to; null lists every purpose.</summary>
    public (string? Purpose, SpendKind Kind)? Filter { get; private set; }

    public bool Headset
    {
        get => _headset;
        set
        {
            if (_headset != value)
            {
                _headset = value;
                Page = 0;
            }
        }
    }

    /// <summary>Whether voice charges are listed when the list is not filtered to a voice purpose.</summary>
    public bool ShowSpoken
    {
        get => _showSpoken;
        set
        {
            _showSpoken = value;
            Page = 0;
        }
    }

    public void SortBy(SortOrder? order)
    {
        Order = order;
        Page = 0;
    }

    public void FilterTo(string? purpose, SpendKind kind)
    {
        Filter = (purpose, kind);
        Page = 0;
    }

    public void ShowEveryPurpose()
    {
        Filter = null;
        Page = 0;
    }

    public void WindowChanged() => Page = 0;

    public void NextPage() => Page++;

    public void PreviousPage() => Page = Math.Max(0, Page - 1);

    /// <summary>The current page of <paramref name="charges"/>, a window's charges in any order.</summary>
    public SpendChargePage Of(IReadOnlyList<SpendEntry> charges)
    {
        ArgumentNullException.ThrowIfNull(charges);

        var spoken = Share(null, SpendKind.Voice, [.. charges.Where(entry => entry.Kind == SpendKind.Voice)]);

        List<SpendEntry> listed;
        SpendPurposeShare? purpose = null;

        if (Filter is { } filter)
        {
            listed = [.. charges.Where(entry => entry.Purpose == filter.Purpose && entry.Kind == filter.Kind)];
            purpose = Share(filter.Purpose, filter.Kind, listed);
        }
        else
        {
            listed = [.. charges.Where(entry => _showSpoken || entry.Kind != SpendKind.Voice)];
        }

        var newestFirst = listed.OrderByDescending(entry => entry.At).ToList();
        var sorted = ColumnSort.Sorted(newestFirst, SpendBreakdownModel.ChargeColumns, Order ?? SpendBreakdownModel.ChargesByDefault);

        Page = Math.Clamp(Page, 0, Math.Max(0, (sorted.Count - 1) / PageSize));

        return new SpendChargePage(
            [.. sorted.Skip(Page * PageSize).Take(PageSize)],
            sorted.Count,
            Page,
            PageSize,
            spoken,
            purpose);
    }

    private static SpendPurposeShare Share(string? purpose, SpendKind kind, IReadOnlyList<SpendEntry> rows) => new(
        purpose,
        kind,
        rows.Count,
        rows.Where(entry => entry.Priced).Sum(entry => entry.Dollars),
        rows.Count(entry => !entry.Priced));
}
