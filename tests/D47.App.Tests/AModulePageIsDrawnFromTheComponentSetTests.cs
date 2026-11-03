using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Modules;
using Xunit;

namespace D47.App.Tests;

/// <summary>A module's page drawn from what it sent, on the window and the big panel, its presses calling its own tools as the Commander (#2084).</summary>
[Trait("Category", "Integration")]
public sealed class AModulePageIsDrawnFromTheComponentSetTests
{
    private const string Ledger = """
        {"parts":[
          {"type":"GroupHead","title":"This session","description":"Bounties since you launched the game."},
          {"type":"DataBlock","cells":[{"label":"Claimed","value":"14"},{"label":"Value","value":"4,210,500 CR"},{"label":"Hunting in","value":"GIRYAK","tone":"here"}]},
          {"type":"Notice","tone":"warning","label":"3 vouchers not handed in","text":"Hand them in at a station of the faction that issued them, or at an Interstellar Factors contact."},
          {"type":"GroupHead","title":"Open bounties","description":"Wanted ships you scanned this session."},
          {"type":"List","rows":[
            {"name":"Kel Ardon","sub":"Python · Giryak","aside":"212,000 CR"},
            {"name":"Sabre Lune","sub":"Viper Mk III · Giryak","aside":"48,300 CR"},
            {"name":"Odo Varn","sub":"Asp Explorer · LHS 3447","aside":"31,900 CR"}]},
          {"type":"TileButtons","buttons":[{"label":"All open bounties","page":"open"}]},
          {"type":"GroupHead","title":"Callouts","description":"What Bounty ledger says out loud."},
          {"type":"CheckboxTiles","tiles":[
            {"label":"Say each bounty as it lands","checked":true,"tool":"say_each","argument":"on"},
            {"label":"Say the session total when you dock","tool":"say_total","argument":"on"}]},
          {"type":"SettingsRow","label":"Count","segmented":{"tool":"count","argument":"period","options":[{"label":"This session","value":"session"},{"label":"Today","value":"today"},{"label":"All time","value":"all"}],"selected":"session"}},
          {"type":"SettingsRow","label":"Point out a bounty above","stepper":{"tool":"threshold","argument":"credits","options":[{"label":"50,000 CR","value":50000},{"label":"100,000 CR","value":100000},{"label":"200,000 CR","value":200000}],"selected":100000}},
          {"type":"GroupHead","title":"Last lines","description":"What it said, newest last."},
          {"type":"Message","text":"Kel Ardon, 212,000 credits. Your biggest this session.","at":"2026-11-02T19:38:00Z"},
          {"type":"Message","text":"Fourteen claimed, 4.2 million credits so far.","at":"2026-11-02T19:42:00Z"},
          {"type":"TileButtons","buttons":[
            {"label":"Remind me to hand them in","tool":"remind","arguments":{"minutes":30}},
            {"label":"Clear the session","tool":"clear_session","destructive":true}]}
        ]}
        """;

    private const string Open = """
        {"parts":[
          {"type":"GroupHead","title":"Open bounties","description":"Every wanted ship you scanned this session, largest first."},
          {"type":"Table","columns":[{"label":"Target"},{"label":"Ship"},{"label":"System"},{"label":"Issued by"},{"label":"Bounty","numeric":true},{"label":"Seen","numeric":true}],"rows":[
            ["Kel Ardon","Python",{"text":"Giryak","tone":"here"},"Giryak Crimson Ring","212,000","19:38"],
            ["Sabre Lune","Viper Mk III",{"text":"Giryak","tone":"here"},"Giryak Crimson Ring","48,300","19:21"],
            ["Odo Varn","Asp Explorer","LHS 3447","LHS 3447 Prison Colony","31,900","18:57"],
            ["Mira Tolk","Cobra Mk III","LHS 3447","LHS 3447 Prison Colony","18,750","18:44"],
            ["Hollis Rane","Eagle","LHS 3447","LHS 3447 Prison Colony","6,200","18:31"]]}
        ]}
        """;

    private sealed class Module
    {
        public Dictionary<string, string> Pages { get; } = new(StringComparer.Ordinal)
        {
            ["d47://pages/ledger"] = Ledger,
            ["d47://pages/open"] = Open,
        };

