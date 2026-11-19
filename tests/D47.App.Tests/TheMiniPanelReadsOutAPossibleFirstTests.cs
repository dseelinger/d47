using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Tests.Stories;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A live possible first takes the mini panel's text area for 60 seconds, over a story playback, with the press in the headset (#2598).</summary>
[Trait("Category", "Integration")]
public sealed class TheMiniPanelReadsOutAPossibleFirstTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-20T08:30:00Z", CultureInfo.InvariantCulture);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static CodexFirstSeen Seen(CodexFirst first = CodexFirst.Region, long entryId = 101, string name = "Tubus Sororibus - Lime") =>
        new(entryId, 18, name, "Inner Orion Spur", first, "Wregoe AA-A d1", 1, 5, Start);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (Window Window, PanelView View, PanelViewModel Model, Clock Clock) Mini(PanelViewModel? model = null, bool headset = false, PanelMode mode = PanelMode.Mini)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var clock = new Clock();
        model ??= new PanelViewModel();
        model.Clock = clock;

        var view = new PanelView { DataContext = model, Mode = mode };
        view.Classes.Add("output-only");

        if (headset)
        {
            view.Classes.Add("headset");
        }

        var window = new Window { Width = 792, Height = 280, Content = view };
        window.Show();
        Jobs();

        return (window, view, model, clock);
    }

    private static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static T? Found<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name && c.IsEffectivelyVisible);

    private static string Text(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;

    [AvaloniaFact]
    public void ARegionFirstShowsItsWordsOnTheDesktopOverlay()
    {
        var (window, view, model, _) = Mini();

        model.ShowRegionFirst(Seen(), "Wregoe AA-A d1 5 a");
        Jobs();

        var readout = Named<RegionFirstReadout>(view, "RegionFirstReadout");
        Assert.True(readout.IsVisible);
        Assert.Equal("POSSIBLE REGION FIRST", Text(Named<TextBlock>(readout, RegionFirstReadout.LabelName)));
        Assert.Equal("TUBUS SORORIBUS - LIME", Text(Named<TextBlock>(readout, RegionFirstReadout.EntryName)));
        Assert.Equal("Inner Orion Spur · Wregoe AA-A d1 5 a", Text(Named<TextBlock>(readout, RegionFirstReadout.WhereName)));
        Assert.Equal("You may be the first Commander to log this in Inner Orion Spur.", Text(Named<TextBlock>(readout, RegionFirstReadout.TextName)));
        Assert.Equal(RegionFirstPress.SayHint, Text(Named<TextBlock>(readout, RegionFirstReadout.HintName)));
        Assert.Null(Found<Border>(readout, RegionFirstReadout.FaceName));

        window.SaveFrame("region-first-mini-overlay.png");
        window.Close();
    }

    [AvaloniaFact]
    public void AGalaxyFirstSaysAnywhereInTheGalaxy()
    {
        var (window, view, model, _) = Mini();

        model.ShowRegionFirst(Seen(CodexFirst.Galaxy), null);
        Jobs();

        var readout = Named<RegionFirstReadout>(view, "RegionFirstReadout");
        Assert.Equal("POSSIBLE FIRST IN THE GALAXY", Text(Named<TextBlock>(readout, RegionFirstReadout.LabelName)));
        Assert.Equal("You may be the first Commander to log this anywhere in the galaxy.", Text(Named<TextBlock>(readout, RegionFirstReadout.TextName)));
        Assert.Equal("Inner Orion Spur · Wregoe AA-A d1", Text(Named<TextBlock>(readout, RegionFirstReadout.WhereName)));
        window.Close();
    }

    [AvaloniaFact]
    public void TheFullPanelNeverShowsIt()
    {
        var (window, view, model, _) = Mini(mode: PanelMode.Full);

        model.ShowRegionFirst(Seen(), null);
        Jobs();

        Assert.False(Named<RegionFirstReadout>(view, "RegionFirstReadout").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ItLeavesAfterSixtySecondsAndItsLineShortensOnceASecond()
    {
        var (window, view, model, clock) = Mini();
        var readout = Named<RegionFirstReadout>(view, "RegionFirstReadout");

        model.ShowRegionFirst(Seen(), null);
        Jobs();
        Assert.Equal(1, readout.Left);

        clock.Now = Start.AddSeconds(0.4);
        Assert.False(view.TickRegionFirst());

        clock.Now = Start.AddSeconds(1);
        Assert.True(view.TickRegionFirst());
        Assert.Equal(59 / 60.0, readout.Left, 6);

        clock.Now = Start.AddSeconds(59.5);
        view.TickRegionFirst();
        Assert.True(readout.IsVisible);

        clock.Now = Start.AddSeconds(60);
        Assert.True(view.TickRegionFirst());
        Assert.False(readout.IsVisible);
        Assert.False(view.TickRegionFirst());

        window.Close();
    }

    [AvaloniaFact]
    public void ANewerFirstReplacesItAndStartsItsSixtySecondsAgain()
    {
        var (window, view, model, clock) = Mini();
        var readout = Named<RegionFirstReadout>(view, "RegionFirstReadout");

        model.ShowRegionFirst(Seen(), null);
        Jobs();

        clock.Now = Start.AddSeconds(30);
        model.ShowRegionFirst(Seen(entryId: 102, name: "Bacterium Nebulis"), null);
        Jobs();

        Assert.Equal("BACTERIUM NEBULIS", Text(Named<TextBlock>(readout, RegionFirstReadout.EntryName)));
        Assert.Equal(1, readout.Left);

        clock.Now = Start.AddSeconds(75);
        view.TickRegionFirst();
        Assert.True(readout.IsVisible);

        clock.Now = Start.AddSeconds(90);
        view.TickRegionFirst();
        Assert.False(readout.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ItShowsOverAStoryPlaybackWhichReturnsWhenItEnds()
    {
        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var model = new PanelViewModel
        {
            StoryPlayer = player,
            StoryPlaybackProgress = () => new TrackProgress(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10)),
        };
        var (window, view, _, clock) = Mini(model);
        var run = new Story
        {
            Id = StoryFixture.Story.Id,
            Title = StoryFixture.Story.Title,
            PublicLayer = "-",
            PickedAt = Start,
            State = StoryState.Running,
            Chapters = ["ch1"],
        };

        player.Play(run, [new StoryRunLine { Sent = Start, From = D47.Core.Messages.MessageStore.Narrator, Key = "line 1", Body = "Line one.", Clip = "line-1.opus" }], 0);
        model.StoryPlayback = true;
        Jobs();

        var story = Named<StoryPlaybackReadout>(view, "StoryReadout");
        Assert.True(story.IsVisible);

        model.ShowRegionFirst(Seen(), null);
        Jobs();

        Assert.True(Named<RegionFirstReadout>(view, "RegionFirstReadout").IsVisible);
        Assert.False(story.IsVisible);
        Assert.False(Named<Border>(view, "StoryReadoutProgress").IsVisible);

        clock.Now = Start.AddSeconds(60);
        view.TickRegionFirst();

        Assert.False(Named<RegionFirstReadout>(view, "RegionFirstReadout").IsVisible);
        Assert.True(story.IsVisible);
        window.Close();
    }

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        "{\"timestamp\":\"2026-10-02T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Wregoe AA-A d1\",\"SystemAddress\":1,\"StarPos\":[0.0,0.0,0.0]}",
        "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":101,\"Name\":\"$Codex_Ent_101;\",\"Name_Localised\":\"Tubus Sororibus - Lime\","
        + "\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Wregoe AA-A d1\",\"SystemAddress\":1,\"BodyID\":5,"
        + "\"Latitude\":12.4512,\"Longitude\":-108.2231,\"IsNewEntry\":true}",
    ];

    private sealed class Sender(bool reachable, Task? gate = null) : IRegionFirstReport
    {
        public async Task<bool> SendAsync(RegionFirstReport report, CancellationToken cancellationToken)
        {
            if (gate is not null)
            {
                await gate;
            }

            return reachable;
        }
    }

    private static (CommanderGameState State, RegionFirstBook Book, RegionFirstPress Press) Desk(bool reachable, Task? gate = null)
    {
        var state = JournalScript.Commander("F1", "Invented Pilot", Lines);
        var book = new RegionFirstBook("region-firsts.json", new MemoryFileSystem(), NullLogger.Instance, work => work());
        book.Observe("F1", CodexLedger.None.With(state.CodexLedger), CodexTable.Parse("# Table date: 2026-10-19\n101\tTubus\tBiology\tOrganic\t4:12\n"));
        var reporting = new RegionFirstReporting(() => state, book, new Sender(reachable, gate), "test", () => Start);
        var press = new RegionFirstPress((_, arguments, cancellation) => reporting.ReportAsync(arguments, cancellation), book, () => state);

        return (state, book, press);
    }

    private static (Window Window, RegionFirstReadout Readout) Readout()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var readout = new RegionFirstReadout();
        var window = new Window { Width = 512, Height = 280, Content = readout };
        window.Show();
        Jobs();

        return (window, readout);
    }

    [AvaloniaFact]
    public void TheHeadsetDrawsTheFaceAndTheOverlayTheHintAlone()
    {
        var (_, _, press) = Desk(reachable: true);
        var moment = new RegionFirstMoment(Seen(), null, Start);
        var (window, readout) = Readout();

        readout.Draw(moment, Start, press, headset: true, "Wregoe AA-A d1");
        Jobs();

        var face = Found<Border>(readout, RegionFirstReadout.FaceName);
        Assert.NotNull(face);
        Assert.Equal(D47.App.Theming.TypeScale.MinimumTarget, face.MinHeight);
        Assert.Contains(face.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "REPORT TO CANONN");
        Assert.Equal(RegionFirstPress.PressHint, Named<TextBlock>(readout, RegionFirstReadout.HintName).Text);
        window.SaveFrame("region-first-mini-headset.png");

        readout.Draw(moment, Start, press, headset: false, "Wregoe AA-A d1");
        Jobs();

        Assert.Null(Found<Border>(readout, RegionFirstReadout.FaceName));
        Assert.Equal(RegionFirstPress.SayHint, Named<TextBlock>(readout, RegionFirstReadout.HintName).Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task APressShowsTheWaitMarkThenTheSentLine()
    {
        var gate = new TaskCompletionSource();
        var (state, book, press) = Desk(reachable: true, gate.Task);
        var moment = new RegionFirstMoment(Seen(), null, Start);
        var (window, readout) = Readout();

        readout.Draw(moment, Start, press, headset: true, null);
        Jobs();

        var face = Found<Border>(readout, RegionFirstReadout.FaceName)!;
        var middle = face.TranslatePoint(new Point(face.Bounds.Width / 2, face.Bounds.Height / 2), window)!.Value;
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Jobs();

        var record = book.Find("F1", 101, 18)!;
        Assert.Equal(RegionFirstPressState.Sending, press.StateOf(record));
        Assert.True(readout.Draw(moment, Start, press, headset: true, null));
        Jobs();

        Assert.NotNull(Found<WaitingState>(readout, RegionFirstReadout.SendingName));
        Assert.Null(Found<Border>(readout, RegionFirstReadout.FaceName));

        gate.SetResult();

        for (var tries = 0; tries < 200 && press.StateOf(record) == RegionFirstPressState.Sending; tries++)
        {
            await Task.Delay(10);
            Jobs();
        }

        Assert.True(readout.Draw(moment, Start, press, headset: true, null));
        Jobs();

        Assert.Equal(RegionFirstPress.SentText, Found<TextBlock>(readout, RegionFirstReadout.SentName)?.Text);
        Assert.Null(Found<WaitingState>(readout, RegionFirstReadout.SendingName));
        Assert.NotNull(book.Find(state.Identity.FrontierId!, 101, 18)!.Reported);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AFailedPressDrawsTheInlineNoticeWithThePhraseThatAsksAgain()
    {
        var (_, book, press) = Desk(reachable: false);
        var moment = new RegionFirstMoment(Seen(), null, Start);
        var (window, readout) = Readout();

        await press.PressAsync(book.Find("F1", 101, 18)!);
        readout.Draw(moment, Start, press, headset: true, null);
        Jobs();

        var notice = Found<Notice>(readout, RegionFirstReadout.FailedName);
        Assert.NotNull(notice);
        Assert.Equal(RegionFirstPress.RetryText, notice.Text);
        Assert.Empty(notice.Actions);
        Assert.Equal("I could not reach Canonn just now. Say “report my region first” to try again.", RegionFirstPress.RetryText);
        Assert.Null(Found<Border>(readout, RegionFirstReadout.FaceName));
        window.Close();
    }

    [AvaloniaFact]
    public void ARecordReportedByVoiceShowsTheSentLineOnTheOverlay()
    {
        var (_, book, press) = Desk(reachable: true);
        var (window, readout) = Readout();

        book.MarkReported("F1", 101, 18, Start);
        readout.Draw(new RegionFirstMoment(Seen(), null, Start), Start, press, headset: false, null);
        Jobs();

        Assert.Equal(RegionFirstPress.SentText, Found<TextBlock>(readout, RegionFirstReadout.SentName)?.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSystemIsCyanOnlyWhileTheCommanderIsInIt()
    {
        var (window, readout) = Readout();
        var moment = new RegionFirstMoment(Seen(), "Wregoe AA-A d1 5 a", Start);

        readout.Draw(moment, Start, null, headset: false, "Wregoe AA-A d1");
        Jobs();
        var here = Named<TextBlock>(readout, RegionFirstReadout.WhereName).Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToList();
        Assert.Equal(["Inner Orion Spur · ", "Wregoe AA-A d1", " 5 a"], here.Select(run => run.Text));
        Assert.True(here[1].IsSet(Avalonia.Controls.Documents.TextElement.ForegroundProperty));

        readout.Draw(moment, Start, null, headset: false, "Elsewhere");
        Jobs();
        var away = Named<TextBlock>(readout, RegionFirstReadout.WhereName).Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToList();
        Assert.False(away[1].IsSet(Avalonia.Controls.Documents.TextElement.ForegroundProperty));
        window.Close();
    }
}
