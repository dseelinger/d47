using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A page's open section is part of the address: every UI showing the page shows the same section (#2740).</summary>
public class ThePageSectionIsTheSameOnEveryUiTests
{
    private const string System = "search.system";

    [AvaloniaFact]
    public void ASectionChosenInTheWindowIsOpenOnTheBigPanelAndTheReverse()
    {
        var (window, big, windowPages, bigPages) = Pair(PanelMode.Full);

        window.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();

        windowPages.Single().Press("Stations");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Stations", bigPages.Single().OpenSectionId);

        bigPages.Single().Press("Bodies");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Bodies", windowPages.Single().OpenSectionId);
    }

    [AvaloniaFact]
    public void AMiniStandingInForThePageNamesTheSectionLast()
    {
        var (window, mini, windowPages, _) = Pair(PanelMode.Mini);

        window.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();
        windowPages.Single().Press("Stations");
        Dispatcher.UIThread.RunJobs();

        Assert.True(StandIns.Shows(mini));
        Assert.Equal($"SEARCH {MiniTrail.Separator} STATIONS", TrailOf(mini));
    }

    [AvaloniaFact]
    public void AUiThatBuildsThePageLaterOpensItOnTheSection()
    {
        var (window, headset, windowPages, headsetPages) = Pair(PanelMode.Mini);

        window.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();
        windowPages.Single().Press("Stations");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(headsetPages);

        headset.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Stations", headsetPages.Single().OpenSectionId);
        Assert.Equal("Stations", windowPages.Single().OpenSectionId);
    }

    [AvaloniaFact]
    public void APageWhoseDataIsNotReadyTakesTheSectionWhenItIs()
    {
        var (window, big, windowPages, bigPages) = Pair(PanelMode.Full, bigReady: false);

        window.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();
        windowPages.Single().Press("Stations");
        Dispatcher.UIThread.RunJobs();

        var page = bigPages.Single();
        Assert.Equal(string.Empty, page.OpenSectionId);

        page.BecomeReady();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Stations", page.OpenSectionId);
        Assert.Equal("Stations", windowPages.Single().OpenSectionId);
    }

    [AvaloniaFact]
    public void APageReadyBeforeAnySectionWasChosenLeavesTheOthersOnItsSection()
    {
        var (window, big, windowPages, bigPages) = Pair(PanelMode.Full, bigReady: false);

        window.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();

        bigPages.Single().BecomeReady();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Overview", window.Nav.Section?.Id);
        Assert.Equal("Overview", big.Nav.Section?.Id);
        Assert.Equal("Overview", windowPages.Single().OpenSectionId);
    }

    [AvaloniaFact]
    public void TheCampaignPageSectionIsTheSameInTheWindowAndOnTheBigPanel()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var state = JournalScript.Commander("F2740", "Tamsin Vael");
        var model = new PanelViewModel();
        var window = new PanelView { DataContext = model };
        var big = new PanelView { DataContext = model };

        foreach (var view in new[] { window, big })
        {
            view.EnableCampaign(new CampaignSources(
                () => state,
                new CampaignStore("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance),
                () => null,
                () => "Kestrel Reach Compact",
                () => null,
                CapabilityRegistry.Build([])));
        }

        new Window { Content = new StackPanel { Children = { window, big } }, Width = 900, Height = 1600 }.Show();

        var mirror = new TranscriptMirror();
        mirror.Lead(window.Nav);
        mirror.Add(big.Nav);

        window.Tab = PanelTab.Commander;
        window.Nav.SelectRoot(PanelTab.Commander, CampaignPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Segment Sections(PanelView view) =>
            view.GetVisualDescendants().OfType<Segment>().First(found => found.Name == CampaignPage.SectionsName);

        Sections(window).PressOption(CampaignModel.SystemsSection);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CampaignModel.SystemsSection, Sections(big).SelectedIndex);

        Sections(big).PressOption(CampaignModel.WorkSection);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CampaignModel.WorkSection, Sections(window).SelectedIndex);
        Assert.Equal(CampaignModel.Sections[CampaignModel.WorkSection], window.Nav.Section?.Word);
    }

    [Fact]
    public void ARootNamedAsItsTabIsNotWrittenTwice()
    {
        var trail = new[] { new NavCrumb("settings", "Settings") };

        Assert.Equal(["SETTINGS", "VOICE"], MiniTrail.Words("Settings", trail, 2, "Voice"));
    }

    /// <summary>A page with three sections that reports none until it is ready, as the system page does.</summary>
    internal sealed class SectionPage : UserControl, IPageSection
    {
        private static readonly string[] Ids = ["Overview", "Stations", "Bodies"];

        private readonly PageSectionWord _word = new();
        private string _section = Ids[0];
        private bool _ready;

        public SectionPage(bool ready)
        {
            _ready = ready;
            Report();
        }

        public string OpenSection => _word.Word;

        public string OpenSectionId => _word.Id;

        public bool SectionChosen => _word.Chosen;

        public event EventHandler? OpenSectionChanged
        {
            add => _word.Changed += value;
            remove => _word.Changed -= value;
        }

        /// <summary>The Commander presses a section on the switch.</summary>
        public void Press(string id)
        {
            _word.Choose();
            _section = id;
            Report();
        }

        public bool OpenSectionById(string id)
        {
            if (!Ids.Contains(id))
            {
                return false;
            }

            _word.Unchoose();
            _section = id;
            Report();
            return true;
        }

        public void BecomeReady()
        {
            _ready = true;
            Report();
        }

        private void Report() => _word.Set(this, _ready ? _section : string.Empty, _ready ? _section : string.Empty);
    }

    private static (PanelView Window, PanelView Other, List<SectionPage> WindowPages, List<SectionPage> OtherPages) Pair(
        PanelMode otherMode,
        bool bigReady = true)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var model = new PanelViewModel();
        var window = new PanelView { DataContext = model };
        var other = new PanelView { DataContext = model, Mode = otherMode };
        List<SectionPage> windowPages = [];
        List<SectionPage> otherPages = [];

        window.Furnish(PanelTab.Search, _ => Built(windowPages, new SectionPage(ready: true)), new NavCrumb(System, "System"));
        other.Furnish(PanelTab.Search, _ => Built(otherPages, new SectionPage(bigReady)), new NavCrumb(System, "System"));

        new Window { Content = new StackPanel { Children = { window, other } }, Width = 900, Height = 1280 }.Show();

        var mirror = new TranscriptMirror();
        mirror.Lead(window.Nav);
        mirror.Add(other.Nav);
        Dispatcher.UIThread.RunJobs();

        return (window, other, windowPages, otherPages);
    }

    private static SectionPage Built(List<SectionPage> pages, SectionPage page)
    {
        pages.Add(page);
        return page;
    }

    internal static string? TrailOf(PanelView view) =>
        AutomationProperties.GetName(view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == "MiniTrailRow"));
}

