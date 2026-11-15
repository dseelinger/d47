using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Campaign: the CAMPAIGN ON switch and the page while the campaign is off (#2438).</summary>
public class TheCampaignPageTurnsTheCampaignOffAndOnTests
{
    private const string Frontier = "F2438";
    private const string Squadron = "Kestrel Reach Compact";

    private sealed class Fixture
    {
        public CampaignStore Store { get; } = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

        public CommanderGameState State { get; } = JournalScript.Commander(
            Frontier,
            "Kade Orrin",
            """{ "timestamp":"2026-11-09T17:30:00Z", "event":"FSDJump", "StarSystem":"Arque", "SystemAddress":129, "StarPos":[0,0,0] }""");

        public CapabilityRegistry Registry { get; }

        public List<ToolInvocation> Invoked { get; } = [];

        public string? SquadronFaction { get; set; } = Squadron;

        public Fixture()
        {
            Registry = CapabilityRegistry.Build([CampaignCapability.Create(Store, () => State)]);
            Registry.ToolInvoked += Invoked.Add;
        }

        public CampaignSources Sources => new(() => State, Store, () => null, () => SquadronFaction, () => null, Registry);
    }

    private static (Window Window, CampaignPage Page) Open(Fixture fixture, bool headset = false)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new CampaignPage(fixture.Sources, headset: headset);
        var window = new Window
        {
            Content = page,
            Width = headset ? AppLook.CaptureWidth : 1280,
            Height = headset ? AppLook.CaptureHeight : 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static CheckBox Switch(Control root) => In<CheckBox>(root).Single(box => box.Name == CampaignPage.SwitchName);

    private static Button Change(Control root) => In<Button>(root).Single(button => button.Name == CampaignPage.ChangeName);

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static void Press(Control control)
    {
        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void WhileOffNoSurfaceAsksForAFactionWhetherOrNotOneIsKnown()
    {
        Assert.Null(CampaignModel.NoFactionLine(null, off: true));
        Assert.Null(CampaignModel.NoFactionLine(Squadron, off: true));
        Assert.Equal(CampaignModel.NoFactionText, CampaignModel.NoFactionLine(null, off: false));
        Assert.Null(CampaignModel.NoFactionLine(Squadron, off: false));
        Assert.Equal("CHANGE FACTION", CampaignModel.FactionTileLabel(null, off: true));
        Assert.Equal("CHOOSE FACTION", CampaignModel.FactionTileLabel(null, off: false));
        Assert.Equal("CHANGE FACTION", CampaignModel.FactionTileLabel(Squadron, off: false));
        Assert.Equal("Your campaign is off.", CampaignModel.Summary(null, off: true, headset: false));
        Assert.Equal($"Your campaign for {Squadron} is off.", CampaignModel.Summary(Squadron, off: true, headset: true));
    }

    [Fact]
    public void AnOffCampaignResumesWithTheNamedFactionElseTheSquadrons()
    {
        Assert.Equal("Lave", CampaignModel.OffFaction(Campaign.None with { Faction = "Lave" }, Squadron));
        Assert.Equal(Squadron, CampaignModel.OffFaction(Campaign.None, Squadron));
        Assert.Null(CampaignModel.OffFaction(Campaign.None, null));
        Assert.StartsWith("Your campaign for Lave is off. D47 fetches no faction report", CampaignModel.OffText("Lave"), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheSwitchIsTickedWhileTheCampaignRuns()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);

        Assert.Equal("CAMPAIGN ON", Switch(page).Content);
        Assert.True(Switch(page).IsChecked);
        Assert.True(Switch(page).IsEnabled);
        Assert.DoesNotContain(In<Control>(page), control => control.Name == CampaignPage.OffName);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheSwitchTurnsTheCampaignOffAsTheCommanderAndOnAgain()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);

        Press(Switch(page));

        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.SwitchTool, ToolCaller.Commander, true), (call.Tool, call.Caller, call.Succeeded));
        Assert.True(fixture.Store.For(Frontier).Off);
        Assert.False(Switch(page).IsChecked);
        Assert.Contains(In<Control>(page), control => control.Name == CampaignPage.OffName);

        Press(Switch(page));

        Assert.False(fixture.Store.For(Frontier).Off);
        Assert.True(Switch(page).IsChecked);
        Assert.DoesNotContain(In<Control>(page), control => control.Name == CampaignPage.OffName);

        window.Close();
    }

    [AvaloniaFact]
    public void WhileOffThePageSaysSoKeepsTheFactionAndDrawsNoSections()
    {
        var fixture = new Fixture();
        fixture.Store.SetOff(Frontier, true);
        var (window, page) = Open(fixture);

        Assert.Equal($"Your campaign for {Squadron} is off.", page.Summary);
        Assert.True(Shows(page, Squadron));
        Assert.True(Shows(page, CampaignModel.NotWhileOff));
        Assert.True(Shows(page, CampaignModel.OffLabel));
        Assert.True(Shows(page, CampaignModel.OffText(Squadron)));
        Assert.True(Shows(page, CampaignModel.OffKept));
        Assert.True(Shows(page, CampaignModel.OffHint));
        Assert.False(Switch(page).IsChecked);
        Assert.False(Change(page).IsEnabled);
        Assert.False(page.Filters);
        Assert.DoesNotContain(In<Control>(page), control => control.Name == CampaignPage.SectionsName);
        Assert.DoesNotContain(In<Control>(page), control => control.Name is CampaignPage.ChosenName or CampaignPage.ProposedName or CampaignPage.NoticeName);

        window.Close();
    }

    [AvaloniaFact]
    public void WhileOffWithNoFactionKnownThePageNeverAsksToChooseOne()
    {
        var fixture = new Fixture { SquadronFaction = null };
        fixture.Store.SetOff(Frontier, true);
        var (window, page) = Open(fixture);

        Assert.Equal("Your campaign is off.", page.Summary);
        Assert.True(Shows(page, "None"));
        Assert.True(Shows(page, CampaignModel.OffText(null)));
        Assert.False(Shows(page, CampaignModel.NoFactionText));
        Assert.False(Shows(page, "Choose the faction your campaign works for."));
        Assert.Equal("CHANGE FACTION", Change(page).Content);
        Assert.False(Change(page).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void TurningTheCampaignOffByPhraseRedrawsThePage()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);

        fixture.Store.SetOff(Frontier, true);
        Assert.True(page.Tick());

        Assert.Contains(In<Control>(page), control => control.Name == CampaignPage.OffName);
        Assert.False(Switch(page).IsChecked);
        Assert.Equal($"Your campaign for {Squadron} is off.", page.Summary);

        fixture.Store.SetOff(Frontier, false);
        Assert.True(page.Tick());

        Assert.True(Switch(page).IsChecked);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetDrawsTheOffBlockCompactAndTheSwitchAtTheTouchHeight()
    {
        var fixture = new Fixture();
        fixture.Store.SetOff(Frontier, true);
        var (window, page) = Open(fixture, headset: true);

        Assert.True(Shows(page, CampaignModel.OffLabel));
        Assert.True(Shows(page, CampaignModel.OffBody));
        Assert.True(Shows(page, CampaignModel.OffCompact));
        Assert.False(Shows(page, CampaignModel.OffText(Squadron)));
        Assert.True(Switch(page).Bounds.Height >= TypeScale.MinimumTarget);

        window.Close();
    }
}
