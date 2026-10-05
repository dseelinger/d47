using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Journal page's right pane draws the selected event as a reading (#817).</summary>
public sealed class TheJournalPaneReadsTheEventTests
{
    private const string Docked =
        """{"timestamp":"2026-09-29T21:01:23Z","event":"Docked","StationName":"BNH-T2F","StationType":"FleetCarrier","Taxi":false,"Multicrew":false,"StarSystem":"LTT 7786","SystemAddress":633608311522,"MarketID":3715429376,"StationFaction":{"Name":"FleetCarrier"},"StationGovernment":"$government_Carrier;","StationGovernment_Localised":"Private Ownership","StationServices":["dock","autodock","commodities","contacts","crewlounge","rearm","refuel","repair","engineer","flightcontroller","stationoperations","stationMenu","carriermanagement","carrierfuel","socialspace"],"StationEconomy":"$economy_Carrier;","StationEconomy_Localised":"Private Enterprise","StationEconomies":[{"Name":"$economy_Carrier;","Name_Localised":"Private Enterprise","Proportion":1.0}],"DistFromStarLS":213.28735,"LandingPads":{"Small":4,"Medium":4,"Large":8}}""";

    private const string EngineerCraft =
        """{"timestamp":"2026-09-20T00:38:02Z","event":"EngineerCraft","Slot":"MainEngines","Module":"int_engine_size4_class5","ApplyExperimentalEffect":"special_engine_overloaded","Ingredients":[{"Name":"iron","Count":5},{"Name":"hybridcapacitors","Name_Localised":"Hybrid Capacitors","Count":3}],"Engineer":"Liz Ryder","EngineerID":300080,"BlueprintID":128673659,"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,"ExperimentalEffect":"special_engine_overloaded","ExperimentalEffect_Localised":"Drag Drives"}""";

    private static JournalEntry Entry(string json, FoldReceipt? receipt = null)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var log = new JournalLog();

        log.Add(
            [new JournalEvent(
                DateTimeOffset.Parse(root.GetProperty("timestamp").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                root.GetProperty("event").GetString()!,
                root.Clone())],
            receipt is null ? [] : [receipt]);

        return Assert.Single(log.Read(noise: true));
    }

