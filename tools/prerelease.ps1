<#
.SYNOPSIS
    Cuts a pre-release, working out on its own whether it is a minor or a patch.

.DESCRIPTION
    A front end to `release.ps1`, which does all the real work. What this adds is the one decision a
    person makes wrong: **whether the version is a minor or a patch.**

    CLAUDE.md states the rule and this applies it rather than restating it:

      A completed phase is always a minor release, because the version is how a Commander
      tells "some fixes landed" from "there is a whole capability here now". A batch of
      wanted changes is a minor for the same reason. Fixes between phases are patches.

    **It is a rule that has already been got wrong, which is why it is worth automating.** It is
    exactly the sort of thing that is obvious on the day the phase ships and invisible three days
    later, when the tag is being cut for something else.

    So it is read out of the commits rather than remembered. One thing makes it a minor: **a
    commit since the last tag saying it closes an issue labelled `phase`, `change-request` or
    `enhancement`.** Anything else is a patch.

    **It reads the commits and not GitHub's closed list**, because an issue closes when the commit
    reaching it is pushed and this decision is made before anything is pushed. A closed query can
    never see the issues that the release it is deciding about is the one closing. It said
    "patch" for a batch of three on 2026-08-27 and was wrong in exactly that way.

    **Until 2026-08-27 a phase was read out of `list.md`**, by diffing its ticked boxes against
    the same file at the last tag. That file is gone, along with everything else in this
    repository that held project-management state, so a phase now lands the same way a wanted
    change always has — through the issue its commit closes. The two roads were always the same
    road; only one of them depended on a file staying accurate.

.PARAMETER Minor
    Force a minor. For the case the rules cannot see: a change request that never had an issue, or a
    judgement that this is a bigger release than its diff looks.

.PARAMETER Patch
    Force a patch.

.PARAMETER DryRun
    Say what it decided and why, and stop. Changes nothing and cuts nothing.

.EXAMPLE
    prerelease
    prerelease -DryRun
    prerelease -Minor

.NOTES
    **It always cuts a pre-release, never a latest**, which is the rule CLAUDE.md states outright: a
    release is never promoted automatically, because deciding a build is fit for everyone is the
    Commander's and is a separate act. `promote.ps1` is that act, and it is a separate command
    because it should be a separate decision.

    The changelog section is checked **before** anything is committed, tagged or pushed. That is the
    same reasoning `release.ps1` gives for working the version out first: the things that can stop a
    run should stop it before it has done anything that needs unpicking.
#>

