using System.IO.Compression;
using D47.App.Updates;
using D47.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Installing an update downloads a program and then runs it, which is the most dangerous thing
/// d47 does. These cover the things standing between a bad release response and an arbitrary
/// executable — where the bytes may come from, whether they hash to what the release published,
/// whether the archive is a d47 build at all — and the swap itself, which since v0.5.14 replaces
/// the executable <em>and</em> the native libraries beside it or none of them.
/// </summary>
public class UpdateInstallerTests
{
    [Theory]
    [InlineData("https://github.com/dseelinger/d47/releases/download/v0.2.1/d47.zip")]
    [InlineData("https://github.com/dseelinger/d47/releases/download/v9.9.9/d47.zip.sha256")]
    public void AnAssetOnThisRepositoryIsTrusted(string url)
    {
        Assert.True(UpdateChecker.IsTrustedDownloadUrl(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // Another host, and one that only reads like the right one.
    [InlineData("https://evil.example/dseelinger/d47/releases/download/v1/d47.zip")]
    [InlineData("https://github.com.evil.example/dseelinger/d47/releases/download/v1/d47.zip")]
    // The right host, someone else's repository, or one whose name merely starts the same.
    [InlineData("https://github.com/someone/else/releases/download/v1/d47.zip")]
    [InlineData("https://github.com/dseelinger/d47-evil/releases/download/v1/d47.zip")]
    // On this repository but not a release asset — a branch's copy of a file, say.
    [InlineData("https://github.com/dseelinger/d47/raw/main/d47.zip")]
    // Not the web at all. This is the one that matters: it would be started, not fetched.
    [InlineData(@"\attacker\share\payload.exe")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    public void AnythingElseIsRefused(string? url)
    {
        Assert.False(UpdateChecker.IsTrustedDownloadUrl(url));
    }

    /// <summary>The shape `sha256sum d47.zip` produces in the release workflow.</summary>
    [Fact]
    public void TheChecksumIsReadFromASha256sumSidecar()
    {
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            UpdateInstaller.ParseChecksum(
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  d47.zip\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // An error page where a checksum should be, which is what a moved URL actually returns.
    [InlineData("<!DOCTYPE html><html><body>Not Found</body></html>")]
    // Right length, not hex.
    [InlineData("zzzzc44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  d47.zip")]
    // Truncated hash.
    [InlineData("e3b0c44298fc1c14  d47.zip")]
    public void AnythingThatIsNotAHashIsNotAChecksum(string? body)
    {
        Assert.Null(UpdateInstaller.ParseChecksum(body));
    }

    /// <summary>
    /// A verified archive still has to be a d47 build. One without the executable is refused
    /// before anything on disk is touched.
    /// </summary>
    [Fact]
    public void AnArchiveWithoutTheExecutableIsRefused()
    {
        var (installer, folder) = Installer();

        var content = Directory.CreateDirectory(Path.Combine(folder, "not-a-build")).FullName;
        File.WriteAllText(Path.Combine(content, "readme.txt"), "no exe here");

        var archive = Path.Combine(folder, "bad.zip");
        ZipFile.CreateFromDirectory(content, archive);

        Assert.Null(installer.Extract(archive, "9.9.9"));
        Assert.False(Directory.Exists(Path.Combine(installer.StagingFolder, "d47-9.9.9")));
    }

    /// <summary>Bytes that are not a zip at all — a truncated transfer that still hashed right.</summary>
    [Fact]
    public void AnArchiveThatIsNotAnArchiveIsRefused()
    {
        var (installer, folder) = Installer();

        var archive = Path.Combine(folder, "garbage.zip");
        File.WriteAllText(archive, "not a zip");

        Assert.Null(installer.Extract(archive, "9.9.9"));
    }

    [Fact]
    public void ARealArchiveUnpacksToItsPayloadFolder()
    {
        var (installer, folder) = Installer();

        var payload = installer.Extract(BuildArchive(folder, "the new build"), "9.9.9");

        Assert.NotNull(payload);
        Assert.Equal("the new build", File.ReadAllText(Path.Combine(payload, "d47.exe")));
        Assert.True(File.Exists(Path.Combine(payload, "runtimes", "win-x64", "whisper.dll")));
    }

    /// <summary>
    /// The swap retires every running file rather than overwriting it, because Windows will not
    /// overwrite a running image — the exe, or a loaded native library — but will rename one.
    /// </summary>
    [Fact]
    public void TheSwapRetiresTheRunningBuildAndInstallsTheNewOne()
    {
        var (installer, folder) = Installer();

        var install = InstalledBuild(folder, "the old build");
        var payload = Payload(folder, "the new build");

        Assert.True(installer.TrySwap(install.Exe, payload));

        Assert.Equal("the new build", File.ReadAllText(install.Exe));
        Assert.Equal("the old build", File.ReadAllText(install.Exe + UpdateInstaller.RetiredSuffix));
        Assert.Equal("the new build", File.ReadAllText(install.Whisper));
        Assert.Equal("the old build", File.ReadAllText(install.Whisper + UpdateInstaller.RetiredSuffix));
    }

    /// <summary>A second update does not trip over what the first one retired.</summary>
    [Fact]
    public void ASecondUpdateReplacesTheAlreadyRetiredBuild()
    {
        var (installer, folder) = Installer();

        var install = InstalledBuild(folder, "the first build");
        File.WriteAllText(install.Exe + UpdateInstaller.RetiredSuffix, "an even older build");
        File.WriteAllText(install.Whisper + UpdateInstaller.RetiredSuffix, "an even older build");

        Assert.True(installer.TrySwap(install.Exe, Payload(folder, "the second build")));

        Assert.Equal("the second build", File.ReadAllText(install.Exe));
        Assert.Equal("the first build", File.ReadAllText(install.Exe + UpdateInstaller.RetiredSuffix));
        Assert.Equal("the first build", File.ReadAllText(install.Whisper + UpdateInstaller.RetiredSuffix));
    }

    /// <summary>
    /// A native library the new build no longer ships is retired, not left in place: Whisper's
    /// loader picks up whatever sits under runtimes\, and a stale library beside new ones is a
    /// load failure with nobody to blame.
    /// </summary>
    [Fact]
    public void ALibraryTheNewBuildDropsIsRetired()
    {
        var (installer, folder) = Installer();

        var install = InstalledBuild(folder, "the old build");
        var stale = Path.Combine(Path.GetDirectoryName(install.Whisper)!, "ggml-stale.dll");
        File.WriteAllText(stale, "no longer shipped");

        Assert.True(installer.TrySwap(install.Exe, Payload(folder, "the new build")));

        Assert.False(File.Exists(stale));
        Assert.Equal("no longer shipped", File.ReadAllText(stale + UpdateInstaller.RetiredSuffix));
    }

    /// <summary>
    /// If any file cannot be replaced, every file goes back — including ones already swapped.
    /// Ending with half an update is the one outcome worse than not updating.
    /// </summary>
    [Fact]
    public void AFailedInstallPutsTheWholeRunningBuildBack()
    {
        var (installer, folder) = Installer();

        var install = InstalledBuild(folder, "the old build");
        var payload = Payload(folder, "the new build");

        // The exe is swapped last, so holding it open fails the swap after the native library
        // has already been replaced — exactly the case the rollback exists for.
        using (File.Open(install.Exe, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(installer.TrySwap(install.Exe, payload));
        }

        Assert.Equal("the old build", File.ReadAllText(install.Exe));
        Assert.Equal("the old build", File.ReadAllText(install.Whisper));
        Assert.False(File.Exists(install.Whisper + UpdateInstaller.RetiredSuffix));
    }

    /// <summary>
    /// The Commander may have renamed the exe — it was "one file you put wherever you want
    /// it" for thirteen releases. The archive's d47.exe goes where the running executable
    /// actually is, whatever it is called there, rather than landing beside it by name.
    /// </summary>
    [Fact]
    public void ARenamedExecutableIsStillTheOneReplaced()
    {
        var (installer, folder) = Installer();

        var install = Directory.CreateDirectory(Path.Combine(folder, "install")).FullName;
        var exe = Path.Combine(install, "commander.exe");
        File.WriteAllText(exe, "the old build");

        Assert.True(installer.TrySwap(exe, Payload(folder, "the new build")));

        Assert.Equal("the new build", File.ReadAllText(exe));
        Assert.False(File.Exists(Path.Combine(install, "d47.exe")));
    }

    /// <summary>
    /// The narrowest rollback case: a retirement succeeds and the move-in then fails —
    /// antivirus holding the staged file, say. The retirement must be undone even though its
    /// replacement never arrived, or the file survives only as .old — which the next
    /// startup's cleanup deletes, and for the exe that is deleting the only copy.
    /// </summary>
    [Fact]
    public void AMoveThatFailsAfterItsRetirementRollsTheRetirementBack()
    {
        var (installer, folder) = Installer();

        var install = InstalledBuild(folder, "the old build");
        var payload = Payload(folder, "the new build");

        // Held open without delete sharing: its destination retires, then the move-in fails.
        using (File.Open(
                   Path.Combine(payload, "runtimes", "win-x64", "whisper.dll"),
                   FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(installer.TrySwap(install.Exe, payload));
        }

        Assert.Equal("the old build", File.ReadAllText(install.Whisper));
        Assert.False(File.Exists(install.Whisper + UpdateInstaller.RetiredSuffix));
        Assert.Equal("the old build", File.ReadAllText(install.Exe));
    }

    /// <summary>Startup clears what the previous update left, once it is no longer running.</summary>
    [Fact]
    public void StartupRemovesTheRetiredBuildAndAnyStagedDownload()
    {
        var (installer, folder) = Installer();

        var install = InstalledBuild(folder, "the current build");
        File.WriteAllText(install.Exe + UpdateInstaller.RetiredSuffix, "the previous build");
        File.WriteAllText(install.Whisper + UpdateInstaller.RetiredSuffix, "the previous build");

        Directory.CreateDirectory(installer.StagingFolder);
        var stagedZip = Path.Combine(installer.StagingFolder, "d47-0.0.1.zip");
        var stagedExe = Path.Combine(installer.StagingFolder, "d47-0.0.1.exe");
        var stagedPayload = Directory.CreateDirectory(Path.Combine(installer.StagingFolder, "d47-0.0.1")).FullName;
        File.WriteAllText(stagedZip, "a spent download");
        File.WriteAllText(stagedExe, "what an older updater staged");
        File.WriteAllText(Path.Combine(stagedPayload, "d47.exe"), "a spent payload");

        installer.CleanUpRetired(install.Exe);

        Assert.False(File.Exists(install.Exe + UpdateInstaller.RetiredSuffix));
        Assert.False(File.Exists(install.Whisper + UpdateInstaller.RetiredSuffix));
        Assert.False(File.Exists(stagedZip));
        Assert.False(File.Exists(stagedExe));
        Assert.False(Directory.Exists(stagedPayload));
        Assert.True(File.Exists(install.Exe));
        Assert.True(File.Exists(install.Whisper));
    }

    /// <summary>A release with no assets is offered as a link, never as an install.</summary>
    [Fact]
    public void AReleaseWithoutBothAssetsCannotBeInstalled()
    {
        const string page = "https://github.com/dseelinger/d47/releases/tag/v9.9.9";
        const string zip = "https://github.com/dseelinger/d47/releases/download/v9.9.9/d47.zip";

        Assert.False(new AvailableUpdate("9.9.9", page).CanInstall);
        Assert.False(new AvailableUpdate("9.9.9", page, zip).CanInstall);
        Assert.True(new AvailableUpdate("9.9.9", page, zip, zip + ".sha256").CanInstall);
    }

    private static (UpdateInstaller Installer, string Folder) Installer()
    {
        var root = TempFolders.Create("d47-update-tests");
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        return (new UpdateInstaller(paths, NullLogger<UpdateInstaller>.Instance), root);
    }

    /// <summary>An installed d47: the exe, and a native library beside it — the v0.5.14 shape.</summary>
    private static (string Exe, string Whisper) InstalledBuild(string folder, string content)
    {
        var install = Directory.CreateDirectory(Path.Combine(folder, "install")).FullName;
        var natives = Directory.CreateDirectory(Path.Combine(install, "runtimes", "win-x64")).FullName;

        var exe = Path.Combine(install, "d47.exe");
        var whisper = Path.Combine(natives, "whisper.dll");
        File.WriteAllText(exe, content);
        File.WriteAllText(whisper, content);

        return (exe, whisper);
    }

    /// <summary>An unpacked download of the same shape, as DownloadAsync would leave it.</summary>
    private static string Payload(string folder, string content)
    {
        var payload = Directory.CreateDirectory(
            Path.Combine(folder, "payload-" + Guid.NewGuid().ToString("N"))).FullName;
        var natives = Directory.CreateDirectory(Path.Combine(payload, "runtimes", "win-x64")).FullName;

        File.WriteAllText(Path.Combine(payload, "d47.exe"), content);
        File.WriteAllText(Path.Combine(natives, "whisper.dll"), content);

        return payload;
    }

    /// <summary>A zip of that shape, as the release workflow would publish it.</summary>
    private static string BuildArchive(string folder, string content)
    {
        var archive = Path.Combine(folder, "d47-" + Guid.NewGuid().ToString("N") + ".zip");
        ZipFile.CreateFromDirectory(Payload(folder, content), archive);
        return archive;
    }
}
