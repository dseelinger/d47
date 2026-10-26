using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Search › Systems runs search_systems from its form and draws the last answer, however it was asked (#1080).</summary>
public sealed class TheSystemsSearchPageDrawsTheLastAnswerTests
{
    private const string Fid = "F1";

    private const long Ltt7786 = 633608311522;

    private const long Hip112535 = 2869708727553;

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly GalaxySearchResult TwoSystems = new(
        "LTT 7786",
        2,
        [
            new SystemSummary
            {
                Name = "LTT 7786",
                SystemAddress = Ltt7786,
                Distance = 0,
                Allegiance = "Independent",
                Government = "Anarchy",
                PrimaryEconomy = "Industrial",
                Security = "Anarchy",
                Population = 35_261_804,
                ControllingFaction = "Diamond Frogs",
                Factions = [new FactionPresence("Diamond Frogs", 0.6) { State = "Boom" }],
                ReportedAt = AskedAt,
            },
            new SystemSummary
            {
                Name = "HIP 112535",
                SystemAddress = Hip112535,
                Distance = 8.42,
                Allegiance = "Independent",
                Government = "Anarchy",
                ControllingPower = "Archon Delaine",
                PowerState = "Fortified",
            },
        ]);

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    /// <summary>A search_systems that records what it was given and answers as the test says.</summary>
    private sealed class World
    {
        public World(bool lookups)
        {
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);
            Store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

            // Protected, so a call made as the model is refused and only the Commander's goes through.
            Registry = CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = "galaxy-probe",
                    Group = "Foundation",
                    Name = "Galaxy probe",
                    Summary = "search_systems, observed.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "search_systems",
                            Description = "Records its arguments.",
                            Parameters = GalaxyCapability.Create(null, () => null, Settings).Tools.Single(tool => tool.Name == "search_systems").Parameters,
                            Protected = true,
                            Handler = (arguments, _) =>
                            {
                                Asked.Add(new Dictionary<string, string>(arguments.Values, StringComparer.Ordinal));

                                if (Error is { } error)
                                {
                                    return Task.FromResult(ToolResult.Error(error));
                                }

                                Board.Post(Posting(arguments.Values, Result));
                                return Task.FromResult(ToolResult.Ok("answered"));
                            },
                        },
                    ],
                },
            ]);
        }

        public SettingsService Settings { get; }

        public SavedSearchStore Store { get; }

        public CapabilityRegistry Registry { get; }

        public GalaxySearchBoard Board { get; } = new();

        public List<Dictionary<string, string>> Asked { get; } = [];

        public string? Error { get; set; }

        public string Here { get; set; } = "LTT 7786";

        public GalaxySearchResult Result { get; set; } = TwoSystems;

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation(Here, null, false, null) { SystemAddress = Ltt7786 },
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(Registry, Board, Store, () => Fid, () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        };
    }

    private static GalaxySearchPosting Posting(IReadOnlyDictionary<string, string> arguments, GalaxySearchResult result) =>
        new(
            GalaxySearchKind.Systems,
            new Dictionary<string, string>(arguments, StringComparer.Ordinal),
            result.Reference,
            result,
            result.Systems.Count == 0 ? "Nothing matched that search." : "2 systems matched.",
            AskedAt);

    private static (PanelView Panel, Window Window) Shown(World world, bool headset = false, bool systems = true)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Bind(Window.BackgroundProperty, Avalonia.Application.Current!.Resources.GetResourceObservable(D47.App.Theming.ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;

        if (systems)
        {
            panel.Nav.SelectRoot(SystemSearchPage.RootKey);
        }

        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static SystemSearchPage Page(Control root) => root.GetVisualDescendants().OfType<SystemSearchPage>().Single();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static InlinePicker StartFrom(Control root) =>
        root.GetVisualDescendants().OfType<InlinePicker>().First();

    private static List<string> Picks(Control root)
    {
        var picker = StartFrom(root);
        picker.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        return [.. picker.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains(InlinePicker.OptionClass)).Select(button => Avalonia.Automation.AutomationProperties.GetName(button) ?? string.Empty)];
    }

    private static void Pick(Control root, string name)
    {
        _ = Picks(root);
        Press(StartFrom(root).GetVisualDescendants().OfType<Button>().Single(button =>
            button.Classes.Contains(InlinePicker.OptionClass) && Avalonia.Automation.AutomationProperties.GetName(button) == name));
    }

    private static string PickerValue(Control root) =>
        string.Concat(StartFrom(root).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "InlinePickerValue").Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static List<Control> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(GalaxySearchPage.RowClass))];

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Concat(text.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? []))];

    [AvaloniaFact]
    public void SearchOffersSystemThenSystemsThenStationsThenBodiesAndSystemStillFollowsTheCommander()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["allegiance"] = "Independent" }, TwoSystems));
        var (panel, window) = Shown(world, systems: false);

        Assert.Equal(
            [StarSystemPage.RootKey, SystemSearchPage.RootKey, StationSearchPage.RootKey, OutfittingSearchPage.RootKey, BodySearchPage.RootKey, SignalSearchPage.RootKey],
            panel.Nav.Roots(PanelTab.Search).Select(root => root.Key));
        Assert.Equal(StarSystemPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));

        var system = panel.GetVisualDescendants().OfType<StarSystemPage>().Single();
        Assert.True(system.Following);

        panel.Nav.SelectRoot(SystemSearchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Press((Button)Rows(panel).Last());

        Assert.Equal(2, panel.Nav.TrailOf(PanelTab.Search).Count);
        Assert.Equal(SearchFurnishing.FoundKeyPrefix + Hip112535, panel.Nav.TrailOf(PanelTab.Search)[^1].Key);
        Assert.Contains(panel.GetVisualDescendants().OfType<StarSystemPage>(), page => !page.Following);

        panel.Nav.SelectRoot(StarSystemPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.True(system.Following);
        Assert.True(panel.OpenSystem(Ltt7786));
        Assert.False(system.Following);

        window.Close();
    }

    [AvaloniaFact]
    public void RunSearchSendsTheFormAsTheCommanderAndDrawsTheAnswer()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("allegiance", "Federation");
        Press(Named(panel, GalaxySearchPage.RunName));

        var asked = Assert.Single(world.Asked);
        Assert.Equal(new Dictionary<string, string> { ["allegiance"] = "Federation" }, asked);
        Assert.Equal(GalaxySearchView.Answer, page.View);
        Assert.Equal(2, Rows(panel).Count);
        Assert.Contains("2 SYSTEMS", TextOf(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheProbeRefusesTheModelSoTheFormMustCallAsTheCommander()
    {
        var world = new World(lookups: true);

        var refused = await world.Registry.InvokeAsync(
            "search_systems",
            new ToolArguments(new Dictionary<string, string> { ["allegiance"] = "Federation" }),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.Empty(world.Asked);
    }

    [AvaloniaFact]
    public void TheFormSendsEveryParameterTheToolTakes()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Type("near", "Sol");
        page.Type("distance from", "10");
        page.Type("distance to", "50");
        page.Type("minimum population", "1000");
        page.Type("controlling_faction", "Diamond Frogs");
        page.Type("faction", "Diamond Frogs");

        foreach (var name in (string[])["allegiance", "government", "primary_economy", "security", "state", "faction_state", "faction_government", "power"])
        {
            page.Pick(name, GalaxyFilters.Find(name)!.Choices[0]);
        }

        foreach (var stepper in page.GetLogicalDescendants().OfType<D47.App.Controls.Stepper>())
        {
            stepper.SelectedIndex = 1;
        }

        page.GetLogicalDescendants().OfType<CheckBox>().Single(box => box.Content as string == "Only colonised systems").IsChecked = true;
        page.Limit = 8;
        page.MaxReportAgeHours = 72;

        var tool = GalaxyCapability.Create(null, () => null, world.Settings).Tools.Single(t => t.Name == "search_systems");

        Assert.Equal(
            tool.Parameters.Select(parameter => parameter.Name).Except(["order_by", "order"]).Order(StringComparer.Ordinal),
            page.FormArguments().Keys.Order(StringComparer.Ordinal));
        Assert.Equal("10-50", page.FormArguments()["distance"]);
        Assert.Equal("1000-", page.FormArguments()["population"]);
        Assert.Equal("8", page.FormArguments()["limit"]);

        Press(Named(panel, GalaxySearchPage.ClearName));

        Assert.Empty(page.FormArguments());

        window.Close();
    }

    [AvaloniaFact]
    public async Task AVoicePostingRedrawsAnOpenPage()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Assert.Empty(Rows(panel));

        await Task.Run(() => world.Board.Post(Posting(new Dictionary<string, string> { ["government"] = "Anarchy" }, TwoSystems)), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, Rows(panel).Count);
        Assert.Equal(new Dictionary<string, string> { ["government"] = "Anarchy" }, Page(panel).FormArguments());

        window.Close();
    }

    [AvaloniaFact]
    public void RefreshRerunsThePostingsArgumentsWithNearLeftOut()
    {
        var world = new World(lookups: true);
        var asked = new Dictionary<string, string> { ["allegiance"] = "Empire", ["distance"] = "50" };
        world.Board.Post(Posting(asked, TwoSystems));
        var (panel, window) = Shown(world);

        Assert.Contains(TextOf(panel), text => text.StartsWith("2 SYSTEMS", StringComparison.Ordinal));
        Assert.Contains("WITHIN 50 LY OF LTT 7786", TextOf(panel));

        Press(Named(panel, GalaxySearchPage.RefreshName));

        Assert.Equal(asked, Assert.Single(world.Asked));
        Assert.False(world.Asked[0].ContainsKey("near"));

        window.Close();
    }

    [AvaloniaFact]
    public void WithLookupsOffRunSearchIsDisabledAndTheSettingIsNamed()
    {
        var world = new World(lookups: false);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Assert.Equal(GalaxySearchView.LookupsOff, page.View);
        Assert.False(page.CanRun);
        Assert.Contains(GalaxySearchPage.LookupsOffText, TextOf(panel));
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => button.Name == GalaxySearchPage.OpenSettingName);

        window.Close();
    }

    [AvaloniaFact]
    public void AnUnavailableServiceDrawsItsSentence()
    {
        const string Unreachable = "I couldn't reach the galaxy search — check the network connection.";

        var world = new World(lookups: true) { Error = Unreachable };
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("allegiance", "Federation");
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.Equal(GalaxySearchView.Unavailable, page.View);
        Assert.Equal(Unreachable, page.Sentence);
        Assert.Contains(panel.GetVisualDescendants().OfType<D47.App.Controls.Notice>(), notice => notice.Text == Unreachable && notice.Label == "GALAXY SEARCH UNAVAILABLE");
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => button.Name == GalaxySearchPage.RetryName);

        window.Close();
    }

    [AvaloniaFact]
    public void ARefusedFilterDrawsTheToolsSentence()
    {
        const string Refusal = "I can't read 'far' as a distance.";

        var world = new World(lookups: true) { Error = Refusal };
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Type("distance to", "far");
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.Equal(GalaxySearchView.Refused, page.View);
        Assert.Contains(panel.GetVisualDescendants().OfType<D47.App.Controls.Notice>(), notice => notice.Text == Refusal && notice.Label == "FILTER REFUSED");

        window.Close();
    }

    [AvaloniaFact]
    public void NoResultsDrawsTheToolsSentence()
    {
        var world = new World(lookups: true) { Result = new GalaxySearchResult("LTT 7786", 0, []) };
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("government", "Prison");
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.Equal(GalaxySearchView.NoResults, page.View);
        Assert.Contains("Nothing matched that search.", TextOf(panel));
        Assert.Contains("0 SYSTEMS", TextOf(panel));
        Assert.Empty(Rows(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void SavingAddsToSavedAndTheStoreAndForgettingRemovesIt()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["faction"] = "Diamond Frogs", ["state"] = "Boom" }, TwoSystems));
        var (panel, window) = Shown(world);

        Press(Named(panel, GalaxySearchPage.SaveName));

        var dialog = window.GetVisualDescendants().OfType<SaveSearchDialog>().Single();
        dialog.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == SaveSearchDialog.NameFieldName).Text = "anarchy outbreak";
        Press(Named(dialog, SaveSearchDialog.ConfirmName));

        Assert.Single(window.GetVisualDescendants().OfType<SaveSearchDialog>());
        Assert.True(dialog.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == SaveSearchDialog.RefusalName).IsVisible);

        dialog.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == SaveSearchDialog.NameFieldName).Text = "Frogs in boom";
        Press(Named(dialog, SaveSearchDialog.ConfirmName));

        Assert.Empty(window.GetVisualDescendants().OfType<SaveSearchDialog>());

        var saved = Assert.Single(world.Store.All());
        Assert.Equal("Frogs in boom", saved.Name);
        Assert.Equal("search_systems", saved.Tool);
        Assert.Equal("Boom", saved.Arguments["state"]);

        Assert.Equal(["anarchy outbreak", "Frogs in boom"], Picks(panel));
        Assert.False(Named(panel, GalaxySearchPage.ForgetName).IsVisible);

        Pick(panel, "Frogs in boom");

        var forget = Named(panel, GalaxySearchPage.ForgetName);
        Assert.True(forget.IsVisible);
        Assert.Equal("Forget the Frogs in boom search", Avalonia.Automation.AutomationProperties.GetName(forget));

        Press(forget);
        Press(Named(window.GetVisualDescendants().OfType<ForgetSearchDialog>().Single(), ForgetSearchDialog.ConfirmName));

        Assert.Empty(world.Store.All());
        Assert.DoesNotContain("Frogs in boom", Picks(panel));
        Assert.Equal(GalaxySearchPage.NoPickText, PickerValue(panel));
        Assert.False(Named(panel, GalaxySearchPage.ForgetName).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ASavedSearchRunsWhenPressed()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Pick(panel, "anarchy outbreak");

        Assert.Equal(new Dictionary<string, string> { ["government"] = "Anarchy", ["state"] = "Outbreak" }, Assert.Single(world.Asked));
        Assert.Equal("Anarchy", Page(panel).FormArguments()["government"]);
        Assert.Equal("anarchy outbreak", PickerValue(panel));
        Assert.False(Named(panel, GalaxySearchPage.ForgetName).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePickedNameGoesBackOnceTheFormIsEditedOrCleared()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Pick(panel, "anarchy outbreak");
        Assert.Equal("anarchy outbreak", PickerValue(panel));

        Page(panel).Tick();
        Assert.Equal("anarchy outbreak", PickerValue(panel));

        Pick(panel, "anarchy outbreak");
        Press(Named(panel, GalaxySearchPage.ClearName));

        Assert.Equal(GalaxySearchPage.NoPickText, PickerValue(panel));
        Assert.False(Named(panel, GalaxySearchPage.ForgetName).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetDrawsTheAnswerViewAndTheFormViewFullWidth()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["allegiance"] = "Independent" }, TwoSystems));
        var (panel, window) = Shown(world, headset: true);
        var page = Page(panel);

        Assert.True(page.ShowsAnswer);
        Assert.Equal(2, Rows(panel).Count);
        Assert.All(
            [GalaxySearchPage.EditName, GalaxySearchPage.RefreshName, GalaxySearchPage.SaveName],
            name => Assert.True(Named(panel, name).IsEffectivelyVisible));
        Assert.False(Named(panel, GalaxySearchPage.RunName).IsEffectivelyVisible);
        Assert.DoesNotContain("REPORTED", TextOf(panel));

        Press(Named(panel, GalaxySearchPage.EditName));

        Assert.False(page.ShowsAnswer);
        Assert.True(Named(panel, GalaxySearchPage.RunName).IsEffectivelyVisible);
        Assert.True(Named(panel, GalaxySearchPage.ClearName).IsEffectivelyVisible);
        Assert.True(StartFrom(panel).IsEffectivelyVisible);
        Assert.Equal("Independent", page.FormArguments()["allegiance"]);
        Assert.DoesNotContain(Rows(panel), row => row.IsEffectivelyVisible);

        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.True(page.ShowsAnswer);
        Assert.Equal(new Dictionary<string, string> { ["allegiance"] = "Independent" }, Assert.Single(world.Asked));

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetOpensOnTheFormWhenNothingWasAsked()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world, headset: true);

        Assert.False(Page(panel).ShowsAnswer);
        Assert.True(Named(panel, GalaxySearchPage.RunName).IsEffectivelyVisible);

        world.Board.Post(Posting(new Dictionary<string, string> { ["allegiance"] = "Empire" }, TwoSystems));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Page(panel).ShowsAnswer);
        Assert.Equal("Empire", Page(panel).FormArguments()["allegiance"]);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetFormRunsASavedSearchAndShowsTheAnswer()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world, headset: true);

        Pick(panel, "anarchy outbreak");

        Assert.True(Page(panel).ShowsAnswer);
        Assert.Equal(2, Rows(panel).Count);

        Press(Named(panel, GalaxySearchPage.EditName));

        Assert.Equal("Anarchy", Page(panel).FormArguments()["government"]);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetFormDrawsRunDisabledWhenLookupsAreOff()
    {
        var world = new World(lookups: false);
        var (panel, window) = Shown(world, headset: true);

        Assert.False(Page(panel).ShowsAnswer);
        Assert.False(Named(panel, GalaxySearchPage.RunName).IsEnabled);
        Assert.Equal("Turn on “Look things up in the galaxy” in Settings to search.", ToolTip.GetTip(Named(panel, GalaxySearchPage.RunName)));

        window.Close();
    }

    [AvaloniaFact]
    public void ASpokenListOfStatesStaysInTheForm()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["faction_state"] = "Boom,Bust" }, TwoSystems));
        var (panel, window) = Shown(world);

        Assert.Equal("Boom, Bust", Page(panel).FormArguments()["faction_state"]);

        window.Close();
    }

    [AvaloniaFact]
    public void NearSaysWhereTheCommanderIsAfterAJump()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var near = Page(panel).GetLogicalDescendants().OfType<TextBox>().First();

        Assert.Equal("YOUR SYSTEM · LTT 7786", near.PlaceholderText);
        Assert.Contains("here", near.Classes);
        Assert.Contains(Page(panel).GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Follows you as you jump. Type a system name to search from there instead.");

        world.Here = "Sol";
        Assert.True(panel.TickSearch());

        Assert.Equal("YOUR SYSTEM · SOL", near.PlaceholderText);

        window.Close();
    }

    [AvaloniaFact]
    public void ATypedFormSurvivesLeavingTheTab()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["allegiance"] = "Independent" }, TwoSystems));
        var (panel, window) = Shown(world);

        Page(panel).Pick("power", "Archon Delaine");

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();
        panel.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Archon Delaine", Page(panel).FormArguments()["power"]);

        window.Close();
    }

    [AvaloniaFact]
    public void EveryPressableControlIsAtLeastFortyFourTall()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["allegiance"] = "Independent" }, TwoSystems));
        var (panel, window) = Shown(world);
        var page = Page(panel);

        var pressable = page.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control is Button or CheckBox or TextBox)
            .Where(control => !control.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.ScrollBar>().Any())
            .ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, control => Assert.True(
            control.Bounds.Height >= 44 - 0.5,
            $"{control.GetType().Name} '{control.Name ?? (control as ContentControl)?.Content?.ToString()}' is {control.Bounds.Height} tall"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSystemsPageIsCaptured()
    {
        using var look = AppLook.Put();

        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["faction"] = "Diamond Frogs", ["distance"] = "50" }, TwoSystems) with { Notes = "Read as Diamond Frogs." });
        world.Store.Add("Frogs in boom", "search_systems", new Dictionary<string, string> { ["state"] = "Boom" }, AskedAt, []);
        var (panel, window) = Shown(world);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-systems.png");
        }

        Press(Named(panel, GalaxySearchPage.SaveName));
        window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == SaveSearchDialog.NameFieldName).Text = "Frogs near me";
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-systems-save.png");
        }

        Assert.Single(window.GetVisualDescendants().OfType<SaveSearchDialog>());

        window.Close();

        var off = new World(lookups: false);
        var (_, offWindow) = Shown(off);

        using (var frame = offWindow.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-systems-lookups-off.png");
        }

        offWindow.Close();
    }

    [AvaloniaFact]
    public unsafe void TheSystemsPageIsCapturedInTheHeadset()
    {
        using var look = AppLook.Put();

        var world = new World(lookups: true);
        world.Settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        world.Board.Post(Posting(new Dictionary<string, string> { ["faction"] = "Diamond Frogs", ["distance"] = "50" }, TwoSystems));
        world.Store.Add("Frogs in boom", "search_systems", new Dictionary<string, string> { ["state"] = "Boom" }, AskedAt, []);

        var headset = new VrPanelSurface(new PanelViewModel(), world.Settings, _ => null, starSystem: world.Surface);
        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(headset)!;

        headset.Nav.Select(PanelTab.Search);
        headset.Nav.SelectRoot(SystemSearchPage.RootKey);

        var (width, height) = headset.Size;
        var buffer = new byte[width * height * 4];

        fixed (byte* pixels = buffer)
        {
            Dispatcher.UIThread.RunJobs();
            headset.Draw((IntPtr)pixels, width * 4);
            headset.Draw((IntPtr)pixels, width * 4);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(2, Rows(view).Count);
        Assert.All(
            view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && button.FindAncestorOfType<SystemSearchPage>() is not null),
            button => Assert.True(button.Bounds.Height >= 44 - 0.5, $"{button.Name ?? button.Content?.ToString()} is {button.Bounds.Height} tall"));

        using (var answer = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(width, height)))
        {
            answer.Render(view);
            answer.SaveCapture("search-systems-headset.png");
        }

        Press(Named(view, GalaxySearchPage.EditName));

        fixed (byte* pixels = buffer)
        {
            headset.Draw((IntPtr)pixels, width * 4);
            headset.Draw((IntPtr)pixels, width * 4);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.All(
            view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && button.FindAncestorOfType<SystemSearchPage>() is not null),
            button => Assert.True(button.Bounds.Height >= 44 - 0.5, $"{button.Name ?? button.Content?.ToString()} is {button.Bounds.Height} tall"));

        using var form = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(width, height));
        form.Render(view);
        form.SaveCapture("search-systems-headset-form.png");
    }

    [AvaloniaFact]
    public void AnAnswerOfOneUsesTheSingularNoun()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["allegiance"] = "Independent" }, TwoSystems with { Total = 1, Systems = [TwoSystems.Systems[0]] }));
        var (panel, window) = Shown(world);

        Assert.Contains("1 SYSTEM", TextOf(panel));
        Assert.DoesNotContain("1 SYSTEMS", TextOf(panel));

        window.Close();
    }
}
