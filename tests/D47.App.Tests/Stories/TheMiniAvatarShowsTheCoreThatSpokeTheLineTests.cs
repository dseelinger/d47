using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>
/// While the mini panel reads out a line a ship's core spoke, its avatar shows that core: speaking while the line is heard,
/// idle while paused. The core aboard comes back when the playback ends.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheMiniAvatarShowsTheCoreThatSpokeTheLineTests : IDisposable
{
    private const string Aboard = "cartographer";

    private static readonly Story Run = new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = "-",
        PickedAt = new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero),
        State = StoryState.Running,
        Chapters = ["ch1"],
    };

    private readonly HeadsetAvatarRig _rig = new("d47-mini-playback-clips-");

    public void Dispose() => _rig.Dispose();

    private static StoryRunLine Line(int number, string from) => new()
    {
        Sent = Run.PickedAt.AddMinutes(number),
        From = from,
        Key = $"line {number}",
        Body = $"Line number {number} of the run.",
        Clip = $"line-{number}.opus",
    };

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private string Clip(string core, LoopState state) => Path.Combine(_rig.Paths.AvatarClips, CoreClips.FileName(core, state));

    private (Window Window, PanelView View, PanelViewModel Model, StoryRunPlayer Player) Shown(params StoryRunLine[] lines)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        foreach (var core in new[] { Aboard, "kex", "warden" })
        {
            foreach (var state in new[] { LoopState.Idle, LoopState.Speaking })
            {
                File.Copy(HeadsetAvatarRig.Fixture(), Clip(core, state), overwrite: true);
            }
        }

        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var model = new PanelViewModel
        {
            CoreId = Aboard,
            StoryPlayer = player,
            StoryPlaybackProgress = () => new TrackProgress(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10)),
        };
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        view.Avatar.Library = AvatarLibrary.Load(new DiskFileSystem(), _rig.Paths);
        var window = new Window { Width = 792, Height = 280, Content = view };
        window.Show();
        Jobs();

        player.Play(Run, lines, 0);
        model.StoryPlayback = true;
        Jobs();

        return (window, view, model, player);
    }

    [AvaloniaFact]
    public void ACoresLineShowsThatCoreSpeakingInPlaceOfTheCoreAboard()
    {
        var (window, view, _, _) = Shown(Line(1, "kex"));

        Assert.Equal(Clip("kex", LoopState.Speaking), view.Avatar.ShowingClip);

        window.SaveFrame("story-playback-mini-core-speaking.png");
        window.Close();
    }

    [AvaloniaFact]
    public void TheNextLineByAnotherCoreChangesTheAvatarAndTheReadOut()
    {
        var (window, view, _, player) = Shown(Line(1, "kex"), Line(2, "warden"));

        player.Skip();
        Jobs();

        var readout = view.GetVisualDescendants().OfType<StoryPlaybackReadout>().Single();
        var text = readout.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == StoryPlaybackReadout.TextName);

        Assert.Equal(Clip("warden", LoopState.Speaking), view.Avatar.ShowingClip);
        Assert.Equal("Line number 2 of the run.", text.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PausingShowsTheLinesCoreIdleAndPlayShowsItSpeakingAgain()
    {
        var (window, view, _, player) = Shown(Line(1, "kex"), Line(2, "kex"));

        player.Pause(StoryRunPause.Commander);
        Jobs();

        Assert.Equal(Clip("kex", LoopState.Idle), view.Avatar.ShowingClip);

        player.Resume();
        Jobs();

        Assert.Equal(Clip("kex", LoopState.Speaking), view.Avatar.ShowingClip);

        window.Close();
    }

    [AvaloniaFact]
    public void TheLoopStateDoesNotReplaceTheSpeakingCoreWhileItsLinePlays()
    {
        var (window, view, model, _) = Shown(Line(1, "kex"));

        model.LoopState = LoopState.Idle;
        model.LoopState = LoopState.Speaking;
        Jobs();

        Assert.Equal(Clip("kex", LoopState.Speaking), view.Avatar.ShowingClip);

        window.Close();
    }

    [AvaloniaFact]
    public void ALineNoCoreSpokeLeavesTheCoreAboard()
    {
        var (window, view, _, _) = Shown(Line(1, "Anselm Coyle"));

        Assert.Equal(Clip(Aboard, LoopState.Idle), view.Avatar.ShowingClip);

        window.Close();
    }

    [AvaloniaFact]
    public void TheCoreAboardComesBackWhenThePlaybackEnds()
    {
        var (window, view, model, player) = Shown(Line(1, "kex"));

        player.Stop();
        model.StoryPlayback = false;
        Jobs();

        Assert.Equal(Clip(Aboard, LoopState.Idle), view.Avatar.ShowingClip);

        window.Close();
    }
}
