using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>What the flying Commander has taught D47 stands for a declared phrase, drawn and forgettable (#171).</summary>
public class TheLearnedPhrasesPageTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed record Surface(Window Window, PanelView Panel, LearnedPhrasesStore Store);

    private static Surface Open(bool seed = true)
    {
        var root = TempFolders.Create("d47-learned-phrases-page-tests");

        var store = new LearnedPhrasesStore(
            Path.Combine(root, "phrases.json"), NullLogger<LearnedPhrasesStore>.Instance);

        if (seed)
        {
            store.Learn("F1", "set focus on elite", "set focus to elite", At);
        }

        var gameState = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            NullLogger.Instance,
            out var commander));
        gameState.Apply(commander!);

        var registry = CapabilityRegistry.Build(
        [
            LearnedPhrasesCapability.Create(store, () => gameState.Active?.Identity.FrontierId ?? string.Empty),
        ]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(
            () => new TextBlock { Text = "Settings" },
            learnedPhrases: () => new LearnedPhrasesPage(registry, store, () => gameState.Active));

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();

        panel.Tab = PanelTab.Settings;
        Assert.True(panel.Nav.SelectRoot(LearnedPhrasesPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, store);
    }

    private static IReadOnlyList<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>The root is on the Settings tab, beside Settings itself.</summary>
    [AvaloniaFact]
    public void TheTabHasALearnedPhrasesRoot()
    {
        var surface = Open();

        Assert.Contains(
            surface.Panel.Nav.Roots(PanelTab.Settings),
            root => root.Key == LearnedPhrasesPage.RootKey && root.Word == "Learned phrases");

        surface.Window.Close();
    }

    /// <summary>A learned entry appears on the page, said next to the phrase it runs.</summary>
    [AvaloniaFact]
    public void ALearnedEntryAppearsOnThePage()
    {
        var surface = Open();

        var drawn = Drawn(surface.Panel);

        Assert.Contains(drawn, said => said.Contains("set focus on elite", StringComparison.Ordinal));
        Assert.Contains(drawn, said => said.Contains("set focus to elite", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>With nothing learned, the page says so rather than drawing an empty list.</summary>
    [AvaloniaFact]
    public void WithNothingLearnedThePageSaysSo()
    {
        var surface = Open(seed: false);

        Assert.Contains(Drawn(surface.Panel), said => said.Contains("Nothing learned yet", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>Pressing Forget removes the entry from the page and from the store.</summary>
    [AvaloniaFact]
    public void PressingForgetRemovesItFromThePageAndTheStore()
    {
        var surface = Open();

        var forget = surface.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Forget");

        forget.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(surface.Store.PhraseFor("F1", "set focus on elite"));
        Assert.Contains(Drawn(surface.Panel), said => said.Contains("Nothing learned yet", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>The tool the page and the router share is refused to the model.</summary>
    [AvaloniaFact]
    public async Task TheModelCallingForgetIsRefused()
    {
        var surface = Open();

        var registry = CapabilityRegistry.Build(
        [
            LearnedPhrasesCapability.Create(surface.Store, () => "F1"),
        ]);

        var result = await registry.InvokeAsync(
            LearnedPhrasesCapability.ForgetTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = "set focus on elite" }),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.NotNull(surface.Store.PhraseFor("F1", "set focus on elite"));

        surface.Window.Close();
    }
}