        public List<(string Name, ToolArguments Arguments, ToolCaller Caller)> Calls { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Reads { get; } = [];

        /// <summary>Tools whose calls the module answers with an error.</summary>
        public HashSet<string> Failing { get; } = [];

        public Action<string, string>? Updated { get; private set; }

        public ModulesSource Source()
        {
            var module = new ReadyModule(
                "modules\\ledger",
                new ModuleManifest(
                    1, "ledger", "Bounty ledger", "CMDR Ana Vey", "1.0.0", "ledger.exe", [], 120,
                    [new ModulePage("ledger", "Bounty ledger"), new ModulePage("open", "Open bounties")],
                    new ModuleCharacter("Tally", [])),
                "ledger.exe");
            var status = new ModuleStatusReport(module.Folder, "ledger", ModuleStatus.Running, DateTimeOffset.UtcNow);

            return new ModulesSource(() => [module], () => [status], _ => { }, () => [], () => [], () => [])
            {
                Pages = new ModulePageServices
                {
                    ReadAsync = (_, uri, _) =>
                    {
                        lock (Reads)
                        {
                            Reads.Add(uri);
                        }

                        return Task.FromResult(Pages.GetValueOrDefault(uri));
                    },
                    ToolsAsync = (_, _) => Task.FromResult<IReadOnlyList<string>>(["say_each", "say_total", "count", "threshold", "remind", "clear_session"]),
                    InvokeAsync = (name, arguments, caller, _) =>
                    {
                        lock (Calls)
                        {
                            Calls.Add((name, arguments, caller));
                        }

                        return Task.FromResult(Failing.Contains(name) ? ToolResult.Error("Refused.") : ToolResult.Ok("done"));
                    },
                    SpeakerName = _ => "Tally",
                    Warn = (_, what, why) => Warnings.Add(what + ": " + why),
                    Watch = handler => Updated = handler,
                },
            };
        }
    }

    private static SettingsHost Show(Module module, bool headset = false)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, headset: headset, width: 1280, height: headset ? 1100 : 1640);
        host.Panel.EnableModules(module.Source());
        Dispatcher.UIThread.RunJobs();

        host.Panel.Tab = PanelTab.Modules;
        Dispatcher.UIThread.RunJobs();
        Press(Named<Button>(host, ModulesInstalledPage.RowName + ".Bounty ledger"));
        Settle(host, () => Find<StackPanel>(host, ModulePageView.ContentName) is not null || Find<Notice>(host, ModulePageView.NoticeName) is not null);