    private static (JournalReadingPane Pane, Window Window) Shown(
        string json, double width = 650, string? here = null, Func<ReadingLink, Action?>? open = null, FoldReceipt? receipt = null)
    {
        var pane = new JournalReadingPane(open ?? (_ => null), () => here);
        var window = new Window
        {
            Content = new ScrollViewer { Content = pane, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
            Width = width,
            Height = 1000,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        pane.Show(Entry(json, receipt));
        Dispatcher.UIThread.RunJobs();

        return (pane, window);
    }

    private static string Said(TextBlock block) => block.Inlines?.Text ?? block.Text ?? string.Empty;

    private static TextBlock Block(Control pane, string text) =>
        pane.GetVisualDescendants().OfType<TextBlock>().First(block => Said(block) == text);

    private static IBrush? Role(string key) => (IBrush?)Application.Current!.Resources[key];

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, name), new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void ADockingDrawsItsHeadlineTimeRowsParagraphAndFoldedFields()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        var said = pane.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(Said).ToList();

        Assert.Equal("Docked at BNH-T2F, LTT 7786", Said(pane.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "ReadingHeadline")));
        Assert.Matches(@"^\d\d:\d\d · \d+ days? ago$", Said(pane.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "ReadingTime")));
        Assert.Contains("STATION", said);
        Assert.Contains("RUN BY", said);
        Assert.Contains("LANDING PADS", said);
        Assert.Contains("WHAT THIS MEANS", said);
        Assert.Contains("EVERY FIELD", said);
        Assert.True(pane.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "ReadingParagraph").IsEffectivelyVisible);
        Assert.False(pane.GetLogicalDescendants().OfType<StackPanel>().Single(panel => panel.Name == "ReadingFields").IsVisible);

        Capture(window, "journal-reading-docked.png");

        window.Close();
    }

    private static readonly FoldReceipt DockedReceipt = new([new FoldChange("Docked", "docked at BNH-T2F")]);

    private static IEnumerable<string> Order(JournalReadingPane pane) =>
        pane.GetVisualDescendants().OfType<Button>().Select(button => button.Name ?? string.Empty)
            .Where(name => name is "ReadingMeaning" or "ReadingChanged" or "ReadingEveryField");

    [AvaloniaFact]
    public void TheReceiptBandSitsBetweenWhatThisMeansAndEveryFieldOpen()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked, receipt: DockedReceipt);

        Assert.Equal(["ReadingMeaning", "ReadingChanged", "ReadingEveryField"], Order(pane));

        var body = pane.GetVisualDescendants().OfType<SelectableTextBlock>().Single(block => block.Name == "ReadingReceipt");
        Assert.True(body.IsEffectivelyVisible);
        Assert.Equal(DockedReceipt.Said, body.Text);
        Assert.Contains("WHAT THIS CHANGED", pane.GetVisualDescendants().OfType<TextBlock>().Select(Said));

        Capture(window, "journal-reading-docked-receipt.png");

        window.Close();
    }

    [AvaloniaFact]
    public void FoldingTheReceiptBandHidesItsTextAndANewSelectionOpensItAgain()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked, receipt: DockedReceipt);

        Press(pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingChanged"));
        Assert.False(pane.GetVisualDescendants().OfType<SelectableTextBlock>().Single(block => block.Name == "ReadingReceipt").IsVisible);

        pane.Show(Entry(EngineerCraft, DockedReceipt));
        Dispatcher.UIThread.RunJobs();
        Assert.True(pane.GetVisualDescendants().OfType<SelectableTextBlock>().Single(block => block.Name == "ReadingReceipt").IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void AMusicEntryReadsThatNothingInThePictureChanged()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"Music","MusicTrack":"Exploration"}""",
            receipt: FoldReceipt.Nothing);

        Assert.Equal(
            "Nothing in d47's picture changed.",
            pane.GetVisualDescendants().OfType<SelectableTextBlock>().Single(block => block.Name == "ReadingReceipt").Text);

        window.Close();
    }

    [AvaloniaFact]
    public void AnEntryWithNoReceiptDrawsNoBand()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        Assert.DoesNotContain(pane.GetVisualDescendants().OfType<Button>(), button => button.Name == "ReadingChanged");
        Assert.DoesNotContain(pane.GetVisualDescendants().OfType<TextBlock>(), block => Said(block) == "WHAT THIS CHANGED");

        window.Close();
    }

    [AvaloniaFact]
    public void EveryFieldOpensToTheFieldsInFileOrderAsSelectableText()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        Press(pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingEveryField"));

        var fields = pane.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "ReadingFields");
        var names = fields.GetVisualDescendants().OfType<SelectableTextBlock>().Select(block => block.Text).ToList();

        Assert.True(fields.IsVisible);
        Assert.Equal("timestamp", names[0]);
        Assert.Equal("event", names[2]);
        Assert.Contains("FleetCarrier", names);

        Capture(window, "journal-reading-docked-fields.png");

        window.Close();
    }

    [AvaloniaFact]
    public void AKindWithNoParagraphHasNoBand()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown("""{"timestamp":"2026-09-29T21:01:23Z","event":"Music","MusicTrack":"Exploration"}""");

        Assert.Null(JournalExplainers.For("Music"));
        Assert.DoesNotContain(pane.GetVisualDescendants().OfType<Button>(), button => button.Name == "ReadingMeaning");
        Assert.Contains(pane.GetVisualDescendants().OfType<Button>(), button => button.Name == "ReadingEveryField");
        Assert.Contains(pane.GetVisualDescendants().OfType<TextBlock>(), block => Said(block) == "Exploration");

        window.Close();
    }

    [AvaloniaFact]
    public void APlayersMessageIsDrawnExactlyInTheMutedRole()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"ReceiveText","From":"Player One","Message":"look **here** $foo;","Channel":"local"}""");

        var message = Block(pane, "look **here** $foo;");

        Assert.Contains("typed", message.Classes);
        Assert.Same(Role(ThemeManager.GreyKey), message.Foreground);

        Capture(window, "journal-reading-receivetext.png");

        window.Close();
    }

    [AvaloniaFact]
    public void ADockingWhileWantedSaysSoFirstFullWidthInRed()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked.Replace("\"Taxi\"", "\"Wanted\":true,\"Taxi\"", StringComparison.Ordinal));

        var first = pane.GetVisualDescendants().OfType<Grid>().First();
        var wanted = Block(pane, "You are wanted here.");

        Assert.Empty(first.ColumnDefinitions);
        Assert.Contains(wanted, first.GetVisualDescendants());
        Assert.Same(Role(ThemeManager.RedKey), wanted.Foreground);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSystemIsCyanOnlyWhereTheCommanderIsNow()
    {
        using var look = AppLook.Put();
        const string jump =
            """{"timestamp":"2026-09-28T14:05:35Z","event":"FSDJump","StarSystem":"Kokoimudji","SystemAddress":2870783124945,"JumpDist":77.985}""";

        var (here, window) = Shown(jump, here: "Kokoimudji");
        Assert.Same(Role(ThemeManager.CyanKey), Block(here, "Kokoimudji").Foreground);
        window.Close();

        var (elsewhere, other) = Shown(jump, here: "Sol");
        Assert.Same(Role(ThemeManager.WhiteKey), Block(elsewhere, "Kokoimudji").Foreground);
        other.Close();
    }

    [AvaloniaFact]
    public void ALongRowShowsSixAndFoldsTheRestBehindACount()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        var more = pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingMore");
        var row = (WrapPanel)more.GetVisualParent()!;

        Assert.Equal(JournalReadingPane.ValuesShown + 1, row.Children.Count);
        Assert.StartsWith("+", Said(more.GetVisualDescendants().OfType<TextBlock>().Single()), StringComparison.Ordinal);

        Press(more);

        var fewer = pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingMore");
        Assert.Equal("FEWER", Said(fewer.GetVisualDescendants().OfType<TextBlock>().Single()));
        Assert.True(row.Children.Count > JournalReadingPane.ValuesShown + 1);

        // A new selection starts folded again.
        pane.Show(Entry(Docked));
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("+", Said(pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingMore")
            .GetVisualDescendants().OfType<TextBlock>().Single()), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void ANarrowPanePutsEachLabelAboveItsValue()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(EngineerCraft, width: 325, open: _ => () => { });

        Assert.True(pane.Narrow);
        Capture(window, "journal-reading-engineercraft-narrow.png");
        window.Close();

        var (wide, other) = Shown(EngineerCraft, open: _ => () => { });
        Assert.False(wide.Narrow);
        Capture(other, "journal-reading-engineercraft.png");
        other.Close();
    }

    [AvaloniaFact]
    public void ALabelWithAKnownFieldExplainsItselfOnHover()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        var labels = pane.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Cursor is not null).ToList();

        Assert.NotEmpty(labels);

        var label = labels[0];
        window.MouseMove(label.TranslatePoint(new Point(4, 4), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Role(ThemeManager.WhiteKey), label.Foreground);
        Assert.Contains(pane.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>(), popup => popup.IsOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheEngineerOpensTheirPageOnTheEngineersTab()
    {
        var surface = Furnished();

        surface.Show(Entry(EngineerCraft));

        var link = surface.Panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingLink");
        Press(link);

        Assert.Equal(PanelTab.Assets, surface.Panel.Nav.Tab);
        Assert.Equal(EngineersPages.DirectoryRoot, surface.Panel.Nav.Root.Key);
        Assert.Equal("Liz Ryder", surface.Panel.Nav.Trail[^1].Word);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AShipInTheFleetLinksToItsPageAndOneSoldIsPlainText()
    {
        var surface = Furnished();

        surface.Show(Entry("""{"timestamp":"2026-09-29T21:01:23Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[]}"""));

        var link = surface.Panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingLink");
        Press(link);

        Assert.Equal(PanelTab.Assets, surface.Panel.Nav.Tab);
        Assert.Equal(LoadoutPages.FleetRoot, surface.Panel.Nav.Root.Key);
        Assert.Equal(LoadoutPages.ShipPrefix + "new:12", surface.Panel.Nav.Trail[^1].Key);

        surface.Panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();
        surface.Show(Entry("""{"timestamp":"2026-09-29T21:01:23Z","event":"Loadout","Ship":"anaconda","ShipID":99,"Modules":[]}"""));

        Assert.DoesNotContain(surface.Panel.GetVisualDescendants().OfType<Button>(), button => button.Name == "ReadingLink");
        Assert.Contains(surface.Panel.GetVisualDescendants().OfType<TextBlock>(), block => Said(block) == "Anaconda");

        surface.Window.Close();
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private sealed record Surface(Window Window, PanelView Panel)
    {
        /// <summary>Selects the entry on the Journal page.</summary>
        public void Show(JournalEntry entry)
        {
            var model = (PanelViewModel)Panel.DataContext!;

            model.JournalSource = _ => [entry];
            Panel.Page = TranscriptPage.Journal;
            model.RefreshJournal();
            Panel.ShowJournalNoise(true);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>A panel with the Engineers and Ships tabs, for a Commander flying ship 12.</summary>
    private static Surface Furnished()
    {
        var root = TempFolders.Create("d47-journal-pane-tests");
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var state = store.Active!;
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => state);
        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state);
        panel.EnableEngineers(unlocks, ships, () => state);

        var window = new Window { Content = panel, Width = 1180, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel);
    }
}
