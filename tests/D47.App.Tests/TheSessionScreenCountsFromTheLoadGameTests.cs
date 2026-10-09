using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › This session: earnings by source and the session's figures since the game started (#554).</summary>
public class TheSessionScreenCountsFromTheLoadGameTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 16, 0, 0, TimeSpan.Zero);

    private static readonly (TimeSpan At, string Json)[] Flight =
    [
        (TimeSpan.Zero, """{"event":"LoadGame","Credits":4203881002}"""),
        (TimeSpan.FromMinutes(5), """{"event":"FSDJump","JumpDist":20.25}"""),
        (TimeSpan.FromMinutes(9), """{"event":"FSDJump","JumpDist":12.5}"""),
        (TimeSpan.FromMinutes(30), """{"event":"Bounty","TotalReward":48210400}"""),
        (TimeSpan.FromMinutes(60), """{"event":"MarketSell","TotalSale":12904000}"""),
        (TimeSpan.FromMinutes(90), """{"event":"RedeemVoucher","Amount":2110000}"""),
        (new TimeSpan(3, 42, 10), """{"event":"Scan"}"""),
    ];

    private static JournalEvent Event(TimeSpan at, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Start + at, root.GetProperty("event").GetString()!, root);
    }

    private static Color Published(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static (Window Window, PanelView Panel, CommanderGameState State) Open(params (TimeSpan At, string Json)[] events)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TempFolders.Create("d47-session-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var (at, json) in events)
        {
            state.Apply(Event(at, json));
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCommanderName(() => state.Identity.Name);
        panel.EnableStanding(() => state);
        panel.EnableStatistics(() => state);
        panel.EnableSession(() => state);
        panel.EnableChecklist(checklists);

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.SelectRoot(PanelTab.Commander, SessionPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel, state);
    }

    private static TextBlock Text(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    private static bool Shows(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    [AvaloniaFact]
    public void ThisSessionIsTheCommanderTabsThirdRoot()
    {
        var (window, panel, _) = Open(Flight);

        var roots = panel.Nav.Roots(PanelTab.Commander);
        Assert.Equal(SessionPage.RootKey, roots[2].Key);
        Assert.Equal("This session", roots[2].Word);

        window.Close();
    }

    [AvaloniaFact]
    public void EarnedIsTheTotalOfEverySource()
    {
        var (window, panel, _) = Open(Flight);

        Assert.True(Shows(panel, "EARNED"));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "63,224,400 CR").Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void TheEightFiguresReadFromTheSession()
    {
        var (window, panel, _) = Open(Flight);

        Assert.True(Shows(panel, "3:42:10"));
        Assert.True(Shows(panel, "2"));
        Assert.True(Shows(panel, "32.8 LY"));
        Assert.True(Shows(panel, "1"));
        Assert.True(Shows(panel, "4,203,881,002 CR"));
        Assert.True(Shows(panel, "BALANCE AT START"));
        Assert.True(Shows(panel, "MATERIALS GAINED"));

        window.Close();
    }

    [AvaloniaFact]
    public void AZeroSourceIsGreyAndTheLargestFillsItsGauge()
    {
        var (window, panel, _) = Open(Flight);

        Assert.Equal(Published(ThemeManager.GreyKey), Ink(Text(panel, "0 CR").Foreground));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "48,210,400 CR").Foreground));

        var row = Text(panel, "Bounties").GetVisualParent<Grid>()!;
        var gauge = row.Children.OfType<Grid>().Single();
        Assert.Equal(1, gauge.ColumnDefinitions[0].Width.Value);

        var trade = Text(panel, "Trade").GetVisualParent<Grid>()!.Children.OfType<Grid>().Single();
        Assert.Equal(12904000.0 / 48210400, trade.ColumnDefinitions[0].Width.Value, 6);

        window.Close();
    }

    [AvaloniaFact]
    public void BeforeTheGameLoadsThePageSaysSoAndDrawsNoFigures()
    {
        var (window, panel, _) = Open();

        Assert.True(Shows(panel, SessionPage.NotStarted));
        Assert.False(Shows(panel, "EARNED"));
        Assert.False(Shows(panel, "TIME PLAYED"));

        window.Close();
    }

    [AvaloniaFact]
    public void ANewLoadGameStartsTheSessionAgainOnTheTick()
    {
        var (window, panel, state) = Open(Flight);

        Assert.False(panel.TickCommander());

        state.Apply(Event(TimeSpan.FromHours(5), """{"event":"LoadGame","Credits":4267105402}"""));

        Assert.True(panel.TickCommander());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(panel, "4,267,105,402 CR"));
        Assert.True(Shows(panel, "0:00:00"));
        Assert.False(Shows(panel, "48,210,400 CR"));
        Assert.False(panel.TickCommander());

        window.Close();
    }

    [AvaloniaFact]
    public void ThereIsNoResetControlAndTheFooterCarriesAPhrase()
    {
        var (window, panel, _) = Open(Flight);

        Assert.True(Shows(panel, SessionPage.Hint));
        Assert.True(Shows(panel, $"Say: “{SessionPage.Phrase}”"));
        Assert.Contains("this session", SessionPage.Phrase, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text?.Contains("RESET", StringComparison.OrdinalIgnoreCase) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSessionScreenIsCaptured()
    {
        var (window, _, _) = Open(Flight);

        using var frame = window.CaptureRenderedFrame()!;
        var path = "commander-session.png";
        frame.SaveCapture(path);

        window.Close();
    }
}
