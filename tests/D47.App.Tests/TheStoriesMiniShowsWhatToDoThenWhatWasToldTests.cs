using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Stories mini draws the title line, then what to do next or why there is nothing to do, then the told log
/// newest first; it follows a chapter write without being reattached.
/// </summary>
[Trait("Category", "Integration")]
public class TheStoriesMiniShowsWhatToDoThenWhatWasToldTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    private sealed record Mini(Window Window, PanelView Panel, AdventureSurface Surface)
    {
        public StoryDirector Director => Surface.Stories!;
    }

    private const string Title = "THE FIRST LIGHT ·\u00A0Step\u00A01\u00A0of\u00A01";

    private static Mini Open(
        Func<AdventureAsk, DateTimeOffset, CancellationToken, Task<AdventureOutcome>>? write = null, SpeakerPortraits? portraits = null)
    {
        var surface = NotForMeGroupFixture.Build(TriggerKind.Arrive, write: write);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Mini };

        if (portraits is not null)
        {
            panel.EnableSpeakerPictures(portraits);
        }

        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = PanelResolution.Mini.Width, Height = PanelResolution.Mini.Height };
        window.Show();

        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return new Mini(window, panel, surface);
    }

    private static void Tell(Mini mini, string text, int minutes, string? speaker = null) =>
        mini.Surface.Book.Told("F1", NotForMeGroupFixture.ChapterKey, new AdventureTold
        {
            Kind = speaker is null ? AdventureToldKind.Nudge : AdventureToldKind.Beat,
            Speaker = speaker,
            Line = speaker is null ? 0 : 1,
            Text = text,
            At = Now.AddMinutes(minutes),
        });

    private static AdventureMini Pane(Mini mini) =>
        mini.Panel.GetVisualDescendants().OfType<AdventureMini>().Single(pane => pane.IsEffectivelyVisible);

    private static string Said(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines ? inlines.Text ?? string.Empty : block.Text ?? string.Empty;

    private static TextBlock? Block(Mini mini, Func<string, bool> matches) =>
        Pane(mini).GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => matches(Said(block)));

    private static double Top(Mini mini, Func<string, bool> matches) =>
        Block(mini, matches)!.TranslatePoint(default, Pane(mini))!.Value.Y;

    private static bool Shows(Mini mini, string text) => Block(mini, said => said == text) is not null;

    private static bool HasBar(Mini mini) => Pane(mini).GetVisualDescendants().OfType<WriteProgressBar>().Any();

    private static bool Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        return done();
    }

    private static void Save(Mini mini, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = mini.Window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void ARunningStoryShowsItsObjectiveThenItsNewestEntryFirst()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var pictures = new SpeakerPictureFixture("d47-stories-mini-pictures");
        pictures.Ship(SpeakerPictures.Narrator, Avalonia.Media.Colors.SteelBlue);

        var mini = Open(portraits: pictures.Portraits);

        Tell(mini, "Somebody is still paying for that beacon.", 1);
        Tell(mini, "The signal repeats every forty seconds.", 2, speaker: "Ilse Varga");
        Tell(mini, "Go and look before someone else does.", 3);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(mini, Title));

        var title = Top(mini, said => said == Title);
        var next = Top(mini, said => said.StartsWith("Next: ", StringComparison.Ordinal));
        var newest = Top(mini, said => said == "Go and look before someone else does.");
        var middle = Top(mini, said => said == "The signal repeats every forty seconds.");
        var oldest = Top(mini, said => said == "Somebody is still paying for that beacon.");

        Assert.True(title < next && next < newest && newest < middle && middle < oldest);

        // The speaker line and the Narrator's picture, the way the chapter's page draws an entry.
        Assert.True(Shows(mini, "Ilse Varga"));
        Assert.Equal(2, Pane(mini).GetVisualDescendants().OfType<Image>().Count(image => image.Name == "SpeakerPicture"));

        // No Story on box, no premise, no Done row, no quoted last line.
        Assert.Empty(Pane(mini).GetVisualDescendants().OfType<CheckBox>());
        Assert.Null(Block(mini, said => said.StartsWith("Done:", StringComparison.Ordinal)));
        Assert.Null(Block(mini, said => said.StartsWith('“')));

        Save(mini, "stories-mini-running.png");
        mini.Window.Close();
    }

    [AvaloniaFact]
    public void PageDownReachesTheMinisScroller()
    {
        var mini = Open();

        for (var minute = 1; minute <= 6; minute++)
        {
            Tell(mini, $"Entry {minute}: the beacon keeps its own counsel and the dust keeps the rest of it.", minute);
        }

        Dispatcher.UIThread.RunJobs();

        var scroller = Pane(mini).GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height);

        Assert.Equal(PanelScrollOutcome.Moved, mini.Panel.Scroll(PanelScrollStep.PageDown));
        Assert.True(scroller.Offset.Y > 0);

        mini.Window.Close();
    }

    [AvaloniaFact]
    public void AChapterBeingWrittenShowsTheBarAndItsFailureWithoutAReattach()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);
        var written = new TaskCompletionSource<AdventureOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mini = Open((_, _, _) => written.Task);

        Assert.False(HasBar(mini));

        _ = mini.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        Assert.True(Until(() => mini.Director.IsWriting("F1") && HasBar(mini)));
        Assert.Null(Block(mini, said => said.StartsWith("Next: ", StringComparison.Ordinal)));

        Save(mini, "stories-mini-writing.png");

        written.SetResult(new AdventureOutcome(null, null, AdventureOutcome.NotWritten, []));

        Assert.True(Until(() => !mini.Director.IsWriting("F1") && !HasBar(mini) && Shows(mini, "Chapter 2 could not be written.")));

        mini.Window.Close();
    }

    [AvaloniaFact]
    public void APausedStorySaysWhichChapterWasAbandoned()
    {
        var mini = Open();
        var story = mini.Director.Stories.Current("F1")!;

        mini.Director.Stories.Save("F1", story with { State = StoryState.Paused });

        Assert.True(Until(() => Shows(mini, "Paused, chapter 1 was abandoned.")));
        Assert.True(Shows(mini, Title));
        Assert.Null(Block(mini, said => said.StartsWith("Next: ", StringComparison.Ordinal)));

        mini.Window.Close();
    }

    [AvaloniaFact]
    public void ASwitchedOffStorySaysSo()
    {
        var mini = Open();
        var story = mini.Director.Stories.Current("F1")!;

        mini.Director.Stories.Save("F1", story.SwitchedOff(Now));

        Assert.True(Until(() => Shows(mini, "Switched off. No objectives, nudges or clues until you switch it on.")));
        Assert.Null(Block(mini, said => said.StartsWith("Next: ", StringComparison.Ordinal)));

        mini.Window.Close();
    }

    [AvaloniaFact]
    public void ADoneChapterOfARunningStorySaysEndOfChapter()
    {
        var mini = Open();

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-10-14T20:05:00Z", "event":"FSDJump", "StarSystem":"IC 2391 Sector MX-T b3-6", "SystemAddress":13872878396833 }""",
            NullLogger.Instance,
            out var jump));

        mini.Surface.Book.Observe(jump!, "F1");

        Assert.True(Until(() => Shows(mini, "End of chapter.")));
        Assert.False(Shows(mini, "The end."));
        Assert.Null(Block(mini, said => said.StartsWith("Next: ", StringComparison.Ordinal)));

        mini.Window.Close();
    }
}
