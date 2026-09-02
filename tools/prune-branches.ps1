<#
.SYNOPSIS
    Deletes branches whose work is already on origin, locally and optionally on origin itself.

.DESCRIPTION
    A checkout accumulates branches. `release.ps1` merges each working branch into `main` and
    pushes, and nothing has ever deleted the branch afterwards - so the list grows by one per
    issue and every `git branch` reading needs a person to remember which names are spent.

    **Spent means "contains no commit that origin has not got".** Not "merged into local main",
    which is a different and more dangerous question: `main` is routinely ahead of `origin/main`
    between a merge and a push, so a branch merged into a local `main` can still be the only copy
    of that work anywhere. The test this uses is the honest one:

        git rev-list --count <branch> --not --remotes=origin

    Zero means every commit on it exists on origin under some ref, so deleting the branch loses
    nothing. Non-zero means the branch is the only place those commits live, and it is kept unless
    `-Force` says otherwise.

    **It fetches first, and fails if it cannot.** Every decision here is about what origin has, and
    a stale remote-tracking ref answers that question confidently and wrongly - a branch deleted on
    origin still looks like a home for commits until `--prune` says otherwise.

    **What it never touches:** `main`, the branch this worktree is on, and any branch checked out
    in another worktree (this repository runs parallel sessions in worktrees, and their branches
    hold in-progress work that no test here can see). `origin/main` and `origin/HEAD` are excluded
    from `-Global` for the same reason.

    **Uncommitted work is not a case this can hit.** A branch that is not checked out anywhere has
    no working tree and so no uncommitted changes; a branch that is checked out is skipped by name
    before the question arises.

.PARAMETER Force
    Delete branches that still hold commits origin has not got. This discards work. It is the only
    switch here that can, which is why it is not implied by anything else.

.PARAMETER Global
    Also delete the matching branches on origin. A remote branch is spent when it holds no commit
    that is not already on `origin/main`. Deleting one is not a local act and cannot be undone from
    here, so this always lists what it will do and asks first unless `-Yes`.

.PARAMETER DryRun
    Say what would be deleted and stop. Changes nothing, and needs no confirmation.

.PARAMETER Yes
    Skip the confirmation. For unattended runs; the same switch and the same meaning as release.ps1.

.EXAMPLE
    prune-branches
    prune-branches --dry-run
    prune-branches --global
    prune-branches --force --global --yes

.NOTES
    Long-form switches are accepted in either dialect - `-Force` and `--force` both work - because
    the bash shim hands its arguments through unchanged and a shim holding a translation table
    would be a second place that has to agree about the switches.

    `git branch -D` rather than `-d` throughout: `-d` asks whether the branch is merged into the
    current HEAD, which is not the question this script decided, and it would refuse exactly the
    branches whose work is safely on origin but not yet in this checkout's `main`.
#>

