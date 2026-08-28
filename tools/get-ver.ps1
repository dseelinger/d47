<#
.SYNOPSIS
    Downloads and installs a named d47 release, resolving a partial version to a real one.

.DESCRIPTION
    For flying a specific build rather than whatever the updater is offering — a pre-release before
    it is promoted, or an older version to reproduce something a Commander reported.

    **The installer's asset name is deliberately not constructed.** `d47.zip` and its checksum are a
    contract that every build in the field reads back (see #96 and `UpdateChecker`), but the
    installer's name is not one — it carries the version today and nothing promises it always will.
    So assets are matched by pattern out of the release rather than spelled out here, which is the
    trap that commit exists to name, arriving by a different road.

    **`gh release` rather than the raw API**, which `release.ps1` already argues: `gh api` is the
    road around `tools/issues.ps1`, `.claude/settings.json` denies it, and a script that needs a
    denied command is a reason to lift the deny rather than to write the script that way.

.PARAMETER Version
    `latest`      — the newest release that is neither a draft nor a pre-release. What the in-app
                    updater reads, so this is the build a Commander who does nothing ends up on.
    `prerelease`  — the newest pre-release, promoted or not.
    `0.79.0`      — that exact version.
    `0.79`        — the highest patch under that minor.

    **A version you name includes pre-releases; `latest` does not.** Naming a version is saying you
    know what you are asking for, and today's 0.79.0 is a pre-release — excluding those would have
    `get-ver 0.79` answer "no such release" about a release that plainly exists. `latest` means the
    opposite thing on purpose: it is the question "what would I get if I did nothing?"

.PARAMETER DownloadOnly
    Fetch and verify, then stop and say where the file is. Nothing is run.

    **This is the only brake, because there is no prompt.** The command is "get me this version", so
    it fetches it, checks it and installs it — a confirmation asking whether you meant the thing you
    just typed is friction rather than safety. `release.ps1` asks before it tags because a tag cannot
    be taken back; an install can simply be redone with a different argument.

.PARAMETER Zip
    Take `d47.zip` instead of the installer — the portable build, for a side-by-side that must not
    touch the installed one. Extracted rather than run; see `two installs, two data folders`.

.PARAMETER NoBackup
    Skip the snapshot of the installed `data\` folder. Taken by `tools\data-backup.ps1` into
    `data\backups\` before the installer runs - one zip per deploy, the last ten kept - because a
    build migrates data, so going back a version without the data that version was written against
    is only half a rollback. Nothing is taken for `-Zip` or `-DownloadOnly`, which install nothing,
    or where there is no install to snapshot.

.PARAMETER Path
    Where to put the download. Defaults to a folder under TEMP named for the version.

.EXAMPLE
    tools/get-ver.ps1 0.79.0
    tools/get-ver.ps1 0.79
    tools/get-ver.ps1 prerelease -DownloadOnly
    tools/get-ver.ps1 latest

.NOTES
    **What the checksum proves, and what it does not.** Every release publishes a SHA-256 beside its
    asset and this refuses a file that does not match. That proves the download was not corrupted or
    truncated in transit. It does **not** prove the bytes are the Commander's, because the hash is
    published on the same host as the file it describes — the exact gap
    [#95](https://github.com/dseelinger/d47/issues/95) exists to close by signing. Worth knowing
    before trusting a green tick here for more than it says.
#>

param(
    [Parameter(Mandatory, Position = 0)]
    [string] $Version,

    [switch] $DownloadOnly,

    [switch] $Zip,

    [string] $Path,

    [switch] $NoBackup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Repo = 'dseelinger/d47'

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

# gh writes ordinary progress and refusals to stderr, and Windows PowerShell turns any stderr line
# from a native command into a terminating error while ErrorActionPreference is Stop. Same trap as
# release.ps1's -PreRelease on v0.78.0 and issues.ps1's; not repeated here.
function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

function Get-Releases {
    # --json and ConvertFrom-Json rather than --jq: a jq filter needs double quotes around every
    # string, and Windows PowerShell's legacy argument passing re-parses an argument containing
    # them, so gh sees several positional arguments and refuses. Found on 2026-08-27 building #94.
    $raw = Invoke-Native {
        gh release list --repo $Repo --limit 100 --exclude-drafts `
            --json tagName,isPrerelease,isLatest,publishedAt 2>&1
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Could not list releases: $($raw -join ' ')"
    }

    # An empty array deserialises to a single empty object rather than to nothing, and the loop then
    # asks it for a property it has never had. Unrolled on the way out.
    return @($raw | ConvertFrom-Json | ForEach-Object { $_ })
}

<#
    Which release the Commander meant. Returns the tag, or writes an error naming what was there
    instead - "no such version" is only useful when it says what the alternatives were.
