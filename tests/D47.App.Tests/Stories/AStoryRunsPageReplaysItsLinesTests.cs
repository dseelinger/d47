using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>Open on a Story bench row drills to the run's page: every line in chapter sections, Jump to, and Play.</summary>
public sealed class AStoryRunsPageReplaysItsLinesTests
{
    private const string Commander = "F1";

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);

    private static readonly Story Run = new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = "-",
        PickedAt = Now.AddDays(-30),
        State = StoryState.Running,
        Chapters = ["ch1", "ch2", "ch3", "ch4"],
    };

    private sealed record Rig(Window Window, PanelView Panel, List<StoryRunLine> Played, MessageStore Messages, StoryRunPlayer Player, SteppedSink Sink);

    private static Adventure Chapter(string key, string name, params string[] beats) => new()
    {
        Key = key,
        Name = name,
        StoryId = Run.Id,
        Beats = [.. beats.Select(title => new AdventureBeat { Title = title })],
        AcceptedAt = Now.AddDays(-20),
        Told = [new AdventureTold { Kind = AdventureToldKind.Beat, Text = $"The text of {name}.", At = Now.AddDays(-20) }],
    };

    private static readonly Adventure[] Chapters =
    [
        Chapter("ch1", "Low tide", "The lights go out", "The second ring"),
        Chapter("ch2", "Static from Ross", "A voice", "Silence"),
        Chapter("ch3", "The relay goes dark", "Dark", "A voice on the wrong band"),
        Chapter("ch4", "What the beacon kept", "The beacon"),
    ];

    private static SpokenClip Spoken() => new([new AudioClip("line", new byte[960], AudioFormat.Standard)], "kokoro", "voice-a");

    private static Rig Open(bool lines = true, bool headset = false, Func<Story, StoryRunLine, Action<double>, Task<SpokenAgain>>? speakAgain = null)
    {
        var files = new MemoryFileSystem();
        var book = new AdventureBook(
            new AdventureStore("adventures.json", files, NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.InMemory();
        var record = new StoryRunRecord(files, "story-runs", stories, () => Commander, null, new NoopProtector(), NullLogger.Instance);
        var messages = new MessageStore("messages.json", files, NullLogger<MessageStore>.Instance) { Record = record };
        var generator = AdventureFixture.Generator();
        var director = new StoryDirector(
            stories, book, () => StoryFixture.Catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Records = record,
        };

        stories.Save(Commander, Run);

        foreach (var chapter in Chapters)
        {
            director.Archive.Append(new ArchivedChapter(Commander, Run.Id, Run.PickedAt, chapter, [], []));
        }

        if (lines)
        {
            var at = Run.PickedAt;

            foreach (var chapter in Chapters)
            {
                foreach (var beat in chapter.Beats)
                {
                    for (var line = 0; line < 8; line++)
                    {
                        at = at.AddMinutes(1);
                        messages.Post(MessageStore.Narrator, beat.Title, $"{chapter.Key} {beat.Title} {line}", at, chapter.Key, spoken: line == 0 ? Spoken() : null);
                    }
                }

                at = at.AddMinutes(1);
                messages.Post(MessageStore.Narrator, Run.Title, $"A clue after {chapter.Key}", at, $"story.clue.{Run.Id}.0");
            }
        }

        var played = new List<StoryRunLine>();
        var sink = new SteppedSink();
        var player = new StoryRunPlayer(
            new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => line.Clip is null ? null : new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work(),
            speakAgain);
        var surface = new AdventureSurface(
            book, generator, () => null, () => Commander, () => Now, _ => { }, () => true, () => null, () => { },
            Messages: messages, Stories: director,
            PlayRunLine: (_, line) =>
            {
                lock (played)
                {
                    played.Add(line);
                }

                return Task.FromResult<string?>(null);
            },
            RunPlayer: player);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        Window window;

        if (headset)
        {
            window = new Window { Content = new StoryRunPage(surface, director, new ToldEntries(surface, () => null), Run, headset: true), Width = 1280, Height = 900 };
        }
        else
        {
            panel.EnableAdventures(surface);
            window = new Window { Content = panel, Width = 1280, Height = 900 };
        }

        window.Show();

        if (!headset)
        {
            panel.Tab = PanelTab.Stories;
            panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        }

        Dispatcher.UIThread.RunJobs();
        return new Rig(window, panel, played, messages, player, sink);
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static StoryRunPage Opened(Rig rig)
    {
        if (rig.Window.Content is StoryRunPage own)
        {
            return own;
        }

        Press(rig.Panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == "OpenRun"));
        return rig.Panel.GetVisualDescendants().OfType<StoryRunPage>().Single();
    }

    private static List<string> Drawn(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Inlines is { Count: > 0 } inlines ? inlines.Text ?? string.Empty : block.Text ?? string.Empty)];

    private static void Pick(StoryRunPage page, string pickerName, string label)
    {
        var picker = page.GetVisualDescendants().OfType<InlinePicker>().Single(found => found.Name == pickerName);
        Press(picker.GetVisualDescendants().OfType<Button>().First(button => button.Name == InlinePicker.ButtonName));
        Press(picker.GetVisualDescendants().OfType<Button>().First(button =>
            button.Classes.Contains(InlinePicker.OptionClass) && AutomationProperties.GetName(button) == label));
    }

    /// <summary>How far row <paramref name="index"/> sits below the top of the list's viewport.</summary>
    private static double FromTop(StoryRunPage page, int index)
    {
        var list = page.GetVisualDescendants().OfType<ItemsControl>().Single(found => found.Name == StoryRunPage.ListName);
        var scroll = list.FindAncestorOfType<ScrollViewer>()!;
        var container = list.ContainerFromIndex(index)!;

        return container.TranslatePoint(default, scroll)!.Value.Y;
    }

    [AvaloniaFact]
    public void OpenDrillsToTheRunsPageUnderTheStoryBench()
    {
        var rig = Open();
        var page = Opened(rig);

        Assert.Equal([StoryBenchPage.RootKey, StoryRunPage.KeyOf(Run)], rig.Panel.Nav.Trail.Select(crumb => crumb.Key));
        Assert.Equal(Run.Title, rig.Panel.Nav.Trail[^1].Word);
        Assert.Equal(StoryRunPage.SummaryOf(Run.Title), page.Summary);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void AClueSaidBetweenTwoChaptersIsListedUnderTheChapterBeforeIt()
    {
        var rig = Open();
        var rows = Opened(rig).Rows;

        var clue = rows.OfType<StoryRunEntry>().Single(entry => entry.Line.Body == "A clue after ch1");
        var section = rows.Take(rows.ToList().IndexOf(clue)).OfType<StoryRunSection>().Last();

        Assert.Equal(1, section.Number);
        Assert.Equal(StoryRunLog.Clue, clue.Kind);
        Assert.IsType<StoryRunSection>(rows[rows.ToList().IndexOf(clue) + 1]);

        rig.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void JumpToScrollsToTheChosenChapterAndBeat(bool headset)
    {
        var rig = Open(headset: headset);
        var page = Opened(rig);
        var rows = page.Rows.ToList();

        Pick(page, StoryRunPage.ChapterPickerName, "Chapter 2 · Static from Ross");
        var chapter = rows.FindIndex(row => row is StoryRunSection { Number: 2 });
        Assert.InRange(FromTop(page, chapter), -1, 1);

        Pick(page, StoryRunPage.BeatPickerName, "Objective 2 · Silence");
        var beat = rows.FindIndex(row => row is StoryRunEntry entry && entry.Line.Body == "ch2 Silence 0");
        Assert.InRange(FromTop(page, beat), -1, 1);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void ANewLineLeavesAnOpenChapterListOpenAndAddsTheLine()
    {
        var rig = Open();
        var page = Opened(rig);
        var picker = page.GetVisualDescendants().OfType<InlinePicker>().Single(found => found.Name == StoryRunPage.ChapterPickerName);
        var before = page.Rows.Count;

        Press(picker.GetVisualDescendants().OfType<Button>().First(button => button.Name == InlinePicker.ButtonName));
        rig.Messages.Post(MessageStore.Narrator, "The beacon", "A new line.", Now, "ch4");
        Dispatcher.UIThread.RunJobs();

        Assert.True(picker.IsOpen);
        Assert.Equal(before + 1, page.Rows.Count);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void PlayPlaysTheLinesRecordedClipAndALineWithoutOneHasPlayAndTheNoClipNote()
    {
        var rig = Open();
        var page = Opened(rig);

        var plays = page.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "PlayLine" && button.IsEffectivelyVisible).ToList();
        var notes = page.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text == StoryRunPage.NoClipNote && text.IsEffectivelyVisible);

        Assert.NotEmpty(plays);
        Assert.True(notes > 0);
        Assert.Equal(
            page.GetVisualDescendants().OfType<Button>().Count(button => button.Name == "PlayAllLine" && button.IsEffectivelyVisible),
            plays.Count);

        Press(plays[0]);

        for (var attempt = 0; attempt < 200 && rig.Played.Count == 0; attempt++)
        {
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        var played = Assert.Single(rig.Played);
        Assert.Equal("ch1 The lights go out 0", played.Body);
        Assert.NotNull(played.Clip);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void ARunWithNoRecordedLinesShowsEachChaptersText()
    {
        var rig = Open(lines: false);
        var drawn = Drawn(Opened(rig));

        Assert.Contains(StoryRunPage.NoLinesText, drawn);
        Assert.Contains("Chapter 1 · Low tide", drawn, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("The text of Low tide.", drawn);
        Assert.DoesNotContain(rig.Panel.GetVisualDescendants().OfType<InlinePicker>(), picker => picker.IsEffectivelyVisible);

        rig.Window.Close();
    }

    private static Button Named(Control page, string name) =>
        page.GetVisualDescendants().OfType<Button>().First(button => button.Name == name && button.IsEffectivelyVisible);

    private static string Place(StoryRunPage page, int number) =>
        $"{number} of {page.Rows.OfType<StoryRunEntry>().Count()} · {MessagesView.Sender(MessageStore.Narrator)}".ToUpperInvariant();

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayAllMarksThePlayingLineAndShowsTheBar(bool headset)
    {
        var rig = Open(headset: headset);
        var page = Opened(rig);

        Press(Named(page, "PlayAllLine"));

        var drawn = Drawn(page);
        Assert.Contains("PLAYING " + Place(page, 1), drawn);
        Assert.Contains(StoryRunPage.SilencedText, drawn);
        Assert.Contains(StoryRunPage.PlayingMark.ToUpperInvariant(), drawn);
        Assert.Single(page.GetVisualDescendants().OfType<Border>(), border => border.Name == "PlayingLine");
        Assert.Equal(["ch1 The lights go out 0"], rig.Sink.Names);

        // Lines 2 to 8 keep no clip, so the next played is line 9.
        rig.Sink.FinishCurrent();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("PLAYING " + Place(page, 9), Drawn(page));
        Assert.Equal(["ch1 The lights go out 0", "ch1 The second ring 0"], rig.Sink.Names);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void ALineWithNoClipShowsSpeakingAgainThenShownForReading()
    {
        var asked = new Dictionary<string, TaskCompletionSource<SpokenAgain>>(StringComparer.Ordinal);
        var rig = Open(speakAgain: (_, line, _) =>
        {
            lock (asked)
            {
                if (!asked.TryGetValue(line.Body, out var answer))
                {
                    asked[line.Body] = answer = new TaskCompletionSource<SpokenAgain>();
                }

                return answer.Task;
            }
        });
        var page = Opened(rig);

        // The second line keeps no clip.
        Press(page.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "PlayAllLine" && button.IsEffectivelyVisible).ElementAt(1));

        var drawn = Drawn(page);
        Assert.Contains($"{StoryRunPage.SpeakingAgainLabel} · {Place(page, 2)}".ToUpperInvariant(), drawn);
        Assert.Contains(StoryRunPage.SpeakingAgainText(MessagesView.Sender(MessageStore.Narrator)), drawn);
        Assert.Contains(StoryRunPage.GeneratingText, drawn);
        using (var frame = ((Window)TopLevel.GetTopLevel(page)!).CaptureRenderedFrame()!)
        {
            frame.SaveCapture("story-run-speaking-again.png");
        }

        asked["ch1 The lights go out 1"].SetResult(SpokenAgain.Held(StoryLineHold.Failed, "HTTP 503 · ELEVENLABS"));
        var held = $"{StoryRunPage.ShownForReadingLabel} · {Place(page, 2)}".ToUpperInvariant();

        for (var attempt = 0; attempt < 200 && !Drawn(page).Contains(held); attempt++)
        {
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        drawn = Drawn(page);
        Assert.Contains($"{StoryRunPage.ShownForReadingLabel} · {Place(page, 2)}".ToUpperInvariant(), drawn);
        Assert.Contains(StoryRunPage.HeldText, drawn);
        Assert.Contains("0:04", drawn);
        Assert.Contains(StoryRunPage.NoVoiceLabel, drawn, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(StoryRunVoicing.Why(StoryLineHold.Failed), drawn);
        Assert.Contains(drawn, text => text.Contains("HTTP 503", StringComparison.Ordinal));
        using (var frame = ((Window)TopLevel.GetTopLevel(page)!).CaptureRenderedFrame()!)
        {
            frame.SaveCapture("story-run-shown-for-reading.png");
        }

        rig.Player.Stop();
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void PushToTalkPausesItSaysSoAndPlayCarriesOn()
    {
        var rig = Open();
        var page = Opened(rig);
        Press(Named(page, "PlayAllLine"));

        rig.Player.Pause(StoryRunPause.PushToTalk);
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(page);
        Assert.Contains("PAUSED · " + Place(page, 1), drawn);
        Assert.Contains(StoryRunPage.PushToTalkText, drawn);
        Assert.Equal("Play", Named(page, "PlaybackPause").Content);

        Press(Named(page, "PlaybackPause"));

        Assert.Contains(StoryRunPage.SilencedText, Drawn(page));
        Assert.Equal(["ch1 The lights go out 0", "ch1 The lights go out 0"], rig.Sink.Names);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void StopEndsItAndClearsTheMark()
    {
        var rig = Open();
        var page = Opened(rig);
        Press(Named(page, "PlayAllLine"));

        Press(Named(page, "PlaybackStop"));

        Assert.Null(rig.Player.Now);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Border>(), border => border.Name is "PlayingLine" or StoryRunPage.BarName && border.IsEffectivelyVisible);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void PlayAllOnTheJumpToRowWithNoBeatChosenStartsAtTheChaptersFirstLine()
    {
        var rig = Open();
        var page = Opened(rig);

        Pick(page, StoryRunPage.ChapterPickerName, "Chapter 2 · Static from Ross");
        Press(Named(page, "PlayAllFromJump"));

        Assert.Equal("ch2 A voice 0", rig.Player.Now!.Line.Body);

        rig.Window.Close();
    }

    [AvaloniaFact]
    public void PlayAllOnTheJumpToRowStartsAtTheChosenBeat()
    {
        var rig = Open();
        var page = Opened(rig);

        Pick(page, StoryRunPage.ChapterPickerName, "Chapter 2 · Static from Ross");
        Pick(page, StoryRunPage.BeatPickerName, "Objective 2 · Silence");
        Press(Named(page, "PlayAllFromJump"));

        Assert.Equal("ch2 Silence 0", rig.Player.Now!.Line.Body);

        rig.Window.Close();
    }
}
