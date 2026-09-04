<#
.SYNOPSIS
    Commits what is in the working tree, merges it to main, and cuts a release.
    Works from a linked worktree: the merge and push happen in the primary checkout, because
    that is the only place main can be checked out at all.

.DESCRIPTION
    The release process is written down in CLAUDE.md and has been run by hand every time. This is
    that process, once, so it cannot be run differently twice — which is the same argument the
    build already makes: "if a workflow needs a checklist to run, fix the workflow."

    Three of the steps here are not conveniences. Each one is a rule this project has already paid
    for in a version number it could not reuse:

      * `dotnet test -c Release` runs before the tag, not after. The release workflow runs it too,
        and a failure there lands *after* the tag is published — leaving a tag with no release
        behind it, which costs a version number to correct.

      * The tag waits for CI to go green on the pushed commit. Same reason, one step earlier.
        Pushing a tag onto a commit whose build is still running is how v0.16.0 was spent.

      * A tag that already exists stops the script dead. A published tag is a receipt for one
        exact d47.exe and the checksum beside it, and the update checker compares a running
        build against it. Retagging makes one version number mean two binaries.

    The version is computed from the newest v* tag rather than read from any file: the tag is
    where the version lives, and the release workflow takes it from there too.

    Branch and tag protection, and why this script still pushes both
    ----------------------------------------------------------------
    <https://github.com/dseelinger/d47/issues/93>. This pushes `main` directly and pushes a tag,
    and a rule on either would refuse it — the first after the commit and the merge, the second
    after the CI wait as well. So there are now three things checked before anything is mutated,
    where there were two: the tag not existing, an annotation being available, and the remote
    being willing to take the push and the tag.

    **The pipeline is unchanged, and that is the recorded decision rather than an omission.**
    As configured today, `main history` restricts deletion and non-fast-forward on the default
    branch, and `released tags` restricts deletion, non-fast-forward and update on `refs/tags/v*`.
    None of the five stops this script: they forbid moving and deleting refs, which is the rule
    this repository already keeps by hand — *a published tag never moves* — written down where
    GitHub enforces it. Creating a tag and pushing a merge commit are untouched.

    If that ever changes, there are two ways out and the choice is the Commander's. **An actor
    exemption** for whoever cuts releases keeps "one command, once" true and is the cheaper one.
    **Opening a pull request and waiting** is honest under protection but changes the shape of the
    command and adds a wait that can hang unattended, which is the thing `-Yes` exists to prevent.
    The preflight below is what turns that from a discovery mid-run into a sentence before it.

    **The release workflow is not affected**, checked separately because it runs under a different
    token: `release.yml` triggers on the pushed tag, holds `contents: write`, and creates a GitHub
    Release. It never pushes, updates or deletes a ref, so a ruleset on `refs/tags/v*` has nothing
    of its to refuse.

    Where the merge to main happens
    --------------------------------
    <https://github.com/dseelinger/d47/issues/292>. `main` is always checked out in the primary
    checkout, and this repository now runs parallel sessions from linked worktrees under
    `.claude/worktrees/`. `git checkout main` from one of those fails — main is already checked out
    elsewhere — and it used to fail *after* the commit above, which is exactly the late failure
    every other check in this script exists to prevent.

    So the merge step first asks `git rev-parse --git-common-dir`, which differs from `--git-dir`
    only in a linked worktree, and the common dir's parent is the primary checkout. From the
    primary checkout, or an ordinary (non-worktree) clone, nothing changes: `checkout main` and
    `merge --no-ff` run right here, as they always did. From a linked worktree, this script never
    checks anything out — refs and objects are shared with the primary checkout, so `main` is
    merged and pushed there instead, by running git with `-C <primary>`. The worktree's own
    checkout is untouched throughout, so the session that ran this is still on its branch
    afterwards.

    A preflight before the commit refuses the run if the primary checkout is not on `main`, or has
    uncommitted changes — that may be another session's work in progress, and a merge on top of it
    is not this script's to make. Said before anything is committed, in the same place every other
    preflight here lives.

    The rollback on a refused push (#93) resets `main` in the primary checkout the same way it
    resets it here otherwise; there is nothing to check back out, because the worktree never moved.

.PARAMETER Release
    Patch or Minor, case-insensitive.

    Minor is for a completed phase or a batch of wanted changes — anything where a
    Commander should be able to tell "there is a whole capability here now" from "some fixes
    landed". Patch is for the fixes. See CLAUDE.md.

.PARAMETER Message
    The commit message for whatever is uncommitted. Required only when there is something to
    commit; the script asks for it if it is needed and not given.

.PARAMETER IncludeUntracked
    Let the commit step sweep in an unusual number of untracked files. The commit is `git add -A`
    and stays that way — a release commit has to carry new source files nobody has added yet — but
    it counts and measures them first, and stops at 200 files or 25 MB. That is the shape of the
    2026-08-24 accident, where an untracked 217 MB `data\` folder with live secrets went into a
    release commit. This switch is how a sweep that really is all meant says so.

.PARAMETER Notes
    The tag's annotation — what changed, in a sentence or several. Defaults to the CHANGELOG
    heading for the version being cut, which is where that sentence already lives.

.PARAMETER Yes
    Runs without asking anything. It skips the confirmation before the tag is pushed — which is
    there because the tag is the one step with no way back — and it also turns every other
    question into an error naming the switch that would have answered it. For unattended runs,
    where a question is not a question: with no console attached, Read-Host either hangs or
    returns nothing and fails several minutes later, having already committed and merged.

.PARAMETER ShowVersion
    Prints the version this run would cut, says whether CHANGELOG.md already has its section,
    and stops. Changes nothing. The annotation is taken from that section, so it has to be
    written before the run that uses it — and until this switch existed the only way to learn
    the number was to work it out by hand, which is the script's own job done twice.

.PARAMETER Tests
    Pushes without running `dotnet test -c Release` first. The suite is not skipped, only moved:
    ci.yml runs it on the pushed commit and the wait below will not tag a red one, so this trades
    ci.yml runs the same suite on the same merged commit, and the CI wait below refuses to tag
    a red one regardless - so the tag is CI-gated with or without this, and leaving it out can
    never cost a version number. Worth asking for when you want the answer before the push rather
    than three minutes into it.

    -SkipCi turns this on whatever you passed, because that pair is the one combination that
    would tag a commit nothing had tested.

.PARAMETER SkipCi
    Pushes the tag without waiting for CI. Only for a run where the CI result is already known,
    and it prints what it is skipping.

.PARAMETER PreRelease
    Cuts the version, but marks the GitHub Release as a pre-release so nobody is offered it.
    The update checker reads only releases/latest, and GitHub's latest endpoint skips
    pre-releases - so this build is installable by you and invisible to everyone else. Drive it,
    then promote it:

        gh release edit vX.Y.Z --prerelease=false --latest

    Use the real version number rather than an -rc suffix. A pre-release that fails its soak is
    simply never promoted, and you fix forward to the next patch: the tag stays where it is,
    which is the rule, and no public release ever carried the fault.

    The marking happens after the release workflow publishes, because the workflow is what
    creates the Release. This script waits for that to finish and then flips the flag, so there
    is a window of a minute or two where the new release is the latest one. That window is
    accepted rather than engineered away: closing it means teaching the workflow to read the
    tag annotation, and the workflow is the one piece of this pipeline that cannot be tested
    without spending a version number.

.EXAMPLE
    ./tools/release.ps1 -Release patch -Message "Remediation 14 item 1: the row layout"

.EXAMPLE
    ./tools/release.ps1 minor -Notes "Phase 35: the thing the phase does"

.EXAMPLE
    ./tools/release.ps1 patch -ShowVersion
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('Patch', 'Minor')]
    [string] $Release,

    [string] $Message,

    [string] $Notes,

    [switch] $Yes,

    [switch] $ShowVersion,

    [switch] $Tests,

    [switch] $SkipCi,

    [switch] $PreRelease,

    [switch] $IncludeUntracked
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Remote = 'origin'
$Main = 'main'

# Where the `git add -A` sweep stops and asks (#186). Generous for a release commit — a phase
# landing is tens of new source files and a few hundred kilobytes — and far under the shape of the
# accident this exists to catch: an untracked 217 MB data folder, thousands of files, live secrets
# inside it, swept into a release commit on 2026-08-24.
$MaxUntrackedFiles = 200
$MaxUntrackedBytes = 25MB

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

# Git writes progress and hints to stderr on success, so a bare call in a script with
# ErrorActionPreference=Stop throws on commands that worked. The exit code is the truth.
# Windows PowerShell turns any stderr line from a native command into a terminating error while
# ErrorActionPreference is Stop — and git, dotnet and gh all write ordinary progress and warnings
# there. The exit code is the truth, so a native call runs with the preference relaxed and is
# checked explicitly afterwards.
function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

# No param block on purpose: a declared parameter makes the binder read `-A` in `git add -A` as
# an attempt at the parameter's own name, and the call fails before git is ever reached. $args
# takes every argument verbatim, which is the only thing wanted here.
function Invoke-Git {
    $call = $args
    $output = Invoke-Native { & git @call 2>&1 | ForEach-Object { "$_" } }
    $code = $LASTEXITCODE

    if ($code -ne 0) {
        $output | ForEach-Object { Write-Host $_ }
        throw "git $($call -join ' ') failed with exit code $code"
    }

    return $output
}

function Test-Clean {
    return [string]::IsNullOrWhiteSpace((Invoke-Git status --porcelain) -join "`n")
}

