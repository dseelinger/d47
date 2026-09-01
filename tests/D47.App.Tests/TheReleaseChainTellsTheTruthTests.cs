using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The release chain is the part of this project the compiler never sees, and the part whose
/// mistakes cost a version number permanently — a published tag never moves. So the properties it
/// has to hold are asserted here, against the scripts as text, the way
/// <see cref="InstallerScriptTests"/> asserts the installer's.
/// <para>
/// Every one of these was a real defect, filed after an analysis of the chain on 2026-08-29:
/// <a href="https://github.com/dseelinger/d47/issues/169">#169</a>,
/// <a href="https://github.com/dseelinger/d47/issues/170">#170</a>,
/// <a href="https://github.com/dseelinger/d47/issues/171">#171</a> and
/// <a href="https://github.com/dseelinger/d47/issues/172">#172</a>. Text assertions are a weak
/// instrument and they are the right one here: these lines fail in production, once, on a run
/// nobody wants to repeat.
/// </para>
/// </summary>
public class TheReleaseChainTellsTheTruthTests
{
    private static string Tool(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", name));

    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", name));

    /// <summary>
    /// <b>The tag names the commit CI greened</b> (#169). <c>git tag</c> with no ref tags whatever
    /// HEAD is at that moment, and the CI wait matched its run server-side against <c>$head</c> —
    /// so a commit arriving from another terminal during the wait, or during an attended
    /// confirmation left sitting for hours, took a signed tag CI had never seen.
    /// </summary>
    [Fact]
    public void TheTagNamesTheCommitCiValidated()
    {
        Assert.Contains("Invoke-Git tag -s $next $head -F $annotation", Tool("release.ps1"), StringComparison.Ordinal);
    }

    /// <summary>
    /// And with the tag pinned, the post-tag rerun is provably redundant (#169) — it re-tested a
    /// commit <c>ci.yml</c> had already greened. Removing it took a full serial suite off every
    /// release's critical path and closed a hazard of its own: a flaky failure <em>after</em> the
    /// tag leaves a signed tag with no Release behind it, which is the failure this repository has
    /// recorded as the one that costs a version number.
    /// </summary>
    [Fact]
    public void TheReleaseWorkflowDoesNotRunTheSuiteAgain()
    {
        // Steps, not prose. The workflow explains at length why the step is absent, and a naive
        // search for the phrase finds that explanation.
        Assert.DoesNotContain(Steps(Workflow("release.yml")), line => line.Contains("dotnet test", StringComparison.Ordinal));

        // And the run it leans on instead still exists. The pair only holds while ci.yml is what
        // the wait is waiting for.
        Assert.Contains(Steps(Workflow("ci.yml")), line => line.Contains("dotnet test", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The local suite is opt-in, and one rule survived the polarity flip</b> (#170): no path
    /// may tag a commit nothing has tested. <c>-SkipCi</c> removes the CI wait, so it turns the
    /// local run back on rather than being refused — a run that says "do not wait for CI" answers
    /// for itself.
    /// </summary>
    [Fact]
    public void SkippingCiTurnsTheLocalSuiteBackOn()
    {
        var release = Tool("release.ps1");

        Assert.Contains("$runTests = $Tests -or $SkipCi", release, StringComparison.Ordinal);

        // The old switch is gone rather than left as a no-op that quietly does nothing.
        Assert.DoesNotContain("$SkipTests", release, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The unattended chain reads exit codes, so the chain has to set them</b> (#172). The
    /// pre-release timeout used to warn and <c>return</c> — which exits 0 — while the build it
    /// could not mark went on to become latest, and <c>UpdateChecker</c> reads
    /// <c>/releases/latest</c>. "Probably marked" is not marked.
    /// </summary>
    [Fact]
    public void TheTimeoutThatLeavesABuildHeadingForLatestFails()
    {
        var release = Tool("release.ps1");
        var timeout = release.IndexOf("did not appear within 20 minutes", StringComparison.Ordinal);

        Assert.True(timeout > 0, "the pre-release timeout message moved");

        // The statement that carries the outcome, not merely a warning near it.
        var around = release[Math.Max(0, timeout - 600)..Math.Min(release.Length, timeout + 600)];

        Assert.Contains("throw", around, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the caller notices. <c>prerelease.ps1</c>'s handover is the final statement of the
    /// unattended chain; without this a release.ps1 that threw reported success to whatever ran it
    /// (#172).
    /// </summary>
    [Fact]
    public void PrereleaseFailsWhenTheReleaseItHandedOverToDid()
    {
        var prerelease = Tool("prerelease.ps1");

        Assert.Contains("if ($LASTEXITCODE -ne 0) {", prerelease, StringComparison.Ordinal);
        Assert.Contains("exit $LASTEXITCODE", prerelease, StringComparison.Ordinal);

        // **And the handover splats by name.** Splatting an array passes every element
        // positionally, so release.ps1 read '-PreRelease' as a second positional argument and
        // refused the call — which is how the first real run after #170 failed. This assertion is
        // text, like the rest here, and text could not have caught it: only running the thing did.
        Assert.Contains("$handover = @{", prerelease, StringComparison.Ordinal);
        Assert.DoesNotContain("$handover = @(", prerelease, StringComparison.Ordinal);

        // Doing nothing is a different answer from cutting a release, and an exit code is where
        // the difference has to show.
        Assert.Contains("exit 1", prerelease, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An empty range says so, and says it first</b> (asked for 2026-08-30, after a run that had
    /// just shipped v0.93.0 was repeated).
    /// <para>
    /// Everything downstream worked perfectly on nothing: no commit closed a change request, so the
    /// decision was Patch, and the changelog check then reported that <c>## 0.93.1</c> was missing
    /// and exited nonzero. Every word of that is true and all of it is about the wrong thing — it
    /// blames the changelog for the absence of a release nobody should be cutting, and it reads
    /// exactly like the tool having broken.
    /// </para>
    /// <para>
    /// So the check sits <em>above</em> the version arithmetic, and it exits <b>zero</b>: nothing
    /// is wrong and nothing was done, which is a different answer from the missing-section refusal
    /// below it.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingToReleaseIsItsOwnAnswerAndComesFirst()
    {
        var prerelease = Tool("prerelease.ps1");

        Assert.Contains("so there is nothing to release", prerelease, StringComparison.Ordinal);

        // Above the changelog check, or the run reports the wrong reason for stopping — which is
        // the whole of what was reported.
        Assert.True(
            prerelease.IndexOf("so there is nothing to release", StringComparison.Ordinal)
            < prerelease.IndexOf("has no '## $next' section", StringComparison.Ordinal),
            "prerelease.ps1 reads the changelog before it notices there is nothing to release, so a "
            + "repeated run blames the changelog for a release that should not be cut at all.");

        // A return rather than an exit, so nothing reading the code hears a failure. The
        // missing-section path below it still exits 1, and the two must not be confusable.
        Assert.Contains("Nothing has been committed since", prerelease, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A dead gh refuses to decide rather than defaulting to Patch</b> (#172). Every label
    /// lookup failing does not mean nothing landed — it means nothing is known, and shipping a
    /// phase as a patch spends a version number that never moves.
    /// </summary>
    [Fact]
    public void AnUnreadableIssueListStopsTheRunRatherThanGuessing()
    {
        var prerelease = Tool("prerelease.ps1");

        Assert.Contains("$unreadable", prerelease, StringComparison.Ordinal);
        Assert.Contains("could be read, so there is nothing", prerelease, StringComparison.Ordinal);

        // The silent path that started this: a bare continue on a nonzero gh, with the catch three
        // lines below already warning about the same thing.
        Assert.DoesNotContain("if ($LASTEXITCODE -ne 0) { continue }", prerelease, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The deepest rollback no longer destroys itself</b> (#171). At the steady state the tool
    /// creates — ten held, one per deploy — the pre-restore snapshot is an eleventh, so the trim
    /// dropped the oldest; and when the Commander reached for the oldest, that was the file about
    /// to be read. Reproduced end to end against the unfixed script before this landed: the target
    /// was deleted, the restore threw, and <c>Remove-Item -Force</c> is not a recycle-bin delete.
    /// </summary>
    [Fact]
    public void TheRestoreProtectsTheSnapshotItIsAboutToRead()
    {
        var backup = Tool("data-backup.ps1");

        Assert.Contains("-Protect $wanted.FullName", backup, StringComparison.Ordinal);
        Assert.Contains("$_.FullName -ne $Protect", backup, StringComparison.Ordinal);

        // And the backup command cannot be turned into a delete-all by a typo.
        Assert.Contains("[ValidateRange(1, 100)]", backup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Protection is asked about before anything is mutated</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/93">#93</a>). The script pushes main
    /// directly and pushes a tag, and a rule on either refuses it — the first after the commit and
    /// the merge, the second after the CI wait as well. That is the same late-failure shape the
    /// version check was moved to the front for, and it is asserted the same way: by position.
    /// </summary>
    [Fact]
    public void TheRemoteIsAskedBeforeAnythingIsCommittedOrMerged()
    {
        var script = Tool("release.ps1");

        var asked = script.IndexOf("Write-Step 'Checking the remote will take a push and a tag'", StringComparison.Ordinal);
        var committed = script.IndexOf("Invoke-Git add -A", StringComparison.Ordinal);
        var merged = script.IndexOf("Invoke-Git merge --no-ff", StringComparison.Ordinal);

        Assert.True(asked > 0, "the preflight is gone");
        Assert.True(asked < committed, "the remote is asked after the commit, which is the fault");
        Assert.True(asked < merged, "the remote is asked after the merge, which is the fault");
    }

    /// <summary>
    /// <b>And it asks GitHub rather than the console</b> (#93). Every question this script asks a
    /// person goes through <c>Request-Value</c>, which <c>-Yes</c> turns into an error — because
    /// <c>Read-Host</c> with no console attached does not ask, it hangs. A preflight that asked
    /// would put a hang in front of every unattended run.
    /// </summary>
    [Fact]
    public void ThePreflightNeverAsksAPerson()
    {
        var script = Tool("release.ps1");

        var from = script.IndexOf("Write-Step 'Checking the remote will take a push and a tag'", StringComparison.Ordinal);
        var to = script.IndexOf("------------------ the annotation", StringComparison.Ordinal);

        Assert.True(from > 0 && to > from);

        var preflight = script[from..to];

        Assert.DoesNotContain("Request-Value", preflight, StringComparison.Ordinal);
        Assert.DoesNotContain("Read-Host", preflight, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The rules this repository actually has must not refuse a run</b> (#93). <c>main
    /// history</c> restricts deletion and non-fast-forward on main; <c>released tags</c> restricts
    /// deletion, non-fast-forward and <c>update</c> on <c>refs/tags/v*</c>. All five are about
    /// moving and deleting refs — the rule this project keeps by hand written where GitHub
    /// enforces it — and none of them stops creating a tag or pushing a merge commit.
    /// <para>
    /// <b>The first draft listed <c>update</c> as a tag blocker and refused every run</b>, caught
    /// by driving it against this repository rather than by reading it. Creating a ref that does
    /// not exist is <c>creation</c>; <c>update</c> governs moving one that does. The asymmetry is
    /// real — <c>update</c> on a *branch* does stop a push — so both halves are pinned here.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRulesThisRepositoryHasAreNotTreatedAsBlockers()
    {
        var script = Tool("release.ps1");

        Assert.Contains(
            "$BlocksPush = @('pull_request', 'update', 'required_status_checks', 'required_linear_history')",
            script,
            StringComparison.Ordinal);

        Assert.Contains("$BlocksTag = @('creation')", script, StringComparison.Ordinal);

        // Neither list may name the two that are configured on main today.
        var push = script[script.IndexOf("$BlocksPush = @(", StringComparison.Ordinal)..];
        var lists = push[..push.IndexOf("$BlocksTag = @(", StringComparison.Ordinal)];

        Assert.DoesNotContain("'deletion'", lists, StringComparison.Ordinal);
        Assert.DoesNotContain("'non_fast_forward'", lists, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A refused push puts main back</b> (#93). The preflight reads rulesets and cannot see
    /// classic branch protection, a permission changed since it asked, or a rule added while the
    /// suite ran — so the unforeseen case costs nothing to unpick rather than leaving a merge
    /// commit on local main behind a failure.
    /// </summary>
    [Fact]
    public void ARefusedPushLeavesNoMergeBehindOnMain()
    {
        var script = Tool("release.ps1");

        Assert.Contains("$before = (Invoke-Git rev-parse $Main)", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Git reset --hard $before", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Git checkout $branch", script, StringComparison.Ordinal);

        // And the failure is still a failure: rolling back and carrying on would tag a commit the
        // remote never took.
        var from = script.IndexOf("Write-Step \"Pushing $Main to $Remote\"", StringComparison.Ordinal);
        var to = script.IndexOf("$head = (Invoke-Git rev-parse HEAD)", StringComparison.Ordinal);

        Assert.True(from > 0 && to > from);
        Assert.Contains("throw", script[from..to], StringComparison.Ordinal);
    }

    /// <summary>A workflow's lines with its comments taken out, so prose is not read as a step.</summary>
    private static string[] Steps(string workflow) =>
        [.. workflow.Split('\n').Where(line => !line.TrimStart().StartsWith('#'))];

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
