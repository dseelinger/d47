using D47.App.Headset;
using D47.App.Panel;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>A headset over a temp install whose avatar clips are the fixture clip, for every state of the covas core.</summary>
[Trait("Category", "Integration")]
internal sealed class HeadsetAvatarRig : IDisposable
{
    internal HeadsetAvatarRig(string folderPrefix)
    {
        Root = Path.Combine(Path.GetTempPath(), folderPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new AppPaths(Root);
        Directory.CreateDirectory(Paths.AvatarClips);
    }

    internal string Root { get; }

    internal AppPaths Paths { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    internal static string Fixture() =>
        Path.Combine(AppSource.RepositoryRoot(), "tests", "fixtures", "avatars", "clip.mp4");

    /// <summary>The full panel 1250×690 at zoom 80 and the mini 792×280 at 100, with every state's clip.</summary>
    internal (VrPanelSurface Panel, SettingsService Settings) Headset(string mode, string? dumpTo = null)
    {
        foreach (var state in Enum.GetValues<LoopState>())
        {
            File.Copy(Fixture(), Path.Combine(Paths.AvatarClips, CoreClips.FileName("covas", state)), overwrite: true);
        }

        var (settings, _, _) = TestSurface.Create();

        settings.Replace(
            "test",
            current => current with
            {
                Vr = current.Vr with
                {
                    Mode = mode,
                    Panel = current.Vr.Panel with { Pixels = "1250x690", Zoom = 80 },
                    Mini = current.Vr.Mini with { Pixels = "792x280", Zoom = 100 },
                },
            });

        var panel = new VrPanelSurface(
            new PanelViewModel { CoreId = "covas" },
            settings,
            _ => null,
            avatars: AvatarLibrary.Load(new DiskFileSystem(), Paths),
            dumpTo: dumpTo);

        return (panel, settings);
    }
}