# Every question the script asks goes through here, so -Yes governs all of them rather than only
# the last one. An unattended run has no console: Read-Host there does not ask, it hangs — and
# both of the questions this replaces came *after* the commit and the merge, so the run that
# eventually gave up had already changed the repository. Naming the switch that would have
# answered it turns a hang into a line of output.
function Request-Value {
    param([string] $Prompt, [string] $Switch)

    if ($Yes) {
        throw "$Prompt is required, and -Yes means nothing can be asked. Pass $Switch."
    }

    return Read-Host $Prompt
}

# The heading for a version, or $null. Shared because -ShowVersion reports whether it is there
# and the annotation below is read out of it: one pattern, so the switch cannot say a section
# exists that the annotation step then fails to find.
function Find-ChangelogHeading {
    param([string] $Version)

    $changelog = Join-Path $root 'CHANGELOG.md'

    if (-not (Test-Path $changelog)) {
        return $null
    }

    return Select-String -Path $changelog -Pattern "^##\s+$([regex]::Escape($Version))\s" |
        Select-Object -First 1
}

# ------------------------------------------------------------------ where we are

$root = (Invoke-Git rev-parse --show-toplevel) | Select-Object -First 1
Set-Location $root

Write-Step "Repository: $root"

$branch = (Invoke-Git rev-parse --abbrev-ref HEAD) | Select-Object -First 1

