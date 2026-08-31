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
    `latest`      — the release GitHub flags as latest, which is the pin `/releases/latest` follows
                    and therefore what the in-app updater offers: the build a Commander who does
                    nothing ends up on.
    `prerelease`  — the newest pre-release **newer than that**, which is the one waiting to be
                    promoted. See the note below on why the qualifier is not pedantry.
    `0.79.0`      — that exact version, asked of GitHub by name rather than looked for in a page.
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

.PARAMETER Force
    Accepted and does nothing, the way `get-local`'s now is. Stopping a running d47 before the
    install is the default since 2026-08-30, on the Commander's instruction, so the switch that
    used to ask for it has nothing left to ask for — and it is taken rather than refused because
    it appears in this file's own examples and in the habit of anybody who read them.

    **`flight-on` is deliberately not changed with them.** Its refusal is a different animal: d47
    holds a single-instance mutex, so a second copy launched with the recorder switch surfaces the
    one already running rather than recording, and going ahead would look exactly like the switch
    not working. That is why its escape hatch is named `-Restart` rather than `-Force`.

    Nothing is stopped for `-Zip` or `-DownloadOnly`, which replace nothing a running d47 holds.

.PARAMETER Silent
    Run the installer without its wizard (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`). For an
    unattended run, which the wizard cannot serve: this now waits for the installer to finish, and
    waiting on clicks nobody is there to make is a hang rather than an install.

.PARAMETER NoSelfTest
    Skip `--selftest` on the installed build. That check is what proves the payload is complete —
    it is the gate that caught natives missing from a published build in 0.5.14 — so this is for
    when the check itself is what is being examined.

.EXAMPLE
    tools/get-ver.ps1 0.79.0
    tools/get-ver.ps1 0.79
    tools/get-ver.ps1 prerelease -DownloadOnly
    tools/get-ver.ps1 latest
    tools/get-ver.ps1 latest -Silent

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

    [switch] $NoBackup,

    [switch] $Force,

    [switch] $Silent,

    [switch] $NoSelfTest
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
    # **The limit is above the number of releases on purpose** (#185). At 100 this was one page of
    # what were already 178, so `0.5` answered "no release under 0.5" about a whole minor that
    # exists - the same paging fault as the exact form's, which is answered by name above instead.
    # `latest` and `prerelease` cannot change with the older ones present: one is a flag GitHub
    # sets, and the other only ever looks *above* the current latest.
    $raw = Invoke-Native {
        gh release list --repo $Repo --limit 500 --exclude-drafts `
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
    One release in full, or $null when there is no such tag.

    **Asked for by name rather than looked for in a page** (#185). `gh release list --limit 100` is
    one page of what were 178 published releases, so matching an exact version inside it answered
    "0.5.14 is not a release" about a release that plainly exists — and fetching an old build to
    reproduce a Commander's report is the documented reason this script exists. It also drops a
    round trip on that road: this is the same call the body below needs anyway.
#>
function Get-Release {
    param([string] $Tag)

    $raw = Invoke-Native {
        gh release view $Tag --repo $Repo `
            --json tagName,isPrerelease,isDraft,publishedAt,assets 2>&1 | ForEach-Object { "$_" }
    }

    # A tag that does not exist is gh exiting non-zero with "release not found" on stderr, which is
    # an answer rather than a failure. The caller says what to make of it.
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($raw -join "`n") | ConvertFrom-Json
}

# Compared as a version rather than by date, which is what `gh release list` orders by. Lifted from
# promote.ps1, which states the reason at its own copy: those two agree right up until they do not.
function ConvertTo-Version {
    param([string] $Tag)

    if ($Tag -match '^v(\d+)\.(\d+)\.(\d+)$') {
        return [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3])
    }

    return $null
}

<#
    Which release the Commander meant, in full. Writes an error naming what was there instead -
    "no such version" is only useful when it says what the alternatives were.
#>
function Resolve-Release {
    param([string] $Wanted)

    $number = $Wanted.TrimStart('v', 'V')

    # The exact form is answered without the listing at all, which is both the fix and the round
    # trip saved. The list is fetched below only where the answer needs a page of them.
    if ($number -match '^\d+\.\d+\.\d+$') {
        $exact = Get-Release -Tag "v$number"

        if (-not $exact) {
            # Listed only to say what there was instead, and only on the road that already failed.
            $all = Get-Releases
            $newest = if ($all.Count -gt 0) { $all[0].tagName } else { 'nothing' }

            Write-Error "v$number is not a release. Newest is $newest."
        }

        return $exact
    }

    $releases = Get-Releases

    if ($releases.Count -eq 0) {
        Write-Error "No releases found in $Repo."
    }

    $latest = $releases | Where-Object { $_.isLatest } | Select-Object -First 1

    if ($Wanted -eq 'latest') {
        # **The field GitHub already sends** (#185). It was fetched and then ignored, and latest was
        # re-derived as the newest non-pre-release by date - a rule that agrees with the flag most
        # days and is not the same question. `isLatest` is the pin `/releases/latest` returns, which
        # is the endpoint `UpdateChecker` reads, so it is what "what I get if I do nothing" means.
        if (-not $latest) {
            Write-Error 'No release is flagged latest, so nothing is being offered to anybody. Ask for one by version, or for prerelease.'
        }

        $tag = $latest.tagName
    }
    elseif ($Wanted -in @('prerelease', 'pre-release', 'pre')) {
        $latestVersion = if ($latest) { ConvertTo-Version -Tag $latest.tagName } else { $null }

        # **promote.ps1's guard, and it belongs here for the same reason** (#185). The plain reading
        # takes the first pre-release by date, and on 2026-08-27 that was v0.78.1 - still flagged
        # pre-release after v0.79.0 had been promoted past it. So `get-ver prerelease`, whose whole
        # job is "the build that is waiting", would have installed the superseded one.
        $waiting = @(
            $releases |
                Where-Object { $_.isPrerelease } |
                Where-Object {
                    $version = ConvertTo-Version -Tag $_.tagName
                    $version -and (-not $latestVersion -or $version -gt $latestVersion)
                }
        )

        if ($waiting.Count -eq 0) {
            # One string rather than three: `Write-Error 'a' + "b"` binds `+` and the second string
            # as further positional arguments, so this printed a parameter-binding exception instead
            # of its own sentence (#185, confirmed under 5.1).
            $name = if ($latest) { $latest.tagName } else { $releases[0].tagName }

            Write-Error "No pre-release newer than $name is waiting. Name a version to fetch an older one."
        }

        $tag = $waiting[0].tagName
    }
    elseif ($number -match '^\d+\.\d+$') {
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

        $tag = $matched[0].tagName
    }
    else {
        Write-Error "Cannot read '$Wanted' as a version. Try 0.79.0, 0.79, latest or prerelease."
    }

    $found = Get-Release -Tag $tag

    if (-not $found) {
        Write-Error "$tag is in the release list and then could not be read. Check it: gh release view $tag"
    }

    return $found
}

if ($Force) {
    Write-Note '-Force is no longer needed: a running d47 is stopped before the install either way.'
}

# **Said here, done later** (#186, amended 2026-08-30). This used to refuse, and the refusal was
# here rather than at the install step so that a run which could not finish said so before the
# download rather than after it. Stopping is now the default and there is nothing left to refuse,
# but the warning keeps its place: "this will close the d47 you are looking at" is worth hearing
# before a download rather than after one.
#
# Only on the road that installs, since -Zip extracts beside itself and -DownloadOnly replaces
# nothing at all. The stop itself happens later, just before the installer runs; see get-local,
# where that ordering is the same rule.
if (-not $Zip -and -not $DownloadOnly) {
    $running = @(Get-Process d47 -ErrorAction SilentlyContinue)

    if ($running.Count -gt 0) {
        Write-Note "d47 is running (pid $($running.Id -join ', ')). It will be stopped before the install."
    }
}

$release = Resolve-Release -Wanted $Version
$tag = $release.tagName

# `release view` will hand over a draft, which the listing's --exclude-drafts used to hide on every
# road. A draft's assets are not published and its tag may still move, so it is not a build to fly.
if ($release.isDraft) {
    Write-Error "$tag is still a draft. Its assets are not published and its tag can still move."
}

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
$installedExe = Join-Path $installed 'd47.exe'

# Stopped here rather than before the download, for the reason get-local gives at its own copy: a
# session that dies minutes early dies for nothing if what comes next fails. Re-read rather than
# reused, because the download took time and a d47 may have been started during it.
$running = @(Get-Process d47 -ErrorAction SilentlyContinue)

if ($running.Count -gt 0) {
    Write-Step "Stopping d47 (pid $($running.Id -join ', '))"

    # -Force here is Stop-Process's own switch and has nothing to do with the parameter above: it
    # closes a process that owns a window without asking it to agree, which is exactly the case.
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 20
}

if (-not $NoBackup -and (Test-Path (Join-Path $installed 'data'))) {
    & (Join-Path $PSScriptRoot 'data-backup.ps1') -InstallRoot $installed
}

Write-Step "Installing $tag"

# **Waited on and read, rather than launched and assumed** (#185). `& $file` returns the moment the
# wizard is up, and the line below said "installed" whatever happened next: a cancelled wizard
# reported success. Waiting is also what makes -Silent necessary rather than a nicety — an
# unattended run that waits on clicks nobody is there to make is a hang.
# The @() is outside the if, as the asset match above already had to learn: an empty array
# assigned out of an if-expression arrives as $null, and $null.Count is a StrictMode error —
# which killed every non-silent install at exactly this line (#188).
$arguments = @(if ($Silent) { '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' })

$process =
    if ($arguments.Count -gt 0) {
        Start-Process -FilePath $file -ArgumentList $arguments -Wait -PassThru
    }
    else {
        Start-Process -FilePath $file -Wait -PassThru
    }

$code = if ($null -eq $process) { $null } else { $process.ExitCode }

# Inno Setup's codes: 0 is done, 2 and 5 are the Commander cancelling (before and during), and
# anything else is a failure that has already said so on screen. `installer\d47.iss` is per-user and
# unelevated by design, so there is no re-launch to elevate and no handing off of the code.
if ($null -eq $code) {
    # Nothing is claimed from a code that is not there, rather than reading absence as success -
    # which is the whole fault this replaces. --selftest below settles it either way.
    Write-Note 'The installer returned no exit code. The check below is what says whether it worked.'
}
elseif ($code -eq 2 -or $code -eq 5) {
    Write-Error "The installer was cancelled (exit $code), so $tag is NOT installed. The download is still at $file."
}
elseif ($code -ne 0) {
    Write-Error "The installer failed (exit $code). Whatever is in $installed may be half replaced; get-ver latest puts a published build back."
}

if (-not $NoSelfTest) {
    Write-Step 'Checking the payload'

    if (-not (Test-Path $installedExe)) {
        Write-Error "The installer finished, but there is no d47.exe at $installed."
    }

    # The same gate get-local runs, and for the same reason: it is what caught natives missing from
    # a published build in 0.5.14, and an exit code is a claim about the installer rather than
    # about the thing it installed.
    Invoke-Native { & $installedExe --selftest }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "--selftest failed on the installed build, so $tag is not fit to fly. get-ver latest puts a published build back."
    }
}

Write-Step "$tag installed."
Write-Note $installedExe

if ($isPre) {
    Write-Note 'This is a pre-release, so the updater will still offer the latest stable over it.'
}
