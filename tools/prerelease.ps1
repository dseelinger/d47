<#
.SYNOPSIS
    Cuts a pre-release, working out on its own whether it is a minor or a patch.

.DESCRIPTION
    A front end to `release.ps1`, which does all the real work. What this adds is the one decision a
    person makes wrong: **whether the version is a minor or a patch.**

    CLAUDE.md states the rule and this applies it rather than restating it:

      A completed phase in list.md is always a minor release, because the version is how a
      Commander tells "some fixes landed" from "there is a whole capability here now".
      A batch of wanted changes is a minor for the same reason. Fixes between phases are patches.

    **It is a rule that has already been got wrong, which is why it is worth automating.** The
    memory of this repository records the trap in as many words — *check list.md for a newly ticked
    phase before tagging* — and it is exactly the sort of thing that is obvious on the day the phase
    ships and invisible three days later, when the tag is being cut for something else.

    So the phase state is read out of `list.md` **at the last tag** and compared with the working
    tree, rather than remembered. Two things make it a minor:

      - a phase header that is ticked now and was not at the last tag, or is ticked and new
      - a `change-request` issue closed since the last tag - a batch of wanted changes

    Anything else is a patch.

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
        Which phases are ticked, read out of a given list.md. A map of number -> completed, so a
        phase that was already ticked at the last tag cannot be mistaken for one that just landed.
    #>
    function Get-PhaseState {
        param([string] $Text)

        $state = @{}

        foreach ($line in ($Text -split "`n")) {
            if ($line -match '^- \[([ x])\] \*\*Phase (\d+)') {
                $state[[int]$Matches[2]] = ($Matches[1] -eq 'x')
            }
        }

        return $state
    }

    $thenText = (Invoke-Native { git show "${lastTag}:list.md" }) -join "`n"
    $nowText = Get-Content (Join-Path $Root 'list.md') -Raw

    $then = Get-PhaseState -Text $thenText
    $now = Get-PhaseState -Text $nowText

    $landed = @(
        foreach ($number in ($now.Keys | Sort-Object)) {
            if (-not $now[$number]) { continue }

            # Ticked now, and either not ticked then or not present then.
            if (-not $then.ContainsKey($number) -or -not $then[$number]) {
                $number
            }
        }
    )

    # A batch of wanted changes is a minor for the same reason a phase is. Since 2026-08-27 those
    # are issues rather than a file, so this asks GitHub rather than diffing anything.
    $tagged = (Invoke-Native { git log -1 --format=%cI $lastTag } | Select-Object -First 1)

    $closedRequests = @()

    try {
        $raw = Invoke-Native {
            gh issue list --repo dseelinger/d47 --state closed --label change-request `
                --limit 50 --json number,title,closedAt 2>&1
        }

        if ($LASTEXITCODE -eq 0) {
            $closedRequests = @(
                $raw | ConvertFrom-Json | ForEach-Object { $_ } |
                    Where-Object { $_.closedAt -and ([datetime]$_.closedAt) -gt ([datetime]$tagged) }
            )
        }
    }
    catch {
        Write-Warning "Could not read closed change requests, so only list.md was consulted: $($_.Exception.Message)"
    }

    foreach ($number in $landed) {
        Write-Note "Phase $number is ticked in list.md and was not at $lastTag"
    }

    foreach ($request in $closedRequests) {
        Write-Note "change-request #$($request.number) closed since $lastTag"
    }

    if ($landed.Count -eq 0 -and $closedRequests.Count -eq 0) {
        Write-Note 'No phase landed and no change request closed.'
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