if ($branch -eq 'HEAD') {
    throw 'HEAD is detached. Check out a branch before releasing from it.'
}

Write-Note "On branch $branch"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'The GitHub CLI (gh) is not on PATH. It is needed to wait for CI before tagging.'
}

# **The rule that survived the polarity flip** (<https://github.com/dseelinger/d47/issues/170>).
# The local suite is opt-in now, because ci.yml runs the same one on the same commit and the wait
# below will not tag a red result — so skipping it costs minutes, never a version number. What has
# not changed is that no path may tag a commit nothing has tested, and -SkipCi is the switch that
# removes the other half of the check. So it turns the local run back on rather than being refused:
# a run that says "do not wait for CI" is a run that has to answer for itself.
#
# It used to read the other way round: -SkipTests leans on the CI wait, and -SkipCi removes it.
# together they are a tag on a commit that nothing has run.
$runTests = $Tests -or $SkipCi

# ------------------------------------------------------------------ the remote

# **Before the version, because the version is worked out from tags** (#186). The tag list was read
# locally and nothing ever fetched, so a checkout that had not seen the newest tag computed a number
# that was already taken — and found out at the push, after the commit, the merge and the CI wait.
# That is exactly the late failure the version-first ordering exists to prevent, arriving by the one
# road it did not cover. Parallel worktree sessions make two cutters colliding a matter of time.
#
# **Best effort on purpose.** An unreachable remote is not a reason to refuse to say what the next
# number would be — -ShowVersion has to keep working offline — and the push is still the real gate.
# So a failure here is loud and does not stop the run.
Write-Step "Fetching tags from $Remote"

try {
    Invoke-Git fetch $Remote --tags | Out-Null
    Write-Note 'Local tags are up to date with the remote.'
}
catch {
    # Not --quiet, so git's own lines are printed by Invoke-Git before it throws: the usual cause is
    # one local tag that disagrees with the published one ("would clobber existing tag"), which
    # rejects that ref and still fetches the rest. Which of those happened is git's to say, not this
    # script's to guess — so the wording claims nothing beyond "not cleanly".
    Write-Warning "The fetch from $Remote did not complete cleanly: $($_.Exception.Message)"
    Write-Warning "Some tags may be stale. $Remote is still asked about the new tag directly below."
}

# ------------------------------------------------------------------ the version
#
# Worked out before anything is committed or merged, because both of the things that can stop a
# run — the tag already existing, and no annotation to be had — are known from the tag list and
# CHANGELOG.md alone. Finding out after the merge left a merge commit on local main behind a
# failure that was knowable at the start.

Write-Step 'Working out the next version'

$tags = @(Invoke-Git tag --list 'v*' --sort=-v:refname)
$latest = $tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } | Select-Object -First 1

if (-not $latest) {
    $next = 'v0.1.0'
    Write-Note 'No v* tag yet, so this is the first one.'
}
else {
    $parts = $latest.TrimStart('v').Split('.')
    $major = [int] $parts[0]
    $minor = [int] $parts[1]
    $patch = [int] $parts[2]

    if ($Release -ieq 'Minor') {
        $minor += 1
        $patch = 0
    }
    else {
        $patch += 1
    }

    $next = "v$major.$minor.$patch"
    Write-Note "$latest is the newest, so a $($Release.ToLowerInvariant()) is $next"
}

# A published tag never moves. Stopping here is the whole point of checking.
if ($tags -contains $next) {
    throw "$next already exists. A published tag never moves — cut the next number instead."
}

# And asked of the remote directly, rather than trusting the fetch above to have happened (#186).
# A fetch that failed leaves the local list authoritative about nothing, and this is the one
# question whose wrong answer is only discovered after the commit, the merge and the CI wait.
#
# It does not close the window between here and the tag push — another cutter can still take the
# number during the suite or the wait — but it closes the one that was open before a single thing
# had been changed, which is where a stale checkout's collision actually lives.
$asked = $true
$onRemote = @()

