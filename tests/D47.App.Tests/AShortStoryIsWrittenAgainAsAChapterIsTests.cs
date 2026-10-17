using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Go on the Short Story page writes up to <see cref="StoryDirector.Drafts"/> drafts, as a story chapter does: an internal
/// failure is written again and told why, and anything else stops after one draft.
/// </summary>
public class AShortStoryIsWrittenAgainAsAChapterIsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    /// <summary>JSON, so the failure is a draft that came back and could not be read rather than no answer at all.</summary>
    private const string NotAStory = """{"weather": "fine"}""";

    private const string CutOffBeats = """{"opening": "Somebody is paying.", "beats": [{"title": "The Lantern" """;

    [AvaloniaFact]
    public void ADraftThatCannotBeReadIsWrittenAgainAndTheSecondIsOffered()
    {
        var model = new RecordingModel(Says(NotAStory), Says(AdventuresTabTests.Spine), Says(AdventuresTabTests.Beats));
        var (panel, book, said) = Open(model);

        Press(panel);
        Until(() => book.Store.Find("F1", "the-unrecoverable-column") is not null);

        Assert.True(book.Store.Find("F1", "the-unrecoverable-column")?.IsDraft);
        Assert.Equal(3, model.Asked.Count);
        Assert.Equal([StoryDirector.WritingAgainLine(2, told: true), "Here it is."], said);

        var beats = model.Asked[2];
        Assert.Contains("An earlier draft of this story could not be used", beats, StringComparison.Ordinal);
        Assert.Contains("The model's answer was not a story I could read.", beats, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheWritingAgainLineShowsUnderGoWhileTheNextDraftIsWritten()
    {
        var hold = new TaskCompletionSource();
        var model = new RecordingModel(Says(NotAStory), Says(AdventuresTabTests.Spine), Says(AdventuresTabTests.Beats)) { HoldFrom = 1, Hold = hold.Task };
        var (panel, book, _) = Open(model);

        Press(panel);
        Until(() => model.Asked.Count == 2);

        Assert.Equal(StoryDirector.WritingAgainLine(2, told: true), Status(panel).Text);

        hold.SetResult();
        Until(() => book.Store.Find("F1", "the-unrecoverable-column") is not null);
    }

    [AvaloniaFact]
    public void AfterTheLastDraftFailsThePageSaysWhatAChapterSays()
    {
        var model = new RecordingModel([.. Enumerable.Repeat(Says(NotAStory), StoryDirector.Drafts)]);
        var (panel, book, said) = Open(model);

        Press(panel);
        Until(() => Status(panel).Failed);

        Assert.Equal(StoryDirector.Drafts, model.Asked.Count);
        Assert.Equal(StoryDirector.NoDraftStood, Status(panel).Text);
        Assert.Equal(
            [.. Enumerable.Range(2, StoryDirector.Drafts - 1).Select(draft => StoryDirector.WritingAgainLine(draft, told: true)), StoryDirector.NoDraftStood],
            said);
        Assert.Empty(book.Store.For("F1"));
    }

    [AvaloniaFact]
    public void ARefusalTheCommanderCanActOnIsShownAfterOneDraft()
    {
        // The page believes a model is set; the generator finds none when it asks.
        var (panel, _, said) = Open(null, modelAvailable: true);

        Press(panel);
        Until(() => Status(panel).Failed);

        Assert.Equal("No language model is configured, and a short story has to be written by one.", Status(panel).Text);
        Assert.Equal(["No language model is configured, and a short story has to be written by one."], said);
    }

    [AvaloniaFact]
    public void AReplyCutOffByTheEndpointsContextIsNotWrittenAgain()
    {
        var model = new RecordingModel(
            Says(AdventuresTabTests.Spine),
            [new LlmStreamEvent.TextDelta(CutOffBeats), new LlmStreamEvent.Completed(new LlmUsage(6084, 2108, 0, 0), LlmStopReason.MaxTokens)]);
        var (panel, _, said) = Open(model);

        Press(panel);
        Until(() => Status(panel).Failed);

        Assert.Equal(2, model.Asked.Count);
        Assert.StartsWith("The model's context ran out.", Status(panel).Text, StringComparison.Ordinal);
        Assert.Single(said);
    }

    private static IReadOnlyList<LlmStreamEvent> Says(string text) =>
        [new LlmStreamEvent.TextDelta(text), new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)];

    private static (PanelView Panel, AdventureBook Book, List<string> Said) Open(RecordingModel? model, bool modelAvailable = false)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-short-story-drafts"));
        var store = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), TestSurface.FilesFor(paths), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        var state = AdventuresTabTests.State();
        IGalaxyService galaxy = new AdventuresTabTests.Galaxy();
        var said = new List<string>();

        var generator = new AdventureGenerator(
            () => model, () => null, () => null, () => null, () => null, () => state,
            () => galaxy, () => null, null, null, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, said.Add, () => modelAvailable || model is not null, () => true, () => null, () => { });

        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(surface);

        new Window { Content = panel, Width = 900, Height = 700 }.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(AdventuresPage.AskKey);
        Dispatcher.UIThread.RunJobs();

        return (panel, book, said);
    }

    private static Button Go(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Go"));

    private static StatusLine Status(PanelView panel) =>
        ((Avalonia.Controls.Panel)Go(panel).Parent!).Children.OfType<StatusLine>().Single();

    private static void Press(PanelView panel) =>
        Go(panel).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

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
        Assert.True(condition());
    }

    /// <summary>Plays one scripted round per call, keeping the text of every request; calls from <see cref="HoldFrom"/> on wait for <see cref="Hold"/>.</summary>
    private sealed class RecordingModel(params IReadOnlyList<LlmStreamEvent>[] rounds) : ILlmProvider
    {
        private readonly List<string> _asked = [];

        public int HoldFrom { get; init; } = int.MaxValue;

        public Task Hold { get; init; } = Task.CompletedTask;

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
            int round;

            lock (_asked)
            {
                round = _asked.Count;
                _asked.Add(string.Join("\n", request.Prompt.History.SelectMany(message => message.Content).OfType<ConversationContent.Text>().Select(part => part.Value)));
            }

            if (round >= HoldFrom)
            {
                await Hold.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (round >= rounds.Length)
            {
                throw new InvalidOperationException($"Round {round + 1} was asked for and {rounds.Length} were scripted.");
            }

            foreach (var streamEvent in rounds[round])
            {
                yield return streamEvent;
            }
        }
    }
}
