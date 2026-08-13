using D47.App.Updates;
using D47.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Installing an update downloads a program and then runs it, which is the most dangerous thing
/// d47 does. These cover the two things standing between a bad release response and an
/// arbitrary executable: where the bytes are allowed to come from, and whether they hash to what
/// the release published.
/// </summary>
public class UpdateInstallerTests
{
    [Theory]
    [InlineData("https://github.com/dseelinger/d47/releases/download/v0.2.1/d47.exe")]
    [InlineData("https://github.com/dseelinger/d47/releases/download/v9.9.9/d47.exe.sha256")]
    public void AnAssetOnThisRepositoryIsTrusted(string url)
    {
        Assert.True(UpdateChecker.IsTrustedDownloadUrl(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // Another host, and one that only reads like the right one.
    [InlineData("https://evil.example/dseelinger/d47/releases/download/v1/d47.exe")]
    [InlineData("https://github.com.evil.example/dseelinger/d47/releases/download/v1/d47.exe")]
    // The right host, someone else's repository, or one whose name merely starts the same.
    [InlineData("https://github.com/someone/else/releases/download/v1/d47.exe")]
    [InlineData("https://github.com/dseelinger/d47-evil/releases/download/v1/d47.exe")]
    // On this repository but not a release asset — a branch's copy of a file, say.
    [InlineData("https://github.com/dseelinger/d47/raw/main/d47.exe")]
    // Not the web at all. This is the one that matters: it would be started, not fetched.
    [InlineData(@"\attacker\share\payload.exe")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    public void AnythingElseIsRefused(string? url)
    {
        Assert.False(UpdateChecker.IsTrustedDownloadUrl(url));
    }

    /// <summary>The shape `sha256sum d47.exe` produces in the release workflow.</summary>
    [Fact]
    public void TheChecksumIsReadFromASha256sumSidecar()
    {
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            UpdateInstaller.ParseChecksum(
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  d47.exe\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // An error page where a checksum should be, which is what a moved URL actually returns.
    [InlineData("<!DOCTYPE html><html><body>Not Found</body></html>")]
    // Right length, not hex.
    [InlineData("zzzzc44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  d47.exe")]
    // Truncated hash.
    [InlineData("e3b0c44298fc1c14  d47.exe")]
    public void AnythingThatIsNotAHashIsNotAChecksum(string? body)
    {
        Assert.Null(UpdateInstaller.ParseChecksum(body));
    }

    /// <summary>
    /// The swap retires the running file rather than overwriting it, because Windows will not
    /// overwrite a running image but will rename one.
    /// </summary>
    [Fact]
    public void TheSwapRetiresTheRunningBuildAndInstallsTheNewOne()
    {
        var (installer, folder) = Installer();

        var running = Path.Combine(folder, "d47.exe");
        var downloaded = Path.Combine(folder, "d47-0.9.9.exe");

        File.WriteAllText(running, "the old build");
        File.WriteAllText(downloaded, "the new build");

        Assert.True(installer.TrySwap(running, downloaded));

        Assert.Equal("the new build", File.ReadAllText(running));
        Assert.Equal("the old build", File.ReadAllText(running + UpdateInstaller.RetiredSuffix));
        Assert.False(File.Exists(downloaded));
    }

    /// <summary>A second update does not trip over what the first one retired.</summary>
    [Fact]
    public void ASecondUpdateReplacesTheAlreadyRetiredBuild()
    {
        var (installer, folder) = Installer();

        var running = Path.Combine(folder, "d47.exe");

        File.WriteAllText(running, "the first build");
        File.WriteAllText(running + UpdateInstaller.RetiredSuffix, "an even older build");

        var downloaded = Path.Combine(folder, "d47-1.0.0.exe");
        File.WriteAllText(downloaded, "the second build");

        Assert.True(installer.TrySwap(running, downloaded));

        Assert.Equal("the second build", File.ReadAllText(running));
        Assert.Equal("the first build", File.ReadAllText(running + UpdateInstaller.RetiredSuffix));
    }

    /// <summary>
    /// If the new build cannot be moved into place, the old one goes back. Ending with no d47 at
    /// all is the one outcome worse than not updating.
    /// </summary>
    [Fact]
    public void AFailedInstallPutsTheRunningBuildBack()
    {
        var (installer, folder) = Installer();

        var running = Path.Combine(folder, "d47.exe");
        File.WriteAllText(running, "the old build");

        // A source that is not there at all: the retire succeeds, the move cannot.
        Assert.False(installer.TrySwap(running, Path.Combine(folder, "never-downloaded.exe")));

        Assert.True(File.Exists(running));
        Assert.Equal("the old build", File.ReadAllText(running));
    }

    /// <summary>Startup clears what the previous update left, once it is no longer running.</summary>
    [Fact]
    public void StartupRemovesTheRetiredBuildAndAnyStagedDownload()
    {
        var (installer, folder) = Installer();

        var running = Path.Combine(folder, "d47.exe");
        File.WriteAllText(running, "the current build");
        File.WriteAllText(running + UpdateInstaller.RetiredSuffix, "the previous build");

        Directory.CreateDirectory(installer.StagingFolder);
        var staged = Path.Combine(installer.StagingFolder, "d47-0.0.1.exe");
        File.WriteAllText(staged, "a spent download");

        installer.CleanUpRetired(running);

        Assert.False(File.Exists(running + UpdateInstaller.RetiredSuffix));
        Assert.False(File.Exists(staged));
        Assert.True(File.Exists(running));
    }

    /// <summary>A release with no assets is offered as a link, never as an install.</summary>
    [Fact]
    public void AReleaseWithoutBothAssetsCannotBeInstalled()
    {
        const string page = "https://github.com/dseelinger/d47/releases/tag/v9.9.9";
        const string exe = "https://github.com/dseelinger/d47/releases/download/v9.9.9/d47.exe";

        Assert.False(new AvailableUpdate("9.9.9", page).CanInstall);
        Assert.False(new AvailableUpdate("9.9.9", page, exe).CanInstall);
        Assert.True(new AvailableUpdate("9.9.9", page, exe, exe + ".sha256").CanInstall);
    }

    private static (UpdateInstaller Installer, string Folder) Installer()
    {
        var root = Directory.CreateTempSubdirectory("d47-update-tests").FullName;
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        return (new UpdateInstaller(paths, NullLogger<UpdateInstaller>.Instance), root);
    }
}
