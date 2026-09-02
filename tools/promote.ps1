<#
.SYNOPSIS
    Promotes the newest pre-release to latest, so the update checker starts offering it.

.DESCRIPTION
    **The separate act.** CLAUDE.md states it: cutting a release is one command and may be run on
    request, but deciding a build is fit for *everyone* is the Commander's and is a different
    decision. `prerelease` makes builds; this one makes them the build.

    Until this runs, `UpdateChecker` reads `/releases/latest` and is offered the previous release, so
    a pre-release reaches nobody who does not go and fetch it with `get-ver`.

    **Called `promote.ps1` rather than `release.ps1` because that name is taken**, by the script that
    cuts releases. Two files called release doing different things is the hazard; the command on the
    PATH is `release`, which is the word for the act rather than for the file.

.PARAMETER Version
    Which one, as `0.79.0` or `v0.79.0`. Defaults to the newest pre-release, which is nearly always
    what is meant - it is the one that was just cut and flown.

.PARAMETER Show
    Say what would be promoted and stop.

.EXAMPLE
    release
    release 0.79.0
    release -Show

.NOTES
    **This one direction is the one that cannot be taken back.** A mistake in a pre-release costs a
    version number; the same mistake here reaches the install base and can only be superseded, since
    a published tag never moves. So it refuses a release that is a draft or carries no assets, and it
    reads the result back rather than trusting the command it just ran.

    It does not ask. Typing it is the confirmation - but it prints what it is about to do first, so
    an argument typed wrong is visible before the enter key rather than after.
#>

param(
    [Parameter(Position = 0)]
    [string] $Version,

    [switch] $Show
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Repo = 'dseelinger/d47'

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

<#
    Runs gh and parses its JSON, refusing to parse anything it did not succeed at.

    One function because the unguarded version was got right in one place and wrong in another on
    2026-08-27: `gh release view --json isLatest` is not a field that exists, gh said so on stderr,
    `2>&1` folded that into the output, and ConvertFrom-Json reported "Invalid JSON primitive" -
    which names neither the field nor the command. Checking the exit code first turns that into
    gh's own sentence, which already says what is wrong and lists the fields that do exist.
#>
function Invoke-GhJson {
    param([string[]] $Arguments, [string] $What)

    $raw = Invoke-Native { & gh @Arguments 2>&1 | ForEach-Object { "$_" } }

    if ($LASTEXITCODE -ne 0) {
        throw "Could not ${What}: $($raw -join ' ')"
    }

    return ($raw -join "`n") | ConvertFrom-Json
}

# --json and ConvertFrom-Json rather than --jq: a jq filter needs double quotes and Windows
# PowerShell's legacy argument passing re-parses an argument containing them.
#
# An empty array deserialises to one empty object rather than to nothing; unrolled on the way out.
$releases = @(
    (Invoke-GhJson -What 'list releases' -Arguments @(
        'release', 'list', '--repo', $Repo, '--limit', '50', '--exclude-drafts',
        '--json', 'tagName,isPrerelease,isLatest,publishedAt')) | ForEach-Object { $_ }
)

if ($releases.Count -eq 0) {
    throw "No releases in $Repo."
}

# Compared as a version rather than by date, which is what `gh release list` orders by. Those agree
# right up until they do not: a pre-release cut before the current latest is older by version and
# newer by nothing, and promoting it would offer the install base a build it has already left.
function ConvertTo-Version {
    param([string] $Tag)

    if ($Tag -match '^v(\d+)\.(\d+)\.(\d+)$') {
        return [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3])
    }

    return $null
}

$latest = ($releases | Where-Object { $_.isLatest } | Select-Object -First 1)
$latestVersion = if ($latest) { ConvertTo-Version -Tag $latest.tagName } else { $null }