try {
    $onRemote = @(Invoke-Git ls-remote --tags $Remote "refs/tags/$next" | Where-Object { $_ })
}
catch {
    # Only a failure to *ask* is a warning; the refusal itself is thrown below, outside the try,
    # so it cannot be swallowed by the handler meant for the network.
    $asked = $false
    Write-Warning "Could not ask $Remote whether $next is taken: $($_.Exception.Message)"
}

if ($asked -and $onRemote.Count -gt 0) {
    throw "$next is already tagged on $Remote. A published tag never moves — cut the next number instead."
}

# ------------------------------------------------------------------ the number, and nothing else

if ($ShowVersion) {
    $version = $next.TrimStart('v')

    Write-Host ''
    Write-Host $next -ForegroundColor Cyan

    if (Find-ChangelogHeading $version) {
        Write-Note "CHANGELOG.md has its '## $version' section, so -Notes will come from there."
    }
    else {
        Write-Note "CHANGELOG.md has no '## $version' section yet. Write it before the real run:"
        Write-Note 'the tag annotation and the GitHub Release body are both read out of it.'
    }

    Write-Step 'Nothing was changed.'
    return
}

# ------------------------------------------------------------------ what the remote will allow

# **The third thing that can stop a run** (<https://github.com/dseelinger/d47/issues/93>), asked
# here for the reason the version is worked out here: both of the pushes below are refusable by a
# rule that is knowable now, and finding out at the push leaves a merge commit on local main
# behind a failure nothing had to discover the hard way.
#
# The two that can be refused are not equally expensive. A rejected `push main` costs an unpick;
# a rejected `push <tag>` arrives after the commit, the merge and the CI wait, and the script's
# own rule is that an existing tag stops it dead — so the obvious retry hits that instead.
#
# **What is checked is rulesets, and that is a real limit.** `gh ruleset` reads rulesets and knows
# nothing about classic branch protection, so a repository protected the older way passes this and
# is refused at the push exactly as before. The rollback below is what makes that survivable; this
# is what makes it unlikely.
#
# **Asking is best-effort, refusing is not.** No gh, no network or no authentication is a warning
# and the run goes on — the push is still the real gate — which is the same shape as the tag
# question above. A rule that *is* read and *does* block stops the run here, named.
$BlocksPush = @('pull_request', 'update', 'required_status_checks', 'required_linear_history')

# **`deletion` and `non_fast_forward` are deliberately not in that list**, and this repository is
# why: both are configured on main today and neither stops an ordinary push. A check that refused
# on them would refuse every run, which is worse than the fault it is meant to catch.

# **And a tag is not a branch about `update`.** Creating a ref that does not exist is governed by
# `creation`; `update` governs moving one that does. `released tags` here carries `update` — that
# is the rule enforcing "a published tag never moves", and it applies to every `refs/tags/v*` this
# script has ever cut. Listing it as a blocker refused every run, which is what the first draft
# did and what driving it against the real repository caught.
$BlocksTag = @('creation')

# The rules a ruleset applies, and the refs it applies them to, out of `gh ruleset view`. Text
# rather than JSON because that command offers no `--json`, so the shape is the parse: one
# `include: [...]` list of ref patterns, then a `Rules` heading and a rule per line.
function Read-Ruleset {
    param([string] $Id)

    $view = Invoke-Native { & gh ruleset view $Id 2>&1 | ForEach-Object { "$_" } }

    if ($LASTEXITCODE -ne 0) {
        throw "gh ruleset view $Id failed: $($view -join ' ')"
    }

    $patterns = @()
    $rules = @()
    $inRules = $false

    foreach ($row in $view) {
        if ($row -match 'include:\s*\[(.*?)\]') {
            $patterns += @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }

        if ($row -match '^\s*Rules\s*$') {
            $inRules = $true
            continue
        }

        if ($inRules -and $row -match '^\s*-\s+([a-z_]+)\s*$') {
            $rules += $Matches[1]
        }
    }

    return [pscustomobject]@{ Patterns = $patterns; Rules = $rules }
}

# Which of a ruleset's ref patterns cover this tag. `~ALL` is GitHub's own wildcard; the rest are
# fnmatch ref patterns, and -like reads the `*` in `refs/tags/v*` the same way.
function Test-CoversTag {
    param([string[]] $Patterns, [string] $Tag)

    foreach ($pattern in $Patterns) {
        if ($pattern -eq '~ALL' -or "refs/tags/$Tag" -like $pattern) {
            return $true
        }
    }

    return $false
}

function Get-BranchRules {
    param([string] $Branch)

    # `gh ruleset check` answers for the current actor, which is the question — a rule somebody
    # else cannot bypass and this account can is not a rule that stops this run.
    $text = Invoke-Native { & gh ruleset check $Branch 2>&1 | ForEach-Object { "$_" } }

    if ($LASTEXITCODE -ne 0) {
        throw "gh ruleset check $Branch failed: $($text -join ' ')"
    }

    return @($text | ForEach-Object {
        if ($_ -match '^\s*-\s+([a-z_]+)\s*$') { $Matches[1] }
    })
}

