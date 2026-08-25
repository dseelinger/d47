using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Where to buy everything a build still needs, drawn (list.md Phase 50).
/// <para>
/// <b>A page on the Checklist tab rather than on Routing.</b> The Commander is looking at what they
/// owe, and <em>where to get it</em> belongs beside <em>what is left</em> — and a panel page costs
/// no tool-surface bytes at all, which is what let this half be generous where the spoken half had
/// 103 to spend.
/// </para>
/// <para>
/// <b>Driven through the drawn page</b>, because the arithmetic being right and the page showing it
/// are two claims and only one of them is what a Commander looks at.
/// </para>
/// </summary>
public class TheSourcingPageTests
{
    private sealed class FakeTrade : ITradePlanService
    {
        public SourcingSearch? Last { get; private set; }

        public SourcingAnswer Answer { get; set; } = SourcingAnswer.Empty;

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(CommodityAnswer.Empty);

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search, CancellationToken cancellationToken)
        {
            Last = search;

            return Task.FromResult(Answer);
        }
    }

    /// <summary>Secrets are not this page's business; the settings service simply wants one.</summary>
    private sealed class Plain : D47.Core.Configuration.ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public bool TryUnprotect(
            byte[] ciphertext,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? plaintext)
        {
            plaintext = ciphertext;

            return true;
        }
    }

    private sealed record Surface(
        Window Window, PanelView Panel, FakeTrade Trade, CarrierManifest Carrier, SourcingBoard Board);

    private const string Depot =
        """
        { "timestamp":"2026-08-25T10:00:00Z", "event":"ColonisationConstructionDepot",
          "MarketID":3960809986, "ConstructionProgress":0.25,
          "ConstructionComplete":false, "ConstructionFailed":false,
          "ResourcesRequired":[
            { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
              "RequiredAmount":500, "ProvidedAmount":100, "Payment":3239 },
            { "Name":"$steel_name;", "Name_Localised":"Steel",
              "RequiredAmount":300, "ProvidedAmount":0, "Payment":5000 } ] }
        """;

    private static Surface Open(SourcingAnswer? answer = null, bool lookups = true)
    {
        var root = TempFolders.Create("d47-sourcing-page-tests");

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-25T09:30:00Z","event":"Docked","StationName":"Ratraii Construction Site","StarSystem":"Ratraii","MarketID":3960809986}""",
                     Depot.ReplaceLineEndings(" "),
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active;
        var trade = new FakeTrade { Answer = answer ?? SourcingAnswer.Empty };
        var board = new SourcingBoard();

        var carrier = new CarrierManifest(
            Path.Combine(root, "carrier.json"), NullLogger<CarrierManifest>.Instance);

        var paths = new D47.Core.AppPaths(root);

        paths.EnsureCreated();

        var settingsStore = new SettingsStore(paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            settingsStore,
            new SecretStore(paths, new Plain(), NullLogger<SecretStore>.Instance),
            settingsStore.Load(),
            NullLogger<SettingsService>.Instance);

        settings.Replace(
            "the test",
            current => current with { Knowledge = current.Knowledge with { GalaxySearch = lookups } });

        var registry = CapabilityRegistry.Build(
        [
            ColonisationCapability.Create(
                () => live,
                null,
                settings,
                trade,
                carrier,
                board,
                () => new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)),
        ]);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(
            checklists,
            null,
            null,
            () => new SourcingPage(registry, board, carrier, () => live, () => settings.Current.Knowledge.GalaxySearch));

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, trade, carrier, board);
    }

    private static IReadOnlyList<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>One of the carrier form's boxes, by the example in it.</summary>
    private static TextBox Box(PanelView panel, string placeholder) =>
        panel.GetVisualDescendants().OfType<TextBox>()
            .First(box => box.PlaceholderText == placeholder);

    private static Button Press(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == label
                             || button.GetVisualDescendants().OfType<TextBlock>()
                                 .Any(text => text.Text == label));

    /// <summary>
    /// Onto the Sourcing root. Through the navigator, which is the one path the mode chooser, the
    /// spoken phrase and this all go down — a reading reached by a press and one reached by a word
    /// are the same path rather than two that have to agree.
    /// </summary>
    private static void Open(Surface surface)
    {
        Assert.True(surface.Panel.Nav.SelectRoot(SourcingPage.RootKey));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The root is on the Checklist tab, beside the checklist itself — as a second reading rather
    /// than a second tab, which is the same arrangement Loadout's three roots and Routing's four
    /// already make.
    /// </summary>
    [AvaloniaFact]
    public void TheTabHasASourcingRoot()
    {
        var surface = Open();

        Assert.Contains(
            surface.Panel.Nav.Roots(PanelTab.Checklist),
            root => root.Key == SourcingPage.RootKey && root.Word == "Sourcing");

        surface.Window.Close();
    }

    /// <summary>
    /// <b>Desktop only, by not making the call</b> — the carrier figure is typed, and typing wants a
    /// keyboard the headset has not got. That is the parity rule working as written rather than an
    /// exception to it, and it means no code anywhere tests which surface this is.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceThatDoesNotFurnishItDoesNotGetIt()
    {
        var root = TempFolders.Create("d47-sourcing-parity-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();


        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            panel.Nav.Roots(PanelTab.Checklist),
            root => root.Key == SourcingPage.RootKey);

        // And the navigator declines to select a root nobody furnished, which is what stops a
        // stale phrase putting a surface on an empty pane with no way back.
        Assert.False(panel.Nav.SelectRoot(SourcingPage.RootKey));

        window.Close();
    }

    /// <summary>The build it is about, and the caveat every colonisation figure carries.</summary>
    [AvaloniaFact]
    public void ThePageNamesTheSiteAndSaysHowFreshItIs()
    {
        var surface = Open();

        Open(surface);

        var drawn = Drawn(surface.Panel);

        Assert.Contains(drawn, said => said.Contains("Ratraii", StringComparison.Ordinal)
                                       && said.Contains("2 commodities outstanding", StringComparison.Ordinal));

        Assert.Contains(drawn, said => said.Contains("not live", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// The button invokes the tool through the registry — the same path the model takes and the
    /// same path the keyword router takes — and the table is what the capability posted on its way
    /// out. One answer, not two searches that could disagree.
    /// </summary>
    [AvaloniaFact]
    public void PressingItDrawsTheAnswerTheCapabilityPosted()
    {
        var surface = Open(new SourcingAnswer(
            new SourcingPlan(
                [
                    new SourcingStop(
                        new MarketSnapshot
                        {
                            Station = "Hutton Orbital",
                            System = "Alpha Centauri",
                            UpdatedAt = DateTimeOffset.UnixEpoch,
                        },
                        [new SourcingLot("Aluminium", "aluminium", 400, 300)],
                        14.5),
                ],
                ["Steel"],
                new Dictionary<string, int>(StringComparer.Ordinal)),
            12,
            0,
            true));

        Open(surface);

        Press(surface.Panel, "Where to buy it").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(surface.Panel);

        Assert.NotNull(surface.Trade.Last);
        Assert.Contains(drawn, said => said.Contains("Hutton Orbital", StringComparison.Ordinal));
        Assert.Contains(drawn, said => said.Contains("400 t", StringComparison.Ordinal));

        // Nothing is dropped in silence, on the page as much as in the sentence.
        Assert.Contains(drawn, said => said.Contains("Nothing in range prices: Steel", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// The carrier is told here and nowhere else, and it comes off what gets searched for. What the
    /// site itself owes is untouched, which is the line this feature must not cross.
    /// </summary>
    [AvaloniaFact]
    public void TypingACarrierFigureTakesItOffTheShoppingList()
    {
        var surface = Open();

        Open(surface);

        Box(surface.Panel, "Tritium").Text = "Steel";
        Box(surface.Panel, "how many").Text = "100";

        Press(surface.Panel, "It's aboard").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var stock = Assert.Single(surface.Carrier.For("F1"));

        Assert.Equal("Steel", stock.Commodity);
        Assert.Equal(100, stock.Tonnes);

        // Drawn back with the date on it, because this is the one figure d47 cannot check.
        Assert.Contains(Drawn(surface.Panel), said => said.Contains("you said so", StringComparison.Ordinal));

        Press(surface.Panel, "Where to buy it").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(surface.Trade.Last);
        Assert.Equal(200, surface.Trade.Last.Outstanding.Single(row => row.Name == "Steel").Remaining);

        surface.Window.Close();
    }

    /// <summary>Asking where to buy leaves the machine, so it is behind the same switch as the rest.</summary>
    [AvaloniaFact]
    public void WithLookupsOffThePageSaysSoAndOffersNoSearch()
    {
        var surface = Open(lookups: false);

        Open(surface);

        Assert.Contains(Drawn(surface.Panel), said => said.Contains("switched off", StringComparison.Ordinal));

        Assert.DoesNotContain(
            surface.Panel.GetVisualDescendants().OfType<Button>(),
            button => button.Content as string == "Where to buy it");

        surface.Window.Close();
    }
}