#>
function Resolve-Version {
    param([string] $Wanted)

    $releases = Get-Releases

    if ($releases.Count -eq 0) {
        Write-Error "No releases found in $Repo."
    }

    if ($Wanted -eq 'latest') {
        # The newest that is neither draft nor pre-release: the definition of what the updater
        # offers, and therefore of "what I get if I do nothing".
        $found = $releases | Where-Object { -not $_.isPrerelease } | Select-Object -First 1

        if (-not $found) {
            Write-Error 'Every release is a pre-release. Ask for one by version, or for prerelease.'
        }

        return $found.tagName
    }

    if ($Wanted -in @('prerelease', 'pre-release', 'pre')) {
        $found = $releases | Where-Object { $_.isPrerelease } | Select-Object -First 1

        if (-not $found) {
            Write-Error 'There is no pre-release right now. The newest release is ' +
                        "$($releases[0].tagName)."
        }

        return $found.tagName
    }

    $number = $Wanted.TrimStart('v', 'V')

    if ($number -match '^\d+\.\d+\.\d+$') {
        $tag = "v$number"
        $found = $releases | Where-Object { $_.tagName -eq $tag }

        if (-not $found) {
            Write-Error "$tag is not a release. Newest is $($releases[0].tagName)."
        }

        return $tag
    }

    if ($number -match '^\d+\.\d+$') {
        # Highest patch under that minor, sorted as a number rather than as text: "v0.79.10" sorts
        # below "v0.79.9" as a string, and that is a bug that only appears after ten patches.
        $matched =
            $releases |
            Where-Object { $_.tagName -match "^v$([regex]::Escape($number))\.(\d+)$" } |
            Sort-Object { [int]($_.tagName -replace "^v$([regex]::Escape($number))\.", '') } -Descending

        $matched = @($matched)

        if ($matched.Count -eq 0) {
            Write-Error "No release under $number. Newest is $($releases[0].tagName)."
        }

        return $matched[0].tagName
    }

    Write-Error "Cannot read '$Wanted' as a version. Try 0.79.0, 0.79, latest or prerelease."
}

$tag = Resolve-Version -Wanted $Version

$release = Invoke-Native {
    gh release view $tag --repo $Repo --json tagName,isPrerelease,publishedAt,assets 2>&1
} | ConvertFrom-Json

$isPre = $release.isPrerelease
$published = ([datetime]$release.publishedAt).ToString('yyyy-MM-dd')

Write-Step "$tag$(if ($isPre) { '  (pre-release)' })"
Write-Note "published $published, asked for as '$Version'"

if ($isPre) {
    Write-Note 'A pre-release is not offered by the updater. This is the way to get one on purpose.'
}

# Matched out of the release rather than spelled out. d47.zip IS a contract and may be named; the
# installer is not, so it is found by shape.
$wantedAsset = if ($Zip) { 'd47.zip' } else { $null }

# The @() is outside the if, not inside it: an array assigned out of an if-expression is unrolled
# on the way, so a one-asset match arrives as a bare object and $asset.Count is a StrictMode error
# rather than 1. The same unrolling that bites a function returning an empty array.
$asset = @(
    if ($Zip) {
        $release.assets | Where-Object { $_.name -eq $wantedAsset }
    }
    else {
        $release.assets | Where-Object { $_.name -like '*setup*.exe' }
    }
)

if ($asset.Count -eq 0) {
    $names = ($release.assets | ForEach-Object { $_.name }) -join ', '
    Write-Error "$tag carries no $(if ($Zip) { 'd47.zip' } else { 'installer' }). It has: $names"
}

if ($asset.Count -gt 1) {
    $names = ($asset | ForEach-Object { $_.name }) -join ', '
    Write-Error "$tag has more than one candidate and this will not guess between them: $names"
}

$assetName = $asset[0].name

$target = if ($Path) { $Path } else { Join-Path $env:TEMP "d47-$($tag.TrimStart('v'))" }
New-Item -ItemType Directory -Force -Path $target | Out-Null

Write-Step "Downloading $assetName"
Write-Note $target

# --clobber so a re-run is not an error, which is the common case when a download was interrupted.
$download = Invoke-Native {
    gh release download $tag --repo $Repo --dir $target --clobber `
        --pattern $assetName --pattern "$assetName.sha256" 2>&1
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Download failed: $($download -join ' ')"
}

$file = Join-Path $target $assetName
$sumFile = "$file.sha256"

if (-not (Test-Path $file)) {
    Write-Error "$assetName did not arrive."
}

Write-Step 'Verifying the checksum'

if (-not (Test-Path $sumFile)) {
    # Said rather than shrugged at: a missing checksum is not the same as a passing one, and a run
    # that quietly skipped the check would look identical to a run that made it.
    Write-Error "$assetName.sha256 is not published on $tag, so this cannot be verified. Refusing."
}

# The file is "<hash>  <name>"; take the first field and nothing else.
$expected = ((Get-Content $sumFile -First 1) -split '\s+')[0].ToLowerInvariant()
$actual = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()

if ($expected -ne $actual) {
    Write-Error "CHECKSUM MISMATCH. Expected $expected, got $actual. The file is not what $tag says it is; it has been left at $file."
}

Write-Note "sha256 $actual"
Write-Note 'Matches. That proves it arrived intact, not that the bytes are the Commander''s - the'
Write-Note 'hash is published on the same host as the file (#95 is what closes that).'

if ($Zip) {
    $extracted = Join-Path $target 'd47'
    Write-Step "Extracting to $extracted"
    Expand-Archive -Path $file -DestinationPath $extracted -Force
    Write-Note 'Portable build. It writes to data\ beside the exe, so it will not touch an installed one.'
    Write-Note (Join-Path $extracted 'd47.exe')
    return
}

if ($DownloadOnly) {
    Write-Step 'Downloaded and verified. Not installed, because -DownloadOnly.'
    Write-Note $file
    return
}

# Before the installer, and only where there is something to snapshot: a first install has no
# data folder, and asking for one would turn "get me d47" into an error. One implementation,
# invoked rather than repeated - what is archived lives in that file and only there.
$installed = Join-Path $env:LOCALAPPDATA 'Programs\d47'

if (-not $NoBackup -and (Test-Path (Join-Path $installed 'data'))) {
    & (Join-Path $PSScriptRoot 'data-backup.ps1') -InstallRoot $installed
}

Write-Step "Installing $tag"

Invoke-Native { & $file }

Write-Step "$tag installed."

if ($isPre) {
    Write-Note 'This is a pre-release, so the updater will still offer the latest stable over it.'
}