function Get-TagRules {
    param([string] $Tag)

    # `gh ruleset check` takes a branch and prefixes refs/heads/, so a tag ref answers "0 rules"
    # however protected it is. The rulesets have to be read and matched against the tag by hand.
    $listed = Invoke-Native { & gh ruleset list 2>&1 | ForEach-Object { "$_" } }

    if ($LASTEXITCODE -ne 0) {
        throw "gh ruleset list failed: $($listed -join ' ')"
    }

    $rules = @()

    foreach ($line in $listed) {
        if ($line -notmatch '^(\d+)\s') { continue }

        $ruleset = Read-Ruleset $Matches[1]

        if (Test-CoversTag $ruleset.Patterns $Tag) {
            $rules += $ruleset.Rules
        }
    }

    return @($rules | Sort-Object -Unique)
}

Write-Step 'Checking the remote will take a push and a tag'

try {
    $onMain = @(Get-BranchRules $Main | Where-Object { $BlocksPush -contains $_ })
    $onTag = @(Get-TagRules $next | Where-Object { $BlocksTag -contains $_ })

    if ($onMain.Count -gt 0) {
        throw ("A rule on $Main would refuse the push: $($onMain -join ', '). " +
               'Nothing has been changed. Either exempt this account from that ruleset, or take ' +
               'the work to main through a pull request and run this from main afterwards.')
    }

    if ($onTag.Count -gt 0) {
        throw ("A rule on refs/tags/$next would refuse the tag: $($onTag -join ', '). " +
               'Nothing has been changed, which is the point of asking now: that push comes ' +
               'after the commit, the merge and the CI wait.')
    }

    Write-Note "No ruleset stops this account pushing $Main or creating $next."
}
catch [System.Management.Automation.RuntimeException] {
    # Only a failure to *ask* is survivable, and the refusals above are thrown as plain strings
    # rather than by a cmdlet, so they are re-thrown rather than swallowed by the handler meant
    # for gh being absent.
    if ($_.Exception.Message -like 'A rule on *') { throw }

    Write-Warning "Could not ask which rules apply: $($_.Exception.Message)"
    Write-Note 'Going on: the push is still the real gate, and a refusal there is put back below.'
}

# ------------------------------------------------------------------ the annotation

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $version = $next.TrimStart('v')
    $heading = Find-ChangelogHeading $version

    if ($heading) {
        # The changelog line is the release's permanent record, so it is also the best thing to
        # put on the tag: one sentence, already written, already reviewed.
        $Notes = ($heading.Line -replace '^##\s+', '').Trim()
        Write-Note "Annotation from CHANGELOG.md: $Notes"
    }
    else {
        Write-Warning "CHANGELOG.md has no '## $version' entry. The changelog line is a release's permanent record."
        $Notes = Request-Value "Tag annotation for $next" '-Notes'

        if ([string]::IsNullOrWhiteSpace($Notes)) {
            throw 'A tag annotation is required.'
        }
    }
}

# ------------------------------------------------------------------ linked worktree

# git shares refs and objects between a primary checkout and every worktree linked off it, but
# only one of them can have any given branch checked out at a time — including main. Run from a
# linked worktree, the merge step's `checkout main` fails because main is already checked out in
# the primary checkout, and it used to fail after the commit above: exactly the late failure every
# preflight in this script exists to prevent (#292).
#
# `--git-dir` and `--git-common-dir` agree in the primary checkout and in an ordinary clone, and
# differ only in a linked worktree, where `--git-dir` is the worktree's own private
# `.git/worktrees/<name>` and `--git-common-dir` is the shared `.git` it was linked from. That
# common dir's parent is the primary checkout.
$gitDir = Resolve-Path ((Invoke-Git rev-parse --git-dir) | Select-Object -First 1)
$commonDir = Resolve-Path ((Invoke-Git rev-parse --git-common-dir) | Select-Object -First 1)
$isLinkedWorktree = $gitDir.Path -ne $commonDir.Path
$primaryRoot = if ($isLinkedWorktree) { Split-Path $commonDir.Path -Parent } else { $root }

if ($isLinkedWorktree) {
    Write-Step "Running from a linked worktree; $Main lives in $primaryRoot"

    # Refused here, before the commit below, for the same reason every other preflight in this
    # script runs early: another session may have uncommitted work in the primary checkout, and a
    # merge on top of it is not this script's to make.
    $primaryBranch = (Invoke-Git -C $primaryRoot rev-parse --abbrev-ref HEAD) | Select-Object -First 1

    if ($primaryBranch -ne $Main) {
        throw "The primary checkout at $primaryRoot is on $primaryBranch, not $Main. Check out $Main there before releasing from a worktree."
    }

    $primaryStatus = (Invoke-Git -C $primaryRoot status --porcelain) -join "`n"

    if (-not [string]::IsNullOrWhiteSpace($primaryStatus)) {
        throw "The primary checkout at $primaryRoot has uncommitted changes. That may be another session's work in progress; this script will not merge on top of it."
    }

    Write-Note "$primaryRoot is on $Main and clean."
}