        return host;
    }

    private static void Settle(SettingsHost host, Func<bool> done)
    {
        for (var i = 0; i < 1000 && !done(); i++)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        host.Window.UpdateLayout();
    }

    private static T? Find<T>(SettingsHost host, string name)
        where T : Control =>
        host.Panel.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible);

    private static T Named<T>(SettingsHost host, string name)
        where T : Control =>
        Find<T>(host, name) ?? throw new InvalidOperationException($"No {typeof(T).Name} named {name}.");

    private static void Press(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).Where(t => t.Length > 0)];

    [AvaloniaTheory]
    [InlineData(false, "module-page-window.png")]
    [InlineData(true, "module-page-big-panel.png")]
    public void ThePageDrawsEachPartTheModuleSent(bool headset, string capture)
    {
        using var look = AppLook.Put();
        var module = new Module();
        var host = Show(module, headset);

        host.Window.CaptureRenderedFrame()!.SaveCapture(capture);

        var content = Named<StackPanel>(host, ModulePageView.ContentName);
        var texts = Texts(content);

        Assert.Empty(module.Warnings);
        Assert.Null(Find<Notice>(host, ModulePageView.NoticeName));
        Assert.Contains("THIS SESSION", texts);
        Assert.Contains("GIRYAK", texts);
        Assert.Contains("Kel Ardon", texts);
        Assert.Contains("212,000 CR", texts);
        Assert.Contains("Tally", texts);
        Assert.Contains("Bounty ledger", texts);
        Assert.Contains("Fourteen claimed, 4.2 million credits so far.", texts);
        Assert.Single(content.GetVisualDescendants().OfType<Notice>());
        Assert.Equal(2, content.GetVisualDescendants().OfType<CheckBox>().Count());
        Assert.True(content.Bounds.Width <= 900);
        Assert.Equal("A module by CMDR Ana Vey.", ((IPageSummary)host.Panel.GetVisualDescendants().OfType<ModulePageView>().Single()).Summary);

        foreach (var button in content.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith(ModulePageDrawing.ButtonName, StringComparison.Ordinal) == true))
        {
            Assert.True(button.Bounds.Height >= 44, $"{button.Name} is {button.Bounds.Height}px high.");
        }

        host.Close();
    }

    [AvaloniaFact]
    public void APressedTileAndATickedBoxCallTheModulesToolsAsTheCommander()
    {
        using var look = AppLook.Put();
        var module = new Module();
        var host = Show(module);

        Press(Named<Button>(host, ModulePageDrawing.ButtonName + ".remind"));
        Named<CheckBox>(host, ModulePageDrawing.CheckboxName + ".say_total").IsChecked = true;
        Settle(host, () => module.Calls.Count >= 2);

        var calls = module.Calls.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(["mod_ledger_remind", "mod_ledger_say_total"], calls.Select(c => c.Name));
        Assert.All(calls, call => Assert.Equal(ToolCaller.Commander, call.Caller));
        Assert.Equal("""{"minutes":30}""", calls[0].Arguments.Json);
        Assert.Equal("""{"on":true}""", calls[1].Arguments.Json);

        host.Close();
    }

    [AvaloniaFact]
    public void ATileOpensTheModulesSecondPageUnderItsBreadcrumb()
    {
        using var look = AppLook.Put();
        var module = new Module();
        var host = Show(module);

        Press(Named<Button>(host, ModulePageDrawing.ButtonName + ".open"));
        Settle(host, () => Find<ScrollViewer>(host, ModulePageDrawing.TableName) is not null);

        host.Window.CaptureRenderedFrame()!.SaveCapture("module-page-second.png");

        Assert.Equal(["Installed", "Bounty ledger", "Open bounties"], host.Panel.Nav.TrailOf(PanelTab.Modules).Select(c => c.Word));
        var table = Named<ScrollViewer>(host, ModulePageDrawing.TableName);
        Assert.Contains("Hollis Rane", Texts(table));
        Assert.Contains("LHS 3447 Prison Colony", Texts(table));

        host.Close();
    }

    [AvaloniaFact]
    public void AnUpdatedPageIsReadAndDrawnAgain()
    {
        using var look = AppLook.Put();
        var module = new Module();
        var host = Show(module);

        module.Pages["d47://pages/ledger"] = """{"parts":[{"type":"DataBlock","prose":"Nothing claimed yet."}]}""";
        module.Updated!("ledger", "d47://pages/ledger");
        Settle(host, () => Texts(Named<StackPanel>(host, ModulePageView.ContentName)).Contains("Nothing claimed yet."));

        Assert.Contains("Nothing claimed yet.", Texts(Named<StackPanel>(host, ModulePageView.ContentName)));

        host.Close();
    }

    [AvaloniaFact]
    public void BackOnTheFirstPageAnUpdateStillRedrawsIt()
    {
        using var look = AppLook.Put();
        var module = new Module();
        var host = Show(module);

        Press(Named<Button>(host, ModulePageDrawing.ButtonName + ".open"));
        Settle(host, () => Find<ScrollViewer>(host, ModulePageDrawing.TableName) is not null);
        host.Panel.GoBack();
        Settle(host, () => Find<ScrollViewer>(host, ModulePageDrawing.TableName) is null);

        module.Pages["d47://pages/ledger"] = """{"parts":[{"type":"DataBlock","prose":"Nothing claimed yet."}]}""";
        module.Updated!("ledger", "d47://pages/ledger");
        Settle(host, () => Find<StackPanel>(host, ModulePageView.ContentName) is { } shown && Texts(shown).Contains("Nothing claimed yet."));

        Assert.Equal(["Installed", "Bounty ledger"], host.Panel.Nav.TrailOf(PanelTab.Modules).Select(c => c.Word));
        Assert.Contains("Nothing claimed yet.", Texts(Named<StackPanel>(host, ModulePageView.ContentName)));

        host.Close();
    }

    [AvaloniaFact]
    public void AFailedPressReadsThePageAgainSoTheBoxShowsWhatTheModuleHas()
    {
        using var look = AppLook.Put();
        var module = new Module();
        module.Failing.Add("mod_ledger_say_total");
        var host = Show(module);
        var reads = module.Reads.Count;

        Named<CheckBox>(host, ModulePageDrawing.CheckboxName + ".say_total").IsChecked = true;
        Settle(host, () => module.Reads.Count > reads && Find<CheckBox>(host, ModulePageDrawing.CheckboxName + ".say_total")?.IsChecked == false);

        Assert.False(Named<CheckBox>(host, ModulePageDrawing.CheckboxName + ".say_total").IsChecked);
        Assert.Contains(module.Warnings, w => w.Contains("say_total", StringComparison.Ordinal));

        host.Close();
    }

    [AvaloniaFact]
    public void APageLeftBehindReadsNothingMore()
    {
        using var look = AppLook.Put();
        var module = new Module();
        var host = Show(module);

        Press(Named<Button>(host, ModulePageDrawing.ButtonName + ".open"));
        Settle(host, () => Find<ScrollViewer>(host, ModulePageDrawing.TableName) is not null);
        host.Panel.GoBack();
        Settle(host, () => Find<ScrollViewer>(host, ModulePageDrawing.TableName) is null);
        var reads = module.Reads.Count;

        module.Updated!("ledger", "d47://pages/open");
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(reads, module.Reads.Count);

        host.Close();
    }

    [AvaloniaFact]
    public void ARefusedPageShowsThatItDidNotArriveAndLogsWhy()
    {
        using var look = AppLook.Put();
        var module = new Module();
        module.Pages["d47://pages/ledger"] = """{"parts":[{"type":"GroupHead","title":"a","color":"#f00"}]}""";
        var host = Show(module, headset: false);
        Settle(host, () => Find<Notice>(host, ModulePageView.NoticeName) is not null);

        Assert.Null(Find<StackPanel>(host, ModulePageView.ContentName));
        Assert.Equal(ModulesTab.NotArrivedLabel.ToUpperInvariant(), Named<Notice>(host, ModulePageView.NoticeName).Label);
        Assert.Contains(module.Warnings, w => w.Contains("colour, font, image or size", StringComparison.Ordinal));

        host.Close();
    }
}