param(
    [switch] $Minor,

    [switch] $Patch,

    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

# A native command's stderr is a terminating error under ErrorActionPreference Stop, and git and gh
# both write ordinary progress there. Same trap as release.ps1's and issues.ps1's.
function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

if ($Minor -and $Patch) {
    throw 'Pick one of -Minor and -Patch, or neither and let it work it out.'
}

Push-Location $Root

try {
    $tags = @(Invoke-Native { git tag --list 'v*' --sort=-v:refname } | ForEach-Object { $_ })

    if ($tags.Count -eq 0) {
        throw 'No v* tag to compare against. Cut the first release with release.ps1 by hand.'
    }

    $lastTag = $tags[0]

    Write-Step "Since $lastTag"

    <#
        A phase and a batch of wanted changes are both minors, and since 2026-08-27 they arrive
        by one road: the issue a commit says it closes.

        **Read out of the commits rather than out of GitHub's closed list, and that is not a
        preference.** An issue closes when the commit reaching it is pushed, and this decision is
        made *before* anything is pushed — the changelog has to be written against the version
        first. So a closed-issue query can never see the issues that the release it is deciding
        about is the one closing. It said "patch" for a batch of three on 2026-08-27 and was wrong
        in exactly that way.

        What a commit says it closes is knowable now, so that is what is asked.
    #>
    $log = (Invoke-Native { git log "$lastTag..HEAD" --format=%B }) -join "`n"

    $mentioned = [regex]::Matches($log, '(?i)\b(?:fixes|closes|resolves)\s+#(\d+)') |
        ForEach-Object { [int]$_.Groups[1].Value } |
        Sort-Object -Unique

    # Both spellings of the wanted-change idea, because the repository has two labels for it:
    # `enhancement`, which the Commander has always used, and `change-request`, added on
    # 2026-08-27 with planning. Counting only the newer one misses every wanted change filed
    # before that day. `phase` is the third, and it is kept in its own bucket so the run can say
    # which kind of thing it saw — the decision is the same either way, the sentence is not.
    $wantedLabels = @('change-request', 'enhancement')
    $phaseLabel = 'phase'
    $landed = @()
    $closedRequests = @()

    foreach ($number in @($mentioned)) {
        try {
            $raw = Invoke-Native {
                gh issue view $number --repo dseelinger/d47 --json number,labels 2>&1
            }

            if ($LASTEXITCODE -ne 0) { continue }

            $issue = $raw | ConvertFrom-Json
            $labels = @($issue.labels | ForEach-Object { $_.name })

            if ($labels -contains $phaseLabel) {
                $landed += [pscustomobject]@{ number = $number }
            }
            elseif (@($labels | Where-Object { $wantedLabels -contains $_ }).Count -gt 0) {
                $closedRequests += [pscustomobject]@{ number = $number }
            }
        }
        catch {
            Write-Warning "Could not read #${number}'s labels, so it did not count toward the decision."
        }
    }

    foreach ($phase in $landed) {
        Write-Note "a commit since $lastTag closes #$($phase.number), a phase"
    }

    foreach ($request in $closedRequests) {
        Write-Note "a commit since $lastTag closes #$($request.number), a wanted change"
    }

    if ($landed.Count -eq 0 -and $closedRequests.Count -eq 0) {
        Write-Note 'No commit since that tag closes a phase or a change request.'
    }

    $decided = if ($landed.Count -gt 0 -or $closedRequests.Count -gt 0) { 'Minor' } else { 'Patch' }

    $kind = if ($Minor) { 'Minor' } elseif ($Patch) { 'Patch' } else { $decided }

    if (($Minor -or $Patch) -and $kind -ne $decided) {
        Write-Warning "The rules said $decided and you said $kind. Going with $kind."
    }

    Write-Step "$kind"

    # The next number, worked out the same way release.ps1 works it out, so the changelog can be
    # checked before anything is committed rather than after the merge.
    if ($lastTag -notmatch '^v(\d+)\.(\d+)\.(\d+)$') {
        throw "$lastTag is not a version this can read."
    }

    $major = [int]$Matches[1]
    $minorPart = [int]$Matches[2]
    $patchPart = [int]$Matches[3]

    $next = if ($kind -eq 'Minor') {
        "$major.$($minorPart + 1).0"
    }
    else {
        "$major.$minorPart.$($patchPart + 1)"
    }

    Write-Note "$lastTag -> v$next"

    $changelog = Get-Content (Join-Path $Root 'CHANGELOG.md') -Raw
    $hasSection = $changelog -match "(?m)^##\s+$([regex]::Escape($next))\b"

    if (-not $hasSection) {
        Write-Host ''
        Write-Warning "CHANGELOG.md has no '## $next' section."
        Write-Note 'The tag annotation and the release body are both read out of it, so write it'
        Write-Note 'first. Nothing has been committed, tagged or pushed.'
        return
    }

    Write-Note "CHANGELOG.md has its '## $next' section."

    if ($DryRun) {
        Write-Step 'Stopping, because -DryRun.'
        return
    }

    Write-Step "Handing over to release.ps1 $($kind.ToLowerInvariant()) -PreRelease"

    & (Join-Path $PSScriptRoot 'release.ps1') $kind -PreRelease -Yes
}
finally {
    Pop-Location
}