# ------------------------------------------------------------------ commit

if (Test-Clean) {
    Write-Step 'Nothing to commit'
}
else {
    Write-Step 'Committing the working tree'

    Invoke-Git status --short | ForEach-Object { Write-Note $_ }

    # **What `git add -A` is about to sweep in, said out loud** (#186). On 2026-08-24 it swept an
    # untracked 217 MB data folder with live secrets into a release commit, and this repository's
    # own convention is commit-by-explicit-path. The sweep stays — a release commit has to carry new
    # source files nobody has added yet — but it is measured first, and a sweep that looks like that
    # one stops the run rather than being narrated past. -IncludeUntracked is how it is meant.
    $untracked = @(Invoke-Git status --porcelain --untracked-files=all |
        Where-Object { $_ -match '^\?\? ' } |
        ForEach-Object { $_.Substring(3).Trim('"') })

    if ($untracked.Count -gt 0) {
        $bytes = 0

        foreach ($entry in $untracked) {
            $full = Join-Path $root $entry

            if (Test-Path -LiteralPath $full -PathType Leaf) {
                $bytes += (Get-Item -LiteralPath $full -Force).Length
            }
        }

        $megabytes = [math]::Round($bytes / 1MB, 1)
        $size = if ($bytes -lt 1MB) { "$([math]::Ceiling($bytes / 1KB)) KB" } else { "$megabytes MB" }

        Write-Note "$($untracked.Count) untracked file$(if ($untracked.Count -ne 1) { 's' }), $size, will be swept in by git add -A"

        # --untracked-files=all above lists files rather than folders, so a 217 MB data folder is
        # thousands of entries as well as hundreds of megabytes: either count on its own is the
        # signal, and neither is reachable by an ordinary release.
        $over = @()

        if ($untracked.Count -gt $MaxUntrackedFiles) {
            $over += "$($untracked.Count) files, over the $MaxUntrackedFiles this stops at"
        }

        if ($bytes -gt $MaxUntrackedBytes) {
            $over += "$megabytes MB, over the $([int] ($MaxUntrackedBytes / 1MB)) MB this stops at"
        }

        if ($over.Count -gt 0 -and -not $IncludeUntracked) {
            $untracked | Select-Object -First 10 | ForEach-Object { Write-Note "  $_" }

            if ($untracked.Count -gt 10) {
                Write-Note "  ... and $($untracked.Count - 10) more"
            }

            throw @"
The release commit would sweep in $($over -join ', and '). One swept an untracked
217 MB data folder with live secrets in on 2026-08-24. Add what belongs, ignore what does not,
or pass -IncludeUntracked if this really is all meant.
"@
        }

        if ($over.Count -gt 0) {
            Write-Warning "Swept in anyway, because -IncludeUntracked: $($over -join ', and ')."
        }
    }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        $Message = Request-Value 'Commit message' '-Message'

        if ([string]::IsNullOrWhiteSpace($Message)) {
            throw 'A commit message is required for the changes in the working tree.'
        }
    }

    Invoke-Git add -A
    Invoke-Git commit -m $Message | Out-Null

    Write-Note (Invoke-Git log --oneline -1)
}

# ------------------------------------------------------------------ merge

# Where main stood before this run touched it, and the whole of the undo at the push below. Null
# when the work was already on main, because then the commit *is* the work and there is nothing
# to put back (#93).
$before = $null

if ($branch -eq $Main) {
    Write-Step "Already on $Main, nothing to merge"
}
elseif ($isLinkedWorktree) {
    Write-Step "Merging $branch into $Main in $primaryRoot"

    $before = (Invoke-Git -C $primaryRoot rev-parse $Main) | Select-Object -First 1

    # No checkout here: refs and objects are shared with the primary checkout, so it can merge a
    # branch this worktree has checked out without either checkout moving.
    #
    # --no-ff, because a batch that arrived as several commits should read as one thing on main.
    # The merge commit is where the whole of it is named.
    Invoke-Git -C $primaryRoot merge --no-ff $branch -m "Merge $branch" | Out-Null

    Write-Note (Invoke-Git -C $primaryRoot log --oneline -1)
}
else {
    Write-Step "Merging $branch into $Main"

    $before = (Invoke-Git rev-parse $Main) | Select-Object -First 1

    Invoke-Git checkout $Main | Out-Null

    # --no-ff, because a batch that arrived as several commits should read as one thing on main.
    # The merge commit is where the whole of it is named.
    Invoke-Git merge --no-ff $branch -m "Merge $branch" | Out-Null

    Write-Note (Invoke-Git log --oneline -1)
}

# ------------------------------------------------------------------ the gate

if ($runTests) {
    Write-Step 'dotnet test -c Release'

    if ($SkipCi) {
        Write-Note 'Not optional on this run: -SkipCi removes the wait that would otherwise catch it.'
    }

    Invoke-Native { & dotnet test -c Release --nologo }

    if ($LASTEXITCODE -ne 0) {
        throw 'Tests failed. Nothing has been pushed.'
    }
}
else {
    Write-Step 'Leaving the suite to CI'
    Write-Note 'ci.yml runs the same one on the pushed commit, and the wait below will not tag a red result.'
    Write-Note 'Nothing is unchecked; a failure is found on main a few minutes later instead. -Tests runs it here.'
}

