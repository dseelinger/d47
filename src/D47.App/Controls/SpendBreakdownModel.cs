using System.Globalization;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Listening;

namespace D47.App.Controls;

/// <summary>The text and order of the spend breakdown page, from the ledger's figures.</summary>
public static class SpendBreakdownModel
{
    public const string Summary = "What D47's estimate went on, one window at a time.";

    /// <summary>Said by the SPEND dialog and this page alike.</summary>
    public const string NotTheBill =
        "not what your account is billed. A subscription with bundled credits can make the real cost anything from "
        + "higher to nothing at all.";

    public const string Hint =
        "D47's estimate from published rates, " + NotTheBill + " ≥ means part of it has no rate. Free voices, and "
        + "charges covered by a reset, are left out, as they are in the totals.";

    public const string NothingHint = "Free voices and transcription on this computer are never listed here.";

    public const string NotRecordedLine = "Charged before D47 began recording purposes";

    public const string NoRate = "no rate";

    /// <summary>The section names, in the order the Segmented shows them.</summary>
    public static IReadOnlyList<string> Sections { get; } = ["By purpose", "Every charge"];

    public const int ByPurposeSection = 0;

    public const int EveryChargeSection = 1;

    public const string PressHint = "Press a purpose to list its charges.";

    public const string ShowSpokenLines = "SHOW SPOKEN LINES";

    public const string ShowEveryPurpose = "SHOW EVERY PURPOSE";

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

    /// <summary>What each catalog calls a provider.</summary>
    public static string ProviderName(SpendKind kind, string providerId) => kind switch
    {
        SpendKind.Voice => TtsProviderCatalog.Selected(providerId).Name,
        SpendKind.Transcription => SttProviderCatalog.Selected(providerId).Name,
        _ => LlmProviderCatalog.Selected(providerId).Name,
    };

    /// <summary>The Time column's index in <see cref="ChargeColumns"/>.</summary>
    public const int TimeColumn = 0;

    /// <summary>The Cost column's index in <see cref="ChargeColumns"/>.</summary>
    public const int ChargeCostColumn = 4;

    /// <summary>Time, Purpose over Step, Model over Provider, Used and Cost; a charge with no rate has no cost to sort by.</summary>
    public static IReadOnlyList<SortColumn<SpendEntry>> ChargeColumns { get; } =
    [
        SortColumn<SpendEntry>.Figure("Time", entry => (DateTimeOffset?)entry.At, SortDirection.Descending),
        SortColumn<SpendEntry>.Text("Purpose", entry => SpendPurposes.NameOf(entry.Purpose), StepWord) with { Second = "Step" },
        SortColumn<SpendEntry>.Text("Model", ModelText, ProviderLine) with { Second = "Provider" },
        SortColumn<SpendEntry>.Plain("Used"),
        SortColumn<SpendEntry>.Figure("Cost", entry => entry.Priced ? entry.Dollars : (decimal?)null, SortDirection.Descending),
    ];

    /// <summary>The charge list's default order: cost, most first, with charges that have no rate last.</summary>
    public static SortOrder ChargesByDefault { get; } = new(ChargeCostColumn, SortDirection.Descending);

    /// <summary>"9 Nov 21:14:06", in <paramref name="zone"/>.</summary>
    public static string TimeText(SpendEntry entry, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(entry.At, zone).ToString("d MMM HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>The line under a charge's purpose: its story step, VOICE or TRANSCRIPTION; null for a model charge with no step.</summary>
    public static string? StepWord(SpendEntry entry) => entry.Kind switch
    {
        SpendKind.Model => string.IsNullOrWhiteSpace(entry.Step) ? null : entry.Step.ToUpperInvariant(),
        _ => KindWord(entry.Kind),
    };

    /// <summary>The model that answered or transcribed; for a voice charge, the provider that spoke.</summary>
    public static string ModelText(SpendEntry entry) =>
        entry.Kind == SpendKind.Voice || string.IsNullOrWhiteSpace(entry.Model)
            ? ProviderName(entry.Kind, entry.ProviderId)
            : entry.Model;

    /// <summary>The provider under the model; null for a voice charge, which shows the provider alone.</summary>
    public static string? ProviderLine(SpendEntry entry) =>
        entry.Kind == SpendKind.Voice || string.IsNullOrWhiteSpace(entry.Model) ? null : ProviderName(entry.Kind, entry.ProviderId);

    /// <summary>"48,210 in · 9,804 out" for a model call, "938 characters" for speech, "3.4 s of audio" for transcription.</summary>
    public static string UsedText(SpendEntry entry) => entry.Kind switch
    {
        SpendKind.Model => $"{Count(entry.InputTokens)} in · {Count(entry.OutputTokens)} out",
        SpendKind.Voice when entry.Characters == 0 && entry.AudioSeconds is { } seconds => AudioText(seconds),
        SpendKind.Voice => Plural(entry.Characters, "character"),
        _ => AudioText(entry.AudioSeconds ?? 0d),
    };

    /// <summary>A model call's cache reads, cache writes and web searches, "12,100 cache read · 2,480 written"; null when it had none.</summary>
    public static string? UsedDetail(SpendEntry entry)
    {
        if (entry.Kind != SpendKind.Model)
        {
            return null;
        }

        var parts = new List<string>();

        if (entry.CacheReadTokens > 0)
        {
            parts.Add($"{Count(entry.CacheReadTokens)} cache read");
        }

        if (entry.CacheWriteTokens > 0)
        {
            parts.Add($"{Count(entry.CacheWriteTokens)} {(parts.Count > 0 ? "written" : "cache written")}");
        }

        if (entry.WebSearchRequests > 0)
        {
            parts.Add(Plural(entry.WebSearchRequests, "web search", "web searches"));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>A charge's cost to four places, or "no rate".</summary>
    public static string ChargeCostText(SpendEntry entry) => entry.Priced ? Money(entry.Dollars) : NoRate;

    /// <summary>"Spoken lines are left out: 3,930 charges, $12.4020.", or "are shown" when they are listed.</summary>
    public static string SpokenText(SpendPurposeShare spoken, bool shown) =>
        $"Spoken lines are {(shown ? "shown" : "left out")}: {ChargesText(spoken.Charges)}, {(spoken.Charges == 0 ? Money(0m) : CostText(spoken))}.";

    /// <summary>"412 charges for Conversation".</summary>
    public static string FilteredText(SpendPurposeShare purpose) => $"{ChargesText(purpose.Charges)} for {NameOf(purpose)}";

    private static string AudioText(double seconds) => $"{seconds.ToString("0.0", CultureInfo.CurrentCulture)} s of audio";

    private static string Count(long count) => count.ToString("N0", CultureInfo.CurrentCulture);

    private static string Plural(long count, string one, string? many = null) =>
        $"{Count(count)} {(count == 1 ? one : many ?? one + "s")}";

    private static string Money(decimal dollars) => dollars.ToString("C4", CultureInfo.CurrentCulture);
}
