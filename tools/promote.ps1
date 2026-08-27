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

# --json and ConvertFrom-Json rather than --jq: a jq filter needs double quotes and Windows
# PowerShell's legacy argument passing re-parses an argument containing them.
$raw = Invoke-Native {
    gh release list --repo $Repo --limit 50 --exclude-drafts `
        --json tagName,isPrerelease,isLatest,publishedAt 2>&1
}

if ($LASTEXITCODE -ne 0) {
    throw "Could not list releases: $($raw -join ' ')"
}

# An empty array deserialises to one empty object rather than to nothing; unrolled on the way out.
$releases = @($raw | ConvertFrom-Json | ForEach-Object { $_ })

if ($releases.Count -eq 0) {
    throw "No releases in $Repo."
}

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
        $found = @($releases | Where-Object { $_.isPrerelease })

        if ($found.Count -eq 0) {
            $current = ($releases | Where-Object { $_.isLatest } | Select-Object -First 1)
            $name = if ($current) { $current.tagName } else { $releases[0].tagName }

            throw "There is no pre-release waiting. $name is already the latest."
        }

        $found[0]
    }

$tag = $target.tagName

if (-not $target.isPrerelease) {
    Write-Step "$tag is already released."
    Write-Note 'Nothing to do.'
    return
}

# Read the assets back rather than assuming the workflow finished. A release promoted before its
# build has published is one the updater will offer and then fail to install from.
$detail = Invoke-Native {
    gh release view $tag --repo $Repo --json assets,isDraft 2>&1
} | ConvertFrom-Json

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
$after = Invoke-Native {
    gh release view $tag --repo $Repo --json isPrerelease,isLatest 2>&1
} | ConvertFrom-Json

if ($after.isPrerelease -or -not $after.isLatest) {
    throw "$tag did not read back as the latest release. Check it: gh release view $tag"
}

Write-Step "$tag is the latest release."
Write-Note 'Every installed d47 will be offered it on its next check.'