# ------------------------------------------------------------------ push

Write-Step "Pushing $Main to $Remote"

# **And put local main back if the remote refuses it** (#93). The preflight above asks about
# rulesets and cannot see classic branch protection, a permission changed since it asked, or a
# rule added while the suite ran — so the unforeseen case is made cheap rather than merely
# unlikely: main returns to where this run found it and the checkout returns to the branch, which
# still holds every commit. Nothing is lost, and there is nothing to unpick by hand.
try {
    if ($isLinkedWorktree) {
        Invoke-Git -C $primaryRoot push $Remote $Main | Out-Null
    }
    else {
        Invoke-Git push $Remote $Main | Out-Null
    }
}
catch {
    if ($before) {
        Write-Warning "$Remote refused the push. Putting $Main back where this run found it."

        if ($isLinkedWorktree) {
            Invoke-Git -C $primaryRoot reset --hard $before | Out-Null

            Write-Note "$Main is at $($before.Substring(0, 12)) again in $primaryRoot."
            Write-Note "This worktree never left $branch. Every commit is still on it. Nothing was tagged."
        }
        else {
            Invoke-Git reset --hard $before | Out-Null
            Invoke-Git checkout $branch | Out-Null

            Write-Note "$Main is at $($before.Substring(0, 12)) again, and you are back on $branch."
            Write-Note 'Every commit is still on that branch. Nothing was tagged.'
        }
    }
    else {
        Write-Warning "$Remote refused the push, and this run committed straight onto $Main."
        Write-Note 'That commit is the work, so it is left alone. Nothing was tagged.'
    }

    throw
}

if ($isLinkedWorktree) {
    $head = (Invoke-Git -C $primaryRoot rev-parse HEAD) | Select-Object -First 1
}
else {
    $head = (Invoke-Git rev-parse HEAD) | Select-Object -First 1
}

Write-Note "main is at $($head.Substring(0, 12))"

# ------------------------------------------------------------------ wait for CI

