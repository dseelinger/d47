using D47.Core.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>"ask for an adventure" opens the Ask page and its brief entry, and committing the brief presses Go with the form as it stands.</summary>
[Trait("Category", "Integration")]
public class AnAdventureIsAskedForByVoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private static (PanelView Panel, AdventureBook Book, AdventureCapability.AdventureDesk Desk, RecordingModel? Model) Open(
        bool canAsk, Task? hold = null)
    {
        var paths = new AppPaths(TempFolders.Create("d47-ask-by-voice-tests"));
        paths.EnsureCreated();

        var store = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        var state = AdventuresTabTests.State();
        var model = canAsk ? new RecordingModel(hold ?? Task.CompletedTask, AdventuresTabTests.Spine, AdventuresTabTests.Beats) : null;
        IGalaxyService? galaxy = canAsk ? new AdventuresTabTests.Galaxy() : null;

        var generator = new AdventureGenerator(
            () => model, () => null, () => null, () => null, () => null, () => state,
            () => galaxy, () => null, null, null, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, _ => { }, () => model is not null, () => galaxy is not null, () => null, () => { });

        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var desk = new AdventureCapability.AdventureDesk();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(surface, desk: desk);

        new Window { Content = panel, Width = 900, Height = 700 }.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, book, desk, model);
    }

    [AvaloniaFact]
    public void SayingItFromAnotherTabShowsTheAskPageWithTheBriefEntryOpen()
    {
        var (panel, _, desk, model) = Open(canAsk: true);

        Assert.NotEqual(PanelTab.Stories, panel.Nav.Tab);

        Assert.Null(desk.Ask());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Stories, panel.Nav.Tab);
        Assert.Equal([AdventuresPage.RootKey, AdventuresPage.AskKey, "adventure.brief"], panel.Nav.Trail.Select(crumb => crumb.Key));
        Assert.Empty(model!.Asked);
    }

    [AvaloniaFact]
    public void CommittingTheBriefAsksWithTheFormAsItStands()
    {
        var (panel, book, desk, model) = Open(canAsk: true);

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(new NavCrumb(AdventuresPage.AskKey, "Ask"));
        Dispatcher.UIThread.RunJobs();

        Choose(panel, "Reach: a session's flying");
        Choose(panel, "Length: short");

        Assert.Null(desk.Ask());
        Dispatcher.UIThread.RunJobs();

        panel.Prompts.Hear(new Heard("a lighthouse nobody tends", 1, Final: true));

        Until(() => book.Store.Find("F1", "the-unrecoverable-column") is not null);

        Assert.True(book.Store.Find("F1", "the-unrecoverable-column")?.IsDraft);
        Assert.Equal(AdventuresPage.ReadPrefix + "the-unrecoverable-column", panel.Nav.Trail[^1].Key);

        var spine = model!.Asked[0];
        Assert.Contains("The Commander's brief, in their words: \"a lighthouse nobody tends\"", spine, StringComparison.Ordinal);
        Assert.Contains("Reach: a session's flying", spine, StringComparison.Ordinal);
        Assert.Contains(AdventureGenerator.Structure(AdventureLength.Short).Sheet, model.Asked[1], StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ASecondAskWhileOneIsBeingWrittenStartsNothing()
    {
        var hold = new TaskCompletionSource();
        var (panel, book, desk, model) = Open(canAsk: true, hold.Task);

        Assert.Null(desk.Ask());
        Dispatcher.UIThread.RunJobs();
        panel.Prompts.Hear(new Heard("a lighthouse nobody tends", 1, Final: true));
        Until(() => model!.Asked.Count == 1);

        Assert.Null(desk.Ask());
        Dispatcher.UIThread.RunJobs();
        panel.Prompts.Hear(new Heard("a second story", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        hold.SetResult();
        Until(() => book.Store.Find("F1", "the-unrecoverable-column") is not null);

        Assert.Equal(2, model!.Asked.Count);
        Assert.Single(book.Store.For("F1"));
    }

    [AvaloniaFact]
    public void WithNoModelThePageOpensAndTheReasonIsSaidInsteadOfTheEntry()
    {
        var (panel, _, desk, _) = Open(canAsk: false);

        Assert.Equal("Asking for one needs a language model, and none is configured.", desk.Ask());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Stories, panel.Nav.Tab);
        Assert.Equal([AdventuresPage.RootKey, AdventuresPage.AskKey], panel.Nav.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>Pumps the UI thread while generation runs on the pool, until the condition holds.</summary>
    private static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Choose(PanelView panel, string option) =>
        panel.GetVisualDescendants().OfType<RadioButton>().Single(button => Equals(button.Content, option)).IsChecked = true;

    /// <summary>Replies in order once <paramref name="hold"/> completes, keeping the text of every request.</summary>
    private sealed class RecordingModel(Task hold, params string[] replies) : ILlmProvider
    {
        private readonly List<string> _asked = [];

        public IReadOnlyList<string> Asked
        {
            get
            {
                lock (_asked)
                {
                    return [.. _asked];
                }
            }
        }

        public string Id => "anthropic";

        public string DisplayName => "Recording";

        public string DefaultModel => "claude-opus-5";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var text = string.Join(
                "\n",
                request.Prompt.History.SelectMany(message => message.Content).OfType<ConversationContent.Text>().Select(part => part.Value));

            int round;

            lock (_asked)
            {
                round = _asked.Count;
                _asked.Add(text);
            }

            await hold.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (round >= replies.Length)
            {
                throw new InvalidOperationException($"Round {round + 1} was asked for and {replies.Length} were scripted.");
            }

            yield return new LlmStreamEvent.TextDelta(replies[round]);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}