$target =
    if ($Version) {
        $wanted = 'v' + $Version.TrimStart('v', 'V')
        $found = @($releases | Where-Object { $_.tagName -eq $wanted })

        if ($found.Count -eq 0) {
            throw "$wanted is not a release. Newest is $($releases[0].tagName)."
        }

        $found[0]
    }
    else {
        # Newer than what is already out, not merely the most recent pre-release. Those are the same
        # thing on a tidy day and not on 2026-08-27, when v0.78.1 was still flagged pre-release after
        # v0.79.0 had been promoted past it - so the plain reading would have offered every Commander
        # a downgrade, which is the one thing this script exists to not do by accident.
        # **`$candidate`, and the name is the whole of a bug that waited a hundred releases.**
        # This said `$version`, and PowerShell variable names are case-insensitive — so it was the
        # script's own `[string] $Version` parameter, type constraint and all. Assigning a
        # `[version]` to it coerced the object back to a string, and `-gt` then compared two
        # strings.
        #
        # Which agreed with a version comparison for every release this project has ever cut, and
        # stopped agreeing at exactly v0.100.0: "0.100.0" -gt "0.99.0" is False, because "1" sorts
        # before "9". The Commander spotted the shape of it before the cause — "funny it started
        # when we rolled over from 99 to 100".
        $waiting = @(
            $releases |
                Where-Object { $_.isPrerelease } |
                Where-Object {
                    $candidate = ConvertTo-Version -Tag $_.tagName
                    $candidate -and (-not $latestVersion -or $candidate -gt $latestVersion)
                }
        )

        if ($waiting.Count -eq 0) {
            $name = if ($latest) { $latest.tagName } else { $releases[0].tagName }
            throw "No pre-release newer than $name is waiting. Nothing to promote."
        }

        $waiting[0]
    }

# The same guard for a version named by hand, because naming one is not a reason to be allowed to go
# backwards. A release that turned out bad is superseded by the next patch, never by promoting an
# older tag over it - a published tag never moves, and the update checker compares version numbers.
$targetVersion = ConvertTo-Version -Tag $target.tagName

if ($latestVersion -and $targetVersion -and $targetVersion -lt $latestVersion) {
    throw "$($target.tagName) is older than the current latest $($latest.tagName). Promoting it would offer every Commander a downgrade. Ship a new patch instead."
}

$tag = $target.tagName

if (-not $target.isPrerelease) {
    Write-Step "$tag is already released."
    Write-Note 'Nothing to do.'
    return
}

# Read the assets back rather than assuming the workflow finished. A release promoted before its
# build has published is one the updater will offer and then fail to install from.
$detail = Invoke-GhJson -What "read $tag" -Arguments @(
    'release', 'view', $tag, '--repo', $Repo, '--json', 'assets,isDraft')

if ($detail.isDraft) {
    throw "$tag is a draft. Publishing it is a different act from promoting it."
}

$assets = @($detail.assets | ForEach-Object { $_.name })

# The two names every build in the field reaches back and reads (#96). A release without them is one
# no installed d47 can update from, which is the failure that looks like a quiet release cycle.
foreach ($required in @('d47.zip', 'd47.zip.sha256')) {
    if ($assets -notcontains $required) {
        throw "$tag has no $required, so no installed build could update to it. Refusing. It has: $($assets -join ', ')"
    }
}

$currentLatest = ($releases | Where-Object { $_.isLatest } | Select-Object -First 1)

Write-Step "Promoting $tag"
Write-Note "$($assets.Count) assets, including d47.zip and its checksum"

if ($currentLatest) {
    Write-Note "The updater currently offers $($currentLatest.tagName); after this it offers $tag."
}

if ($Show) {
    Write-Step 'Stopping, because -Show. Nothing was changed.'
    return
}

$result = Invoke-Native { gh release edit $tag --repo $Repo --prerelease=false --latest 2>&1 }

if ($LASTEXITCODE -ne 0) {
    throw "Could not promote ${tag}: $($result -join ' ')"
}

# Read it back. The command exiting zero and the release actually being latest are different claims,
# and this is the one place where believing the wrong one reaches every install.
#
# Through `release list` rather than `release view`, because **isLatest is a field on list and not on
# view** - which this script got wrong on its first real run and which the guarded parse above now
# reports properly instead of as a JSON error.
$after = @(
    (Invoke-GhJson -What "read $tag back" -Arguments @(
        'release', 'list', '--repo', $Repo, '--limit', '50', '--exclude-drafts',
        '--json', 'tagName,isPrerelease,isLatest')) |
        ForEach-Object { $_ } | Where-Object { $_.tagName -eq $tag }
)

if ($after.Count -eq 0) {
    throw "$tag did not come back in the release list at all. Check it: gh release view $tag"
}

if ($after[0].isPrerelease -or -not $after[0].isLatest) {
    throw "$tag did not read back as the latest release. Check it: gh release view $tag"
}

Write-Step "$tag is the latest release."
Write-Note 'Every installed d47 will be offered it on its next check.'
