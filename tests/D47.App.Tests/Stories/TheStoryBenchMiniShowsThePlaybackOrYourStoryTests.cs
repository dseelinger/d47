using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>On the Story Bench root, the mini panel shows the playback while a run plays and the Your story mini otherwise.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryBenchMiniShowsThePlaybackOrYourStoryTests
{
    private static readonly Story Run = new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = "-",
        PickedAt = new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero),
        State = StoryState.Running,
        Chapters = ["ch1"],
    };

    private static StoryRunLine Line(int number) => new()
    {
        Sent = Run.PickedAt.AddMinutes(number),
        From = D47.Core.Messages.MessageStore.Narrator,
        Key = $"line {number}",
        Body = $"Line number {number} of the run.",
        Clip = $"line-{number}.opus",
    };

    [AvaloniaFact]
    public void TheBenchRootShowsYourStoryThenThePlaybackThenYourStoryAgain()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var paths = new AppPaths(TestSurface.MemoryFolder("d47-bench-mini"));
        paths.EnsureCreated();
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), TestSurface.FilesFor(paths), NullLogger<StoryStore>.Instance);
        var baseSurface = AdventureFixture.Surface(paths);
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);
        var director = new StoryDirector(
            stories, baseSurface.Book, () => catalog,
            (_, _, _) => Task.FromResult(new D47.Core.Adventures.AdventureOutcome(null, null, "Not written.", [])),
            () => null, _ => { }, NullLogger.Instance);

        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var model = new PanelViewModel
        {
            StoryPlayer = player,
            StoryPlaybackProgress = () => new TrackProgress(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10)),
        };

        using var look = AppLook.Put();
        var view = new PanelView { DataContext = model };
        view.EnableAdventures(baseSurface with { Stories = director });
        var window = new Window { Width = 792, Height = 280, Content = view };
        window.Show();
        view.Tab = PanelTab.Stories;
        Assert.True(view.Nav.SelectRoot(PanelTab.Stories, StoryBenchPage.RootKey));
        view.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Mini(view).IsEffectivelyVisible);
        Assert.False(Readout(view).IsVisible);
        window.SaveFrame("story-bench-mini-idle.png");

        player.Play(Run, [Line(1), Line(2)], 0);
        model.StoryPlayback = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Readout(view).IsVisible);
        Assert.False(Mini(view).IsEffectivelyVisible);
        window.SaveFrame("story-bench-mini-playing.png");

        player.Stop();
        model.StoryPlayback = false;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Mini(view).IsEffectivelyVisible);
        Assert.False(Readout(view).IsVisible);

        window.Close();
    }

    private static AdventureMini Mini(PanelView view) => view.GetVisualDescendants().OfType<AdventureMini>().Single();

    private static StoryPlaybackReadout Readout(PanelView view) =>
        view.GetVisualDescendants().OfType<StoryPlaybackReadout>().Single(c => c.Name == "StoryReadout");
}
