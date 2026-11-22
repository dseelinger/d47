using Avalonia;
using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers: one ledger's page in a scroller. The ledgers are the page sidebar's items.</summary>
public sealed class LedgersPage : UserControl, IPageSummary
{
    public const string RootKey = "ledgers";

    public const string SummaryText = "Your record from the journals, one ledger at a time.";

    private static readonly (string Key, string Description)[] Known =
    [
        (SessionPage.RootKey, "What you have earned and done since the game started."),
        (CombatStatsPage.PageKey, "Your kills and the credits they were awarded."),
        (CreditsPage.PageKey, "Where your credits came from and where they went."),
        (CodexPage.PageKey, "Every kind of object in the Codex, and the ones you have found."),
        (ScreenshotsPage.PageKey, ScreenshotsPage.SummaryText),
    ];

    /// <summary>The ledgers in the page sidebar's order.</summary>
    public static IReadOnlyList<string> Order { get; } = [.. Known.Select(known => known.Key)];

    private readonly string _summary;

    public LedgersPage(Control ledger, string key)
    {
        _summary = Known.FirstOrDefault(known => known.Key == key).Description ?? SummaryText;

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(ledger);
    }

    /// <summary>A ledger's crumb: the level that replaces the ledger open under Ledgers.</summary>
    public static NavCrumb Ledger(string key, string word) => new(key, word) { Whole = true, Level = RootKey };

    public static NavCrumb Root => new(RootKey, "Ledgers")
    {
        Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
    };

    public string Summary => _summary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }
}
