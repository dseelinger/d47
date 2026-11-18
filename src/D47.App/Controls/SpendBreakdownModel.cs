using System.Globalization;
using D47.App.Theming;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>The text and order of the spend breakdown page, from the ledger's figures.</summary>
public static class SpendBreakdownModel
{
    public const string Summary = "What D47's estimate went on, one window at a time.";

    public const string Hint =
        "D47's estimate from published rates. ≥ means part of it has no rate. Free voices, and charges covered by "
        + "a reset, are left out, as they are in the totals.";

    public const string NothingHint = "Free voices and transcription on this computer are never listed here.";

    public const string NotRecordedLine = "Charged before D47 began recording purposes";

    public const string NoRate = "no rate";

    /// <summary>The section names, in the order the Segmented shows them.</summary>
    public static IReadOnlyList<string> Sections { get; } = ["By purpose", "Every charge"];

    public const int ByPurposeSection = 0;

    /// <summary>The window the page opens on: Last 30 days, the last of <see cref="SpendPeriods.All"/>.</summary>
    public const string OpeningWindow = "Last 30 days";

    /// <summary>The Cost column's index in <see cref="PurposeColumns"/>.</summary>
    public const int CostColumn = 3;

    /// <summary>What a window came to, over the charges <see cref="SpendLedger.ByPurpose"/> groups.</summary>
    public sealed record WindowFigures(int Charges, decimal Dollars, bool Complete, int Purposes)
    {
        public static WindowFigures Of(IReadOnlyList<SpendPurposeShare> purposes) => new(
            purposes.Sum(share => share.Charges),
            purposes.Sum(share => share.Dollars),
            purposes.All(share => share.Complete),
            purposes.Count);

        public bool Any => Charges > 0;
    }

    /// <summary>"18 Oct 19:42 – 17 Nov 19:42", in <paramref name="zone"/>.</summary>
    public static string RangeText(SpendPeriod window, TimeZoneInfo zone) =>
        $"{Local(window.From, zone)} – {Local(window.To, zone)}";

    private static string Local(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone).ToString("d MMM HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"6,614 charges", or "1 charge".</summary>
    public static string ChargesText(int charges) =>
        charges == 1 ? "1 charge" : $"{charges.ToString("N0", CultureInfo.CurrentCulture)} charges";

    /// <summary>"12 purposes", or "1 purpose".</summary>
    public static string PurposesText(int purposes) =>
        purposes == 1 ? "1 purpose" : $"{purposes.ToString("N0", CultureInfo.CurrentCulture)} purposes";

    /// <summary>The window's total, "≥ " in front when part of it has no rate.</summary>
    public static string TotalText(WindowFigures figures) => (figures.Complete ? string.Empty : "≥ ") + Money(figures.Dollars);

    /// <summary>"Nothing has been charged today.", with a rolling window read as "in the last 7 days".</summary>
    public static string NothingText(string window)
    {
        var lower = window.ToLowerInvariant();

        return $"Nothing has been charged {(lower.StartsWith("last ", StringComparison.Ordinal) ? "in the " + lower : lower)}.";
    }

    /// <summary>MODEL, VOICE or TRANSCRIPTION.</summary>
    public static string KindWord(SpendKind kind) => kind switch
    {
        SpendKind.Voice => "VOICE",
        SpendKind.Transcription => "TRANSCRIPTION",
        _ => "MODEL",
    };

    public static string NameOf(SpendPurposeShare share) => SpendPurposes.NameOf(share.Purpose);

    public static bool NotRecorded(SpendPurposeShare share) => share.Purpose is null;

    /// <summary>Whether any of the purpose's charges had a rate.</summary>
    public static bool Priced(SpendPurposeShare share) => share.Unpriced < share.Charges;

    /// <summary>The cost line: "$4.2057", "≥ $4.2057", or "no rate" when none of its charges had one.</summary>
    public static string CostText(SpendPurposeShare share) =>
        !Priced(share) ? NoRate
        : share.Complete ? Money(share.Dollars)
        : "≥ " + Money(share.Dollars);

    /// <summary>"3 with no rate" under a partly priced cost; null otherwise.</summary>
    public static string? UnpricedText(SpendPurposeShare share) =>
        Priced(share) && !share.Complete ? $"{share.Unpriced.ToString("N0", CultureInfo.CurrentCulture)} with no rate" : null;

    /// <summary>The purpose's cost over the window's, to one place; a dash when either has nothing priced.</summary>
    public static string ShareText(SpendPurposeShare share, decimal window) =>
        !Priced(share) || window <= 0m
            ? "—"
            : (share.Dollars / window).ToString("P1", CultureInfo.InvariantCulture).Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>Purpose, Charges, Share and Cost; a purpose with nothing priced has no cost to sort by.</summary>
    public static IReadOnlyList<SortColumn<SpendPurposeShare>> PurposeColumns { get; } =
    [
        SortColumn<SpendPurposeShare>.Text("Purpose", NameOf, share => KindWord(share.Kind)),
        SortColumn<SpendPurposeShare>.Figure("Charges", share => (int?)share.Charges, SortDirection.Descending),
        SortColumn<SpendPurposeShare>.Plain("Share"),
        SortColumn<SpendPurposeShare>.Figure("Cost", share => Priced(share) ? share.Dollars : (decimal?)null, SortDirection.Descending),
    ];

    /// <summary>The page's default order: cost, most first, with purposes that have no rate last.</summary>
    public static SortOrder ByDefault { get; } = new(CostColumn, SortDirection.Descending);

    /// <summary><paramref name="purposes"/> in the default order, ties by name.</summary>
    public static IReadOnlyList<SpendPurposeShare> InDefaultOrder(IReadOnlyList<SpendPurposeShare> purposes) =>
        ColumnSort.Sorted(
            [.. purposes.OrderBy(NameOf, StringComparer.CurrentCultureIgnoreCase).ThenBy(share => share.Kind)],
            PurposeColumns,
            ByDefault);

    private static string Money(decimal dollars) => dollars.ToString("C4", CultureInfo.CurrentCulture);
}
