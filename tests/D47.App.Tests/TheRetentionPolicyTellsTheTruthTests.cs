using D47.App.Logging;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Diagnostics;
using D47.Core.Diagnostics.Recording;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>docs/data-retention.md</c> states every number, and every number is somewhere else
/// (<a href="https://github.com/dseelinger/d47/issues/168">#168</a>).
/// <para>
/// <b>The whole complaint #168 was filed about is a document that drifts.</b> The rules lived in
/// three files and nowhere else, so nobody could read one and nothing checked them against each
/// other — and a policy page written once and left behind would be that failure with an extra
/// step, because it would also <em>look</em> authoritative. So the page is held to the code the
/// way <see cref="TheReleaseChainTellsTheTruthTests"/> holds the release scripts: change a limit
/// without changing the page and the build says so.
/// </para>
/// <para>
/// <b>It asserts presence, not prose.</b> These are text assertions over a document a person
/// writes, and the property worth having is that the number a Commander reads is the number the
/// code holds. What the page says <em>around</em> the number is nobody's test to write.
/// </para>
/// </summary>
public class TheRetentionPolicyTellsTheTruthTests
{
    private static string Policy =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "data-retention.md"));

    /// <summary>
    /// The two log lives and the per-day ceiling, read off <see cref="LoggingSetup"/> rather than
    /// typed here — a second copy of the number in the test is a third place to keep in step.
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

    /// <summary>
    /// <b>The asymmetry is the rule, not the two numbers.</b> The readable log is what a bug report
    /// quotes and what an incident excerpt cuts its log half out of; the JSON copy is most of the
    /// bytes and nobody reads it. Somebody levelling the two would leave both assertions above
    /// passing and undo the reason either number is what it is.
    /// </summary>
    [Fact]
    public void TheReadableLogOutlivesTheMachineOne()
    {
        Assert.True(
            LoggingSetup.ReadableLogLife > LoggingSetup.MachineLogLife,
            "The readable log is the half a person and an excerpt both read, so it is the half "
            + "whose reach is worth buying. Keeping the JSON copy as long or longer spends the "
            + "bytes on the copy nobody opens.");
    }

    /// <summary>
    /// The audio recorder's ring is the sharpest number on the page — a rolling recording of the
    /// audio in somebody's home — and it is the one #168 said must never be a number a person
    /// remembers to apply.
    /// </summary>
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

    /// <summary>
    /// The two rules that live outside the .NET build, which is exactly why they are worth a gate:
    /// nothing else in <c>dotnet test</c> ever opens either file, and a snapshot count changed in
    /// PowerShell or an expiry changed in the Worker's runbook would otherwise leave this page
    /// quietly wrong.
    /// </summary>
    [Fact]
    public void ThePageStatesTheRulesThatLiveOutsideTheDotNetBuild()
    {
        var policy = Policy;
        var root = RepositoryRoot();

        var backup = File.ReadAllText(Path.Combine(root, "tools", "data-backup.ps1"));
        Assert.Contains("[int] $Keep = 10,", backup, StringComparison.Ordinal);
        Assert.Contains("**the last 10 deploys**", policy, StringComparison.Ordinal);

        // The excerpt's expiry is a bucket lifecycle rule rather than code, so the command that
        // creates it in the runbook is the only place it exists. That is the point of naming it
        // here: a rule nothing in this repository can execute is the easiest one to drift.
        var runbook = File.ReadAllText(Path.Combine(root, "worker", "README.md"));
        Assert.Contains("--expire-days 30", runbook, StringComparison.Ordinal);
        Assert.Contains("**30 days**", policy, StringComparison.Ordinal);
    }

    /// <summary>
    /// A donor reads the notice at the moment of consent, so the two documents have to reach each
    /// other. <b>Both directions</b>: a retention question arriving from the notice, and a "who
    /// holds this" question arriving from the policy.
    /// </summary>
    [Fact]
    public void TheTwoDocumentsReachEachOther()
    {
        var notice = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "docs", "donation-privacy.md"));

        Assert.Contains("donation-privacy.html", Policy, StringComparison.Ordinal);
        Assert.Contains("data-retention.html", notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Both sinks still write with the retention on them.</b> Serilog's file sink refuses some
    /// combinations of <c>shared</c>, a size limit and a rolling interval at the moment it is
    /// asked to open a file rather than when it is configured, so a configuration that compiles
    /// and builds a logger can still write nothing at all — and a log that silently stopped
    /// existing is the worst possible outcome of a change to how long it is kept.
    /// </summary>
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

            // The line rather than the file, because a sink that failed to open still leaves the
            // name behind in some arrangements, and an empty log passes any check about existence.
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
