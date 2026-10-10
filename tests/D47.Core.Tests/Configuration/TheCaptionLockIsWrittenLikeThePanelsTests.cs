using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>What <c>vr.captions.lock</c> puts in the file.</summary>
public class TheCaptionLockIsWrittenLikeThePanelsTests
{
    [Fact]
    public void TheWordInTheFileIsTheWordTheRowOffers()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(VrCapability.CaptionLockKey, "world", SettingsCaller.Panel).Status);

        var written = install.Files.ReadText(install.Paths.SettingsFile);

        Assert.Contains("\"lock\": \"world\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("worldLocked", written, StringComparison.Ordinal);

        // The reading of the word is derived, so it must not be written beside the word itself: settings.json
        // is append-only, and a key nothing reads is one nobody can take back out.
        Assert.DoesNotContain("\"locking\"", written, StringComparison.Ordinal);
    }

    [Fact]
    public void ItSurvivesARestart()
    {
        var install = new MemoryInstall();

        TestSurface.For(install).Settings.Apply(VrCapability.CaptionLockKey, "world", SettingsCaller.Panel);

        // A second store over the same folder, which is a restart in effect.
        var reloaded = TestSurface.For(install);

        Assert.Equal(SurfaceLock.WorldLocked, reloaded.Settings.Current.Vr.Captions.Locking);
        Assert.Equal("world", reloaded.Settings.Read(VrCapability.CaptionLockKey));
    }

    /// <summary>A file that was written before this row existed, which is every file there is.</summary>
    [Fact]
    public void AFileFromBeforeTheRowReadsAsHeadLocked()
    {
        var install = new MemoryInstall();

        install.Files.WriteText(
            install.Paths.SettingsFile,
            """{ "vr": { "captions": { "enabled": true, "size": "large" } } }""");

        var surface = TestSurface.For(install);

        Assert.Equal(SurfaceLock.HeadLocked, surface.Settings.Current.Vr.Captions.Locking);
        Assert.Equal(CaptionSize.Large, surface.Settings.Current.Vr.Captions.Size);
    }

    /// <summary>
    /// And a file hand-edited to a word nobody recognises loads, rather than taking every other setting
    /// in it down with it.
    /// </summary>
    [Fact]
    public void AWordNobodyRecognisesReadsAsHeadLockedRatherThanRefusingToLoad()
    {
        var install = new MemoryInstall();

        install.Files.WriteText(
            install.Paths.SettingsFile,
            """{ "vr": { "captions": { "lock": "footwell" } } }""");

        var surface = TestSurface.For(install);

        Assert.Equal(SurfaceLock.HeadLocked, surface.Settings.Current.Vr.Captions.Locking);
    }
}