if ($SkipCi) {
    Write-Step 'Skipping the CI wait (-SkipCi)'
    Write-Note 'A tag pushed onto a red commit leaves a version number that cannot be reused.'
}
else {
    Write-Step 'Waiting for CI on that commit'

    $run = $null

    # The run does not exist the instant the push returns. Two minutes of looking is generous for
    # a webhook and short enough that a genuine failure to start is still noticed.
    #
    # `--commit` does the matching on GitHub's side, and that is not a style preference. Matching
    # in PowerShell went wrong twice: Windows PowerShell hands a JSON array through the pipeline
    # as one object, so `Where-Object { $_.headSha -eq $head }` compares the whole *collection* of
    # head shas against one value and lets every run through — the first cut printed ten run ids
    # and watched whichever gh took first, which happened to be the right one. Moving the match
    # into a jq expression then broke on quoting, because a jq pipe inside a double-quoted
    # PowerShell string is a pipe PowerShell reads first. A flag has neither problem.
    foreach ($attempt in 1..24) {
        $ids = Invoke-Native {
            & gh run list --branch $Main --workflow ci.yml --commit $head --limit 5 `
                --json databaseId -q '.[].databaseId'
        }

        $ids = @($ids | Where-Object { $_ -match '^\d+$' })

        if ($ids.Count -gt 0) {
            $run = $ids[0]
            break
        }

        Start-Sleep -Seconds 5
    }

    if (-not $run) {
        throw "No CI run appeared for $($head.Substring(0, 12)). Nothing has been tagged."
    }

    # One id, and it is checked rather than assumed: watching the wrong run reports a green that
    # belongs to somebody else's commit, which is the one failure this whole wait exists to stop.
    if ($run -isnot [string]) {
        throw "Expected one CI run id for $($head.Substring(0, 12)), got: $($run -join ', ')"
    }

    Write-Note "Run $run"

    Invoke-Native { & gh run watch $run --exit-status } | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw 'CI is not green. Nothing has been tagged.'
    }

    Write-Note 'CI is green.'
}

# ------------------------------------------------------------------ tag

if (-not $Yes) {
    Write-Host ''
    Write-Host "About to tag $next and push it." -ForegroundColor Yellow
    Write-Host "  $Notes" -ForegroundColor Yellow
    Write-Host 'A published tag never moves.' -ForegroundColor Yellow

    $answer = Read-Host 'Cut it? (y/N)'

    if ($answer -notmatch '^(y|yes)$') {
        Write-Step 'Stopped. main is pushed; nothing is tagged.'
        return
    }
}

Write-Step "Tagging $next"

# **Named explicitly, and that is the safety property rather than a tidiness one**
# (<https://github.com/dseelinger/d47/issues/169>). `git tag` with no ref tags whatever HEAD is
# at this moment, and the CI wait above matched its run server-side against $head — so a commit
# arriving from another terminal during the wait, or during an attended confirmation that sat for
# hours, got a signed tag CI had never seen. release.yml's post-tag rerun was the only thing
# standing between that commit and a published Release, and it is gone now because this line makes
# it redundant. Order mattered: this first, the deletion second.
#
# Signed and annotated, as every published tag of this project is.
# The annotation goes through a file rather than through -m, because Windows PowerShell
# re-parses quotes inside a native command's arguments: a CHANGELOG heading containing a
# double quote — `"Copy that"` — was split, and git read the second word as a ref and failed
# with `Failed to resolve 'that' as a valid ref`. Nothing was tagged, which is the script
# working, but it cost a run. A file has no quoting rules for PowerShell to apply.
$annotation = [System.IO.Path]::GetTempFileName()

try {
    [System.IO.File]::WriteAllText($annotation, $Notes, [System.Text.UTF8Encoding]::new($false))
    Invoke-Git tag -s $next $head -F $annotation | Out-Null
}
finally {
    Remove-Item $annotation -ErrorAction SilentlyContinue
}
Invoke-Git push $Remote $next | Out-Null

Write-Step "$next is pushed. The release workflow builds, checksums and publishes it."
Write-Note "gh run watch (gh run list --workflow release.yml --limit 1 --json databaseId -q '.[0].databaseId')"

# ------------------------------------------------------------------ pre-release

# The Release is created by the workflow, not here, so the flag can only be set once that has
# published. Waiting is the point: flipping it early would race the workflow, and the failure
# mode of that race is exactly the thing this switch exists to prevent.
if ($PreRelease) {
    Write-Step "Marking $next as a pre-release"
    Write-Note 'Waiting for the release workflow to publish it first.'

    $deadline = (Get-Date).AddMinutes(20)
    $published = $false

    while ((Get-Date) -lt $deadline) {
        # gh exits non-zero until the Release exists, which is the signal being waited on — and
        # it says "release not found" on stderr while doing it.
        #
        # **Through Invoke-Native, which is not optional here.** Called bare, this is the trap that
        # helper was written for and documents at its own definition: while ErrorActionPreference
        # is Stop, Windows PowerShell turns any stderr line from a native command into a
        # terminating error. `2>$null` does not save it — the redirection is applied after the
        # error record has already been raised. So the first poll of a workflow that had not
        # finished publishing killed the whole step, and the switch this block implements failed
        # on its first real use, on v0.78.0, in the one case it was written to handle.
        $state = Invoke-Native { gh release view $next --json isDraft --jq '.isDraft' 2>&1 }

        if ($LASTEXITCODE -eq 0 -and $state -eq 'false') {
            $published = $true
            break
        }

        Start-Sleep -Seconds 15
    }

    if (-not $published) {
        # **Thrown rather than warned** (<https://github.com/dseelinger/d47/issues/172>). This
        # path used to return, which exits 0 — and release.yml publishes with no prerelease input,
        # so the build this could not mark lands as plain latest the moment the slow workflow
        # finishes, and UpdateChecker offers an undriven build to every install. prerelease.ps1
        # ends on this call and reads its exit code; "probably marked" is not marked.
        throw @"
The Release for $next did not appear within 20 minutes, so it is NOT marked as a pre-release.
It is probably still building — and when it finishes it will be the latest release, which is
what every install is offered. Mark it the moment it is up:
    gh release edit $next --prerelease
"@
    }

    gh release edit $next --prerelease | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not mark $next as a pre-release. Do it by hand: gh release edit $next --prerelease"
        return
    }

    # Read it back rather than trusting the exit code: the whole value of this switch is that
    # nobody is offered the build, and "probably marked" is not that.
    # Both through Invoke-Native, for the reason given at the poll above: a bare native call with
    # stderr in it is a terminating error while the preference is Stop, and `2>$null` is applied
    # too late to prevent one.
    $isPre = Invoke-Native { gh release view $next --json isPrerelease --jq '.isPrerelease' 2>&1 }

    # `gh release list` rather than `gh api repos/:owner/:repo/releases/latest`, which says the
    # same thing: the newest release that is neither a draft nor a pre-release is the definition
    # of that endpoint's answer. The raw API is denied to agents by .claude/settings.json, because
    # it is the road around tools/issues.ps1 — an issue body is one `gh api` call away otherwise —
    # and a script that needs a denied command is a reason to lift the deny.
    $latest = Invoke-Native {
        gh release list --limit 1 --exclude-drafts --exclude-pre-releases `
            --json tagName --jq '.[0].tagName' 2>&1
    }

    if ($isPre -ne 'true') {
        Write-Warning "$next does not read back as a pre-release. Check it: gh release view $next"
        return
    }

    Write-Step "$next is a pre-release. Nobody is offered it."
    Write-Note "The update checker still points at $latest."
    Write-Note 'Fly it, then promote:'
    Write-Note "    gh release edit $next --prerelease=false --latest"
}