param(
    [switch] $Force,

    [switch] $Global,

    [switch] $DryRun,

    [switch] $Yes,

    # PowerShell parameter names cannot begin with two dashes and an alias cannot either, so the
    # unix spellings arrive here and are mapped below rather than being rejected by the parser.
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot

foreach ($argument in @($Rest)) {
    # An empty argument is what a shim's unquoted "$@" leaves behind when it was handed nothing;
    # it names no switch and refusing it would fail a run that asked for nothing unusual.
    if ([string]::IsNullOrWhiteSpace($argument)) { continue }

    switch -Regex ($argument) {
        '^(--force|-f)$'   { $Force = $true;  continue }
        '^(--global|-g)$'  { $Global = $true; continue }
        '^(--dry-run|-n)$' { $DryRun = $true; continue }
        '^(--yes|-y)$'     { $Yes = $true;    continue }
        default {
            throw "prune-branches: unknown argument '$argument'. Expected --force, --global, --dry-run or --yes."
        }
    }
}

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

<#
    git writes ordinary progress to stderr, and Windows PowerShell turns any stderr line from a
    native command into a terminating error while ErrorActionPreference is Stop. Same trap that
    broke release.ps1's -PreRelease switch on v0.78.0 and that repo-guard.ps1 works around for gh.
#>
function Invoke-Git {
    param([string[]] $Arguments, [string] $What, [switch] $Tolerate)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try {
        $output = & git -C $Root @Arguments 2>&1 | ForEach-Object { "$_" }
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($LASTEXITCODE -ne 0 -and -not $Tolerate) {
        throw "Could not ${What}: $($output -join ' ')"
    }

    return @($output)
}

# Every question below is "what has origin got", so ask origin before answering any of them.
Write-Step 'Fetching origin'
Invoke-Git -What 'fetch origin' -Arguments @('fetch', '--prune', 'origin') | Out-Null

# The branch this worktree is on, plus every branch held by another worktree. `git branch -D`
# refuses a checked-out branch anyway, but refusing it here means the plan that gets printed is the
# plan that runs, rather than a list with a failure in the middle of it.
$checkedOut = @(
    Invoke-Git -What 'list worktrees' -Arguments @('worktree', 'list', '--porcelain') |
        Where-Object { $_ -like 'branch refs/heads/*' } |
        ForEach-Object { $_.Substring('branch refs/heads/'.Length) }
)

$protected = @(@('main') + $checkedOut | Select-Object -Unique)

$locals = @(
    Invoke-Git -What 'list local branches' -Arguments @(
        'for-each-ref', '--format=%(refname:short)', 'refs/heads') |
        Where-Object { $_ -and $protected -notcontains $_ }
)

# Commits on the branch that exist under no origin ref at all. Zero is the whole safety case.
function Get-UnpushedCount {
    param([string] $Ref, [string] $Not)

    $counted = Invoke-Git -What "count commits on $Ref" -Arguments @(
        'rev-list', '--count', $Ref, '--not', $Not)

    return [int]($counted | Select-Object -Last 1)
}

$spent = @()
$held = @()

foreach ($branch in $locals) {
    $unpushed = Get-UnpushedCount -Ref "refs/heads/$branch" -Not '--remotes=origin'

    if ($unpushed -eq 0) { $spent += $branch }
    else { $held += [pscustomobject]@{ Name = $branch; Unpushed = $unpushed } }
}

$localTargets = @($spent) + @(if ($Force) { $held | ForEach-Object { $_.Name } })

# A remote branch is spent against origin/main rather than against "all of origin", which for a
# remote ref would include itself and make every branch trivially spent.
$remoteTargets = @()

if ($Global) {
    $remotes = @(
        Invoke-Git -What 'list remote branches' -Arguments @(
            'for-each-ref', '--format=%(refname:short)', 'refs/remotes/origin') |
            # `refs/remotes/origin/HEAD` shortens to a bare `origin`, which is neither a branch nor
            # something that can be deleted; the `origin/*` test drops it along with anything else
            # that is not a branch under the remote.
            Where-Object { $_ -like 'origin/*' -and $_ -ne 'origin/main' -and $_ -ne 'origin/HEAD' }
    )

    foreach ($remote in $remotes) {
        $unmerged = Get-UnpushedCount -Ref $remote -Not 'origin/main'
        $name = $remote.Substring('origin/'.Length)

        if ($unmerged -eq 0) { $remoteTargets += $name }
        elseif ($Force) { $remoteTargets += $name }
    }
}

Write-Step 'Plan'

if ($held.Count -gt 0 -and -not $Force) {
    foreach ($branch in $held) {
        Write-Note "keep   $($branch.Name) - $($branch.Unpushed) commit(s) origin has not got"
    }
}

foreach ($branch in $protected) { Write-Note "keep   $branch - protected or checked out" }
foreach ($branch in $localTargets) { Write-Host "  delete local  $branch" }
foreach ($branch in $remoteTargets) { Write-Host "  delete remote origin/$branch" -ForegroundColor Yellow }

if ($localTargets.Count -eq 0 -and $remoteTargets.Count -eq 0) {
    Write-Host ''
    Write-Host 'Nothing to prune.'
    exit 0
}

if ($DryRun) {
    Write-Host ''
    Write-Host 'Dry run - nothing was deleted.'
    exit 0
}

<#
    Confirmation, unless -Yes. A local branch is recoverable from the reflog for as long as it
    lasts; a branch deleted on origin is not recoverable from here at all, so the prompt says which
    kind is in the list rather than asking the same bland question for both.
#>
if (-not $Yes) {
    $stakes = if ($remoteTargets.Count -gt 0) {
        "$($localTargets.Count) local and $($remoteTargets.Count) remote branch(es). Deleting on origin cannot be undone from here"
    }
    else {
        "$($localTargets.Count) local branch(es)"
    }

    # ${stakes} braced: a bare $stakes? reads the question mark as part of the variable name, and
    # PowerShell then reports an undefined variable rather than a syntax error.
    $answer = Read-Host "Delete ${stakes}? [y/N]"

    if ($answer -notmatch '^(y|yes)$') {
        Write-Host 'Cancelled.'
        exit 0
    }
}

foreach ($branch in $localTargets) {
    Invoke-Git -What "delete $branch" -Arguments @('branch', '-D', $branch) | Out-Null
    Write-Host "  deleted local  $branch"
}

foreach ($branch in $remoteTargets) {
    Invoke-Git -What "delete origin/$branch" -Arguments @('push', 'origin', '--delete', $branch) | Out-Null
    Write-Host "  deleted remote origin/$branch"
}

Write-Host ''
Write-Host "Pruned $($localTargets.Count) local and $($remoteTargets.Count) remote branch(es)."
