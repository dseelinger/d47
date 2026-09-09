using D47.App.Logging;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Diagnostics;
using D47.Core.Diagnostics.Recording;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>docs/data-retention.md</c> states every number, and every number is somewhere else.
/// </summary>
public class TheRetentionPolicyTellsTheTruthTests
{
    private static string Policy =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "data-retention.md"));

    /// <summary>
    /// The two log lives and the per-day ceiling, read off <see cref="LoggingSetup"/> rather than typed
    /// here — a second copy of the number in the test is a third place to keep in step.
    /// </summary>
    [Fact]
    public void ThePageStatesTheLogRetentionTheSinksActuallyHold()
    {
        var policy = Policy;

        Assert.Contains($"**{LoggingSetup.ReadableLogLife.Days} days**", policy, StringComparison.Ordinal);
        Assert.Contains($"**{LoggingSetup.MachineLogLife.Days} days**", policy, StringComparison.Ordinal);
        Assert.Contains(
            $"**{LoggingSetup.MostBytesPerDay / (1024 * 1024)} MB** of any one day",
            policy,
            StringComparison.Ordinal);
    }

    /// <summary>The asymmetry is the rule, not the two numbers.</summary>
    [Fact]
    public void TheReadableLogOutlivesTheMachineOne()
    {
        Assert.True(
            LoggingSetup.ReadableLogLife > LoggingSetup.MachineLogLife,
            "The readable log is the half a person and an excerpt both read, so it is the half "
            + "whose reach is worth buying. Keeping the JSON copy as long or longer spends the "
            + "bytes on the copy nobody opens.");
    }

    /// <summary>The audio recorder's ring is the sharpest number on the page — a rolling recording of the audio in somebody's home — and it must never be a number a person remembers to apply.</summary>
    [Fact]
    public void ThePageStatesTheAudioRingTheWriterEnforces()
    {
        Assert.Contains(
            $"**{RecordingLog.CapBytes / (1024 * 1024)} MB**",
            Policy,
            StringComparison.Ordinal);
    }

    /// <summary>How long d47 remembers something about the Commander, out of the box.</summary>
    [Fact]
    public void ThePageStatesTheMemoryExpiryTheSettingsDefaultTo()
    {
        Assert.Contains(
            $"**{new D47Settings().Memory.ExpiryDays} days** by default",
            Policy,
            StringComparison.Ordinal);
    }

    /// <summary>The one rule that lives outside the .NET build: nothing else in <c>dotnet test</c> opens the
    /// Worker's runbook, so an expiry changed there would leave this page silently wrong.</summary>
    [Fact]
    public void ThePageStatesTheRuleThatLivesOutsideTheDotNetBuild()
    {
        var policy = Policy;

        // The excerpt's expiry is a bucket lifecycle rule rather than code, so the command that creates it in
        // the runbook is the only place it exists.
        var runbook = File.ReadAllText(Path.Combine(RepositoryRoot(), "worker", "README.md"));
        Assert.Contains("--expire-days 30", runbook, StringComparison.Ordinal);
        Assert.Contains("**30 days**", policy, StringComparison.Ordinal);
    }

    /// <summary>
    /// A donor reads the notice at the moment of consent, so the two documents have to reach each
    /// other.
    /// </summary>
    [Fact]
    public void TheTwoDocumentsReachEachOther()
    {
        var notice = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "docs", "donation-privacy.md"));

        Assert.Contains("donation-privacy.html", Policy, StringComparison.Ordinal);
        Assert.Contains("data-retention.html", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void BothSinksStillWriteUnderTheirRetention()
    {
        var root = Path.Combine(Path.GetTempPath(), "d47-retention", Guid.NewGuid().ToString("n"));

        try
        {
            var paths = new AppPaths(root);
            paths.EnsureCreated();

            const string line = "a line that has to reach both files";

            var logger = LoggingSetup.Create(paths, new SerilogVerbosityControl());
            logger.Information(line);
            (logger as IDisposable)?.Dispose();

            // The line rather than the file, because a sink that failed to open still leaves the name behind
            // in some arrangements, and an empty log passes any check about existence.
            Assert.Contains(Text(paths.Logs, "d47-*.log"), text => text.Contains(line, StringComparison.Ordinal));
            Assert.Contains(Text(paths.Logs, "d47-*.jsonl"), text => text.Contains(line, StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static IEnumerable<string> Text(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern).Select(File.ReadAllText);

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
}
