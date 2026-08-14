using D47.App.Updates;
using D47.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The real GitHub release, end to end: the API shape, the asset names, the checksum sidecar
/// format and the hash all have to agree or an update silently downgrades to "open the page".
/// Opt-in with <c>D47_UPDATE_LIVE=1</c> so `dotnet test` stays hermetic, and standing rather
/// than one-off — run it to tell "GitHub changed" apart from "d47 broke".
/// </summary>
public class UpdateLiveTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("D47_UPDATE_LIVE") == "1";

    [Fact]
    public async Task TheLatestReleaseCarriesAnInstallableVerifiedBuild()
    {
        Assert.SkipUnless(Enabled, "set D47_UPDATE_LIVE=1 to run tests that contact GitHub");

        var checker = new UpdateChecker(NullLogger<UpdateChecker>.Instance);

        // A version that will always be older than whatever is published.
        var update = await checker.CheckAsync("0.0.1", TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.True(update.CanInstall, "the published release carries both d47.zip and its checksum");

        var root = TempFolders.Create("d47-update-live");
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        var installer = new UpdateInstaller(paths, NullLogger<UpdateInstaller>.Instance);

        var (payload, failure) = await installer.DownloadAsync(
            update, progress: null, TestContext.Current.CancellationToken);

        Assert.Null(failure);
        Assert.NotNull(payload);

        // Verified against the checksum GitHub publishes, inside DownloadAsync. Getting a
        // payload back at all is that check having passed on the real bytes.
        var exe = Path.Combine(payload, "d47.exe");
        Assert.True(new FileInfo(exe).Length > 1_000_000, "a self-contained d47.exe is tens of MB");
        Assert.True(
            File.Exists(Path.Combine(payload, "runtimes", "win-x64", "whisper.dll")),
            "the release ships Whisper's natives beside the exe — their loader probes the disk");

        Directory.Delete(root, recursive: true);
    }
}
