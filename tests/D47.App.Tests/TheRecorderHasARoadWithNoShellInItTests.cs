using D47.App.Recording;
using D47.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The audio recorder is reachable without a shell.</summary>
public class TheRecorderHasARoadWithNoShellInItTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-flight-road", Guid.NewGuid().ToString("N"));

    /// <summary>The switch is process-wide state, so it is put back.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        AudioRecorder.ReadCommandLine([]);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>The road the issue asked for: a switch on the command line, and nothing else.</summary>
    [Fact]
    public void TheSwitchTurnsRecordingOn()
    {
        AudioRecorder.ReadCommandLine(["--record-audio"]);

        Assert.True(AudioRecorder.Enabled);
    }

    [Fact]
    public void AnOrdinaryLaunchLeavesItOff()
    {
        AudioRecorder.ReadCommandLine(["--selftest"]);

        Assert.False(AudioRecorder.Enabled);
    }

    /// <summary>Matched whole and case-sensitively, so a near miss is off rather than on.</summary>
    [Theory]
    [InlineData("--record-audioer")]
    [InlineData("--record")]
    [InlineData("--Record-Audio")]
    [InlineData("record-audio")]
    [InlineData("--flight-record")]
    [InlineData("--Flight-Recorder")]
    [InlineData("flight-recorder")]
    public void ANearMissIsNotTheSwitch(string argument)
    {
        AudioRecorder.ReadCommandLine([argument]);

        Assert.False(AudioRecorder.Enabled);
    }

 /// <summary>The name it had before still works.</summary>
    [Fact]
    public void TheRetiredSwitchStillTurnsItOnAndSaysSo()
    {
        AudioRecorder.ReadCommandLine([AudioRecorder.RetiredFlag]);

        Assert.True(AudioRecorder.Enabled);
        Assert.True(AudioRecorder.ByRetiredName);

        // And the current name is not reported as the old one, which is what makes the log line above worth
        // having rather than noise on every run.
        AudioRecorder.ReadCommandLine([AudioRecorder.Flag]);

        Assert.True(AudioRecorder.Enabled);
        Assert.False(AudioRecorder.ByRetiredName);
    }

    /// <summary>
    /// The switch is the spelling the helper on the PATH passes, so the two cannot drift apart —
    /// <c>tools/rec-on.ps1</c> hands this exact string to the installed executable.
    /// </summary>
    [Fact]
    public void TheHelperPassesTheSwitchThisReads()
    {
        var script = Path.Combine(Repository(), "tools", "rec-on.ps1");

        Assert.True(File.Exists(script), $"tools/rec-on.ps1 is missing (looked in {script}).");

        Assert.Contains(
            $"'{AudioRecorder.Flag}'",
            File.ReadAllText(script),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// And nothing is composed when nobody asked, which is where the surface silence comes from: no
    /// recorder means no settings row, no review pane and no folder written.
    /// </summary>
    [Fact]
    public void NoRecorderIsComposedWhenNobodyAsked()
    {
        AudioRecorder.ReadCommandLine([]);

        var recorder = AudioRecorder.Create(
            new AppPaths(_folder), () => DateTimeOffset.UnixEpoch, NullLogger.Instance);

        Assert.Null(recorder);
        Assert.False(Directory.Exists(Path.Combine(_folder, "data", "flight")));
    }

    /// <summary>Walks up to the repository root, which is where <c>tools/</c> lives.</summary>
    private static string Repository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tools")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }
}
