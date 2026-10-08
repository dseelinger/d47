using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Settings › Phrases adds through <c>add_phrase</c>, lists the Commander's phrases and every built-in one (#566).</summary>
public class ThePhrasesPageAddsWhatTheVoiceWouldTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed record Surface(Window Window, PanelView Panel, PhrasesPage Page, LearnedPhrasesStore Store) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static Surface Open(bool seed = true, double width = 1100, double height = 900, bool actions = false)
    {
        var root = TempFolders.Create("d47-phrases-page-tests");

        var store = new LearnedPhrasesStore(
            Path.Combine(root, "phrases.json"), NullLogger<LearnedPhrasesStore>.Instance);

        if (seed)
        {
            store.Learn("F1", "[gear|wheels] out", "new phrase", At);
        }

        var gameState = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            NullLogger.Instance,
            out var commander));
        gameState.Apply(commander!);

        CapabilityRegistry? registry = null;

        registry = CapabilityRegistry.Build(
        [
            LearnedPhrasesCapability.Create(
                store,
                () => gameState.Active?.Identity.FrontierId ?? string.Empty,
                () => PhraseBook.From(registry!, [])),
            InterfaceCapability.Create(),
            .. actions ? ActionCapabilities.All(ActionSurface.Inert) : [],
        ]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(
            () => new TextBlock { Text = "Settings" },
            phrases: () => new PhrasesPage(registry, store, () => gameState.Active));

        var window = new Window { Content = panel, Width = width, Height = height };

        window.Show();

        panel.Tab = PanelTab.Settings;
        Assert.True(panel.Nav.SelectRoot(PhrasesPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<PhrasesPage>().Single();

        return new Surface(window, panel, page, store);
    }

    private static IReadOnlyList<string> Drawn(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static TextBox Say(Surface surface) =>
        surface.Page.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Say");

    private static Notice Notice(Surface surface) =>
        surface.Page.GetVisualDescendants().OfType<Notice>().Single();

    private static void Pick(Surface surface, string phrase)
    {
        var picker = surface.Page.GetVisualDescendants().OfType<InlinePicker>().Single();
        picker.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        var option = picker.GetVisualDescendants().OfType<Button>()
            .First(button => button.Classes.Contains(InlinePicker.OptionClass) && AutomationProperties.GetName(button) == phrase);

        option.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Add(Surface surface)
    {
        var add = surface.Page.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "Add");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Surface surface, string text)
    {
        Say(surface).Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheSettingsTabHasAPhrasesRoot()
    {
        using var surface = Open();

        Assert.Contains(
            surface.Panel.Nav.Roots(PanelTab.Settings),
            root => root.Key == PhrasesPage.RootKey && root.Word == "Phrases");
    }

    [AvaloniaFact]
    public void YourPhraseIsDrawnBesideThePhraseItStandsFor()
    {
        using var surface = Open();

        var drawn = Drawn(surface.Page);

        Assert.Contains("[gear|wheels] out", drawn);
        Assert.Contains("new phrase", drawn);
        Assert.Contains("YOUR PHRASES", drawn);
    }

    [AvaloniaFact]
    public void WithNothingTaughtThePageSaysHowToTeachOne()
    {
        using var surface = Open(seed: false);

        Assert.Contains(PhrasesPage.NothingTaught, Drawn(surface.Page));
    }

    [AvaloniaFact]
    public void EveryBuiltInPhraseIsListedUnderItsCapabilityWithWhatItDoes()
    {
        using var surface = Open();

        var drawn = Drawn(surface.Page);

        Assert.Contains("PHRASES", drawn);
        Assert.Contains(LearnedPhrasesCapability.TeachPhrase, drawn);
        Assert.Contains("show me every setting", drawn);
        Assert.Contains(drawn, line => line.StartsWith("Start teaching D47 a new wording by voice", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AGameActionIsListedAsItsPatternsRatherThanEveryPhrase()
    {
        using var surface = Open(actions: true, height: 1000);

        surface.Page.Filter("landing gear");
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(surface.Page);

        Assert.Contains("[gear|landing gear]", drawn);
        Assert.Contains("[gear|landing gear] [down|up]", drawn);
        Assert.Contains("[turn|switch] [on|off] [gear|landing gear]", drawn);
        Assert.Contains("[deploy|lower|retract|raise] [gear|landing gear]", drawn);
        Assert.DoesNotContain("turn on landing gear", drawn);
        Assert.True(File.Exists(Save(surface.Window, "phrases-game-action-patterns.png")));
    }

    [AvaloniaFact]
    public void TheWordingPickerOffersAGameActionByItsShortPhrases()
    {
        using var surface = Open(actions: true);

        var picker = surface.Page.GetVisualDescendants().OfType<InlinePicker>().Single();
        picker.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        var options = picker.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains(InlinePicker.OptionClass))
            .Select(button => AutomationProperties.GetName(button))
            .ToList();

        Assert.Contains("gear down", options);
        Assert.Contains("gear up", options);
        Assert.Contains("gear", options);
        Assert.DoesNotContain("turn on landing gear", options);
        Assert.DoesNotContain("[gear|landing gear] [down|up]", options);
    }

    [AvaloniaFact]
    public void TheHelpLineNamesTeachingByVoice()
    {
        using var surface = Open();

        Assert.Contains(PhrasesPage.Hint, Drawn(surface.Page));
        Assert.Contains("“teach a phrase”", PhrasesPage.Hint, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AddingPutsTheNewPhraseAtTheTopAndClearsTheForm()
    {
        using var surface = Open();

        Type(surface, "[please|] drop the [gear|wheels]");
        Pick(surface, "teach a phrase");
        Add(surface);

        Assert.Contains(surface.Store.For("F1"), phrase => phrase.Said == "[please|] drop the [gear|wheels]");
        Assert.Equal(string.Empty, Say(surface).Text);
        Assert.False(Notice(surface).IsVisible);

        var drawn = Drawn(surface.Page).ToList();
        Assert.True(drawn.IndexOf("[please|] drop the [gear|wheels]") < drawn.IndexOf("[gear|wheels] out"));
    }

    [AvaloniaFact]
    public void EnterAddsAsTheTileDoes()
    {
        using var surface = Open(seed: false);

        Type(surface, "start teaching");
        Pick(surface, "teach a phrase");

        Say(surface).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("teach a phrase", surface.Store.PhraseFor("F1", "start teaching"));
    }

    [AvaloniaFact]
    public void ABuiltInClashIsRefusedAndNamesItsCapability()
    {
        using var surface = Open();

        Type(surface, "[teach a phrase|go teach]");
        Pick(surface, "new phrase");
        Add(surface);

        var notice = Notice(surface);

        Assert.True(notice.IsVisible);
        Assert.Contains(FieldMessage.ErrorClass, Say(surface).Classes);
        Assert.StartsWith("“teach a phrase” is already a phrase D47 knows: start teaching", notice.Text, StringComparison.Ordinal);
        Assert.Equal("BUILT-IN PHRASE · PHRASES", notice.Detail);
        Assert.DoesNotContain(surface.Store.For("F1"), phrase => phrase.Said.Contains("go teach", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AClashWithYourOwnPhraseNamesItsPattern()
    {
        using var surface = Open();

        Type(surface, "wheels out");
        Pick(surface, "teach a phrase");
        Add(surface);

        var notice = Notice(surface);

        Assert.Equal("“wheels out” already stands for “new phrase”. Choose another wording.", notice.Text);
        Assert.Equal("YOUR PHRASE · [GEAR|WHEELS] OUT", notice.Detail);
    }

    [AvaloniaFact]
    public void AnEmptyFieldSaysThereIsNothingToAdd()
    {
        using var surface = Open();

        Add(surface);

        Assert.True(Notice(surface).IsVisible);
        Assert.Equal(PhrasesPage.NothingToAddLabel, Notice(surface).Label);
    }

    [AvaloniaFact]
    public void AWordingWithNoPhraseAsksForOne()
    {
        using var surface = Open();

        Type(surface, "hit it");
        Add(surface);

        Assert.Equal(PhrasesPage.PickAPhraseLabel, Notice(surface).Label);
        Assert.Null(surface.Store.PhraseFor("F1", "hit it"));
    }

    [AvaloniaFact]
    public void EditingTheFieldClearsTheNotice()
    {
        using var surface = Open();

        Type(surface, "teach a phrase");
        Pick(surface, "new phrase");
        Add(surface);
        Assert.True(Notice(surface).IsVisible);

        Type(surface, "teach a phrase please");

        Assert.False(Notice(surface).IsVisible);
        Assert.DoesNotContain(FieldMessage.ErrorClass, Say(surface).Classes);
    }

    [AvaloniaFact]
    public void TheSearchFiltersYourPhrasesAndTheBuiltInOnes()
    {
        using var surface = Open();

        surface.Page.Filter("wheels");
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(surface.Page);
        Assert.Contains("[gear|wheels] out", drawn);
        Assert.DoesNotContain("show me every setting", drawn);

        surface.Page.Filter("every setting");
        Dispatcher.UIThread.RunJobs();

        drawn = Drawn(surface.Page);
        Assert.DoesNotContain("[gear|wheels] out", drawn);
        Assert.Contains("show me every setting", drawn);

        surface.Page.Filter("zzz");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(PhrasesPage.NoMatch("zzz"), Drawn(surface.Page));
    }

    [AvaloniaFact]
    public void TheForgetGlyphRemovesThePhrase()
    {
        using var surface = Open();

        var forget = surface.Page.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == PhrasesPage.Forget);

        forget.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Store.For("F1"));
        Assert.Contains(PhrasesPage.NothingTaught, Drawn(surface.Page));
    }

    /// <summary>The clash notice, the open list and the added phrase, saved for a look against the handoff (24, 25, 26).</summary>
    [AvaloniaFact]
    public void ThePhrasesPageIsCaptured()
    {
        using var look = AppLook.Put();

        using (var surface = Open(width: 1280, height: 1100))
        {
            Type(surface, "teach a phrase");
            Pick(surface, "new phrase");
            Add(surface);
            Assert.True(File.Exists(Save(surface.Window, "phrases-clash.png")));
        }

        using (var surface = Open(width: 1280, height: 1100))
        {
            Type(surface, "hit it");
            surface.Page.GetVisualDescendants().OfType<InlinePicker>().Single().IsOpen = true;
            Assert.True(File.Exists(Save(surface.Window, "phrases-pick.png")));
        }

        using (var surface = Open(width: 1280, height: 1100))
        {
            Type(surface, "hit it");
            Pick(surface, "teach a phrase");
            Add(surface);
            Assert.True(File.Exists(Save(surface.Window, "phrases-added.png")));
        }
    }

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        return path;
    }

    [AvaloniaFact]
    public async Task TheModelCallingForgetIsRefused()
    {
        using var surface = Open();

        var registry = CapabilityRegistry.Build(
        [
            LearnedPhrasesCapability.Create(surface.Store, () => "F1"),
        ]);

        var result = await registry.InvokeAsync(
            LearnedPhrasesCapability.ForgetTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = "[gear|wheels] out" }),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.NotEmpty(surface.Store.For("F1"));
    }
}