/// <summary>The Settings section is part of the address too, and Previous still restores it (#2740).</summary>
[Trait("Category", "Integration")]
public class TheSettingsSectionIsTheSameOnEveryUiTests
{
    [AvaloniaFact]
    public void ASettingsSectionChosenOnOneUiIsOpenOnTheOther()
    {
        var (window, big, windowPage, bigPage, _) = Panels(PanelMode.Full);

        window.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        windowPage().RevealPlace(Second);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Second, bigPage().OpenPlaceId);

        bigPage().RevealPlace(First);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(First, windowPage().OpenPlaceId);
    }

    [AvaloniaFact]
    public void PreviousAndNextStillRestoreTheSectionOnEveryUi()
    {
        var (window, big, windowPage, bigPage, _) = Panels(PanelMode.Full);

        window.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();
        windowPage().RevealPlace(First);
        Dispatcher.UIThread.RunJobs();
        windowPage().RevealPlace(Second);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Nav.Previous());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(First, windowPage().OpenPlaceId);
        Assert.Equal(First, bigPage().OpenPlaceId);

        Assert.True(big.Nav.Next());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Second, windowPage().OpenPlaceId);
        Assert.Equal(Second, bigPage().OpenPlaceId);
    }

    [AvaloniaFact]
    public void TheMiniOnSettingsNamesTheOpenSection()
    {
        var (window, mini, windowPage, _, title) = Panels(PanelMode.Mini);

        window.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();
        windowPage().RevealPlace(Second);
        Dispatcher.UIThread.RunJobs();

        Assert.True(StandIns.Shows(mini));
        Assert.Equal($"SETTINGS {MiniTrail.Separator} {title(Second).ToUpperInvariant()}", ThePageSectionIsTheSameOnEveryUiTests.TrailOf(mini));
    }

    private static readonly string First = SettingsLayout.Areas.SelectMany(area => area.Places).First().Id;

    private static readonly string Second = SettingsLayout.Areas.SelectMany(area => area.Places).Skip(1).First().Id;

    private static (PanelView Window, PanelView Other, Func<SettingsView> WindowPage, Func<SettingsView> OtherPage, Func<string, string> Title) Panels(
        PanelMode otherMode)
    {
        var (settings, viewState, _) = TestSurface.Create();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var model = new PanelViewModel();
        var window = new PanelView { DataContext = model };
        var other = new PanelView { DataContext = model, Mode = otherMode };
        SettingsView? windowPage = null;
        SettingsView? otherPage = null;

        window.EnableSettings(() => windowPage = Page(settings, viewState));
        other.EnableSettings(() => otherPage = Page(settings, viewState));

        new Window { Content = new StackPanel { Children = { window, other } }, Width = 1200, Height = 1600 }.Show();

        var mirror = new TranscriptMirror();
        mirror.Lead(window.Nav);
        mirror.Add(other.Nav);
        Dispatcher.UIThread.RunJobs();

        return (
            window,
            other,
            () => windowPage ?? throw new InvalidOperationException("The window has not built its settings page."),
            () => otherPage ?? throw new InvalidOperationException("The other UI has not built its settings page."),
            id => SettingsLayout.Areas.SelectMany(area => area.Places).Single(place => place.Id == id).Title);
    }

    private static SettingsView Page(SettingsService settings, ViewStateStore viewState)
    {
        var page = new SettingsView();
        page.Attach(settings, viewState);
        return page;
    }
}
