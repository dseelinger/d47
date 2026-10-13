using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>While a story cast member's line plays, the avatar shows their picture whole in place of the core, then the core again (#1139).</summary>
[Trait("Category", "Integration")]
public sealed class TheAvatarShowsTheCastMemberSpeakingTests : IDisposable
{
    private const string Member = "story.night-watch.vasquez";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-cast-avatar-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    /// <summary>A story's downloaded picture for <see cref="Member"/>, from a shipped portrait.</summary>
    private static SpeakerPictureFixture WithMember()
    {
        var fixture = new SpeakerPictureFixture("d47-cast-avatar");
        var portrait = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "assets", "portraits", "captain.woman.jpg"));
        fixture.Files.WriteBytes(Path.Combine(fixture.Paths.Stories, Member + ".jpg"), portrait);

        return fixture;
    }

    [AvaloniaFact]
    public void ACastLineShowsTheMembersPictureUntilItEnds()
    {
        var fixture = WithMember();
        var (window, panel) = fixture.Open();

        fixture.Model.SpeakingPicture = Member;
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(panel.Avatar.Speaker);
        Assert.Same(panel.Avatar.Speaker, panel.Avatar.Picture.Source);
        Assert.True(panel.Avatar.Picture.IsVisible);
        Assert.Equal(Stretch.Uniform, panel.Avatar.Picture.Stretch);
        Assert.Null(panel.Avatar.ShowingClip);

        fixture.Model.SpeakingPicture = null;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(panel.Avatar.Speaker);
        Assert.False(panel.Avatar.Picture.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TheLoopMovingMidLineKeepsThePicture()
    {
        var fixture = WithMember();
        var (window, panel) = fixture.Open();

        fixture.Model.SpeakingPicture = Member;
        fixture.Model.LoopState = LoopState.Answered;
        fixture.Model.CoreId = "covas";
        Dispatcher.UIThread.RunJobs();

        Assert.Same(panel.Avatar.Speaker, panel.Avatar.Picture.Source);
        Assert.True(panel.Avatar.Picture.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void AMemberWithNoPictureLeavesTheAvatarAsItIs()
    {
        var fixture = new SpeakerPictureFixture("d47-cast-avatar-none");
        var (window, panel) = fixture.Open();

        fixture.Model.SpeakingPicture = "story.night-watch.nobody";
        Dispatcher.UIThread.RunJobs();

        Assert.Null(panel.Avatar.Speaker);
        Assert.False(panel.Avatar.Picture.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePictureStopsTheCoresClipAndTheClipComesBackAfter()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.AvatarClips);
        File.Copy(
            Path.Combine(RepositoryRoot(), "tests", "fixtures", "avatars", "clip.mp4"),
            Path.Combine(paths.AvatarClips, CoreClips.FileName("covas", LoopState.Speaking)));

        var fixture = WithMember();
        var view = new AvatarView { Library = AvatarLibrary.Load(new DiskFileSystem(), paths) };
        view.Show(LoopState.Speaking);
        view.Core = "covas";

        Assert.NotNull(view.ShowingClip);

        view.Speaker = fixture.Portraits.For(Member, 208);

        Assert.Null(view.ShowingClip);
        Assert.False(view.HasTimer);
        Assert.Same(view.Speaker, view.Picture.Source);

        view.Speaker = null;

        Assert.NotNull(view.ShowingClip);
    }

    [AvaloniaFact]
    public void TheHeaderAvatarIsCapturedHoldingACastPicture()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(null);

        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.AvatarClips);
        File.Copy(
            Path.Combine(RepositoryRoot(), "tests", "fixtures", "avatars", "clip.mp4"),
            Path.Combine(paths.AvatarClips, CoreClips.FileName("covas", LoopState.Idle)));

        var fixture = WithMember();
        fixture.Model.CoreId = "covas";
        var (window, panel) = fixture.Open();
        panel.Avatar.Still = true;
        panel.Avatar.Library = AvatarLibrary.Load(new DiskFileSystem(), paths);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(panel.Avatar.ShowingClip);

        using (var before = window.CaptureRenderedFrame()!)
        {
            before.SaveCapture("cast-avatar-before.png");
        }

        fixture.Model.SpeakingPicture = Member;
        Dispatcher.UIThread.RunJobs();

        using var during = window.CaptureRenderedFrame()!;
        during.SaveCapture("cast-avatar-speaking.png");

        window.Close();
    }
}
