<#
.SYNOPSIS
    Publishes what is in this working tree and installs it over the installed d47.

.DESCRIPTION
    For flying a change without cutting a release for it. `get-ver` fetches a build somebody
    published; this one builds the working tree and puts it where the installed d47 lives, so it
    runs against the Commander's real `data\` — their settings, their secrets, their ship stores and
    the 325 MB local voice model — which is the whole point and the thing a portable build cannot do.

    **Release, never Debug, and this is not a preference.** A Debug build carries
    `AssemblyMetadata("DevInstallRoot", ...)` pointing at `dev-install\`, compiled in by
    `D47.App.csproj` for that configuration only. Copy one into the install folder and it still
    reads and writes `dev-install\data` — no settings, no secrets, no downloaded model — so it
    would look broken for reasons that have nothing to do with the change being tested. See
    `AppPaths.ForRunningBuild` and the `bin is disposable` rule in CLAUDE.md.

    **The payload is exactly what the installer ships**: `d47.exe` and `runtimes\`, the two entries
    in `installer\d47.iss`. `data\` is never touched and never mirrored — deleting it costs the
    Commander their checklist, their settings and a 325 MB download, which has happened once
    already (2026-08-23).

    **The version says what it is, in the one place that keeps the whole stamp.** The build is
    stamped `<newest tag>-local`, and About shows the full string — `0.84.3-local+<sha>` — so a
    screenshot or a bug report says outright that this was not a published build.

    **And the title bar says so too, since 2026-08-28**: `0.84.3 (local build)`, with a matching
    badge on the panel. It did not at first, and what it said instead was worse than nothing -
    `ReleaseVersion` strips everything from the first `-` or `+`, so a local build compares *equal*
    to the release it was cut from, the app asked GitHub what channel `0.84.3` was on, and a
    hand-built binary came up wearing the published pre-release's badge. The channel is now read
    from the binary whenever the version carries a label, and GitHub is not asked at all.

    The version still compares equal, deliberately, so the updater will not offer to replace a
    local build with the release it came from.

    **The data folder is snapshotted first**, by `tools\data-backup.ps1`, into `data\backups\` —
    one zip per deploy, the last ten kept. A build migrates data, so swapping the executable back
    without the data it was written against is only half a rollback. `-NoBackup` skips it.

    **The way back is `get-ver`.** Nothing here is backed up, because a real build is one command
    away and a backup is a second thing to trust:

        get-ver latest        # whatever a Commander who does nothing ends up on
        get-ver 0.84.3        # a specific release, pre-release or not

.PARAMETER NoBuild
    Skip the publish and install whatever is already in `bin\Release\publish`. For a second install
    of a build that has not changed, and refused if there is nothing there.

.PARAMETER Force
    Stop a running d47 first. Without it, a running instance is an error rather than a silent kill:
    the Commander may be mid-flight, and the file lock is the least of what is lost.

.PARAMETER NoBackup
    Skip the snapshot of `data\`. The snapshot runs before anything is replaced, so a failure here
    stops the install rather than leaving one half done — which is the right way round, and this is
    the switch for when it is in the way.

.PARAMETER NoSelfTest
    Skip the plumbing check after the copy. `--selftest` is what proves the payload is complete —
    it is the gate that caught natives missing from a published build in 0.5.14 — so this is for
    when the check itself is what is being changed.

.PARAMETER InstallRoot
    Where the installed d47 lives. Defaults to the Programs folder the installer uses, and is here
    so a second install can be targeted rather than assumed.

.EXAMPLE
    tools/get-local.ps1
    tools/get-local.ps1 -Force
    tools/get-local.ps1 -NoBuild
#>

param(
    [switch] $NoBuild,

    [switch] $Force,

    [switch] $NoBackup,

    [switch] $NoSelfTest,

    [string] $InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\d47')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\D47.App'
$publish = Join-Path $project 'bin\Release\publish'
$exe = Join-Path $InstallRoot 'd47.exe'

if (-not (Test-Path $InstallRoot)) {
    Write-Error "There is no installed d47 at $InstallRoot. Install one first: get-ver latest"
}

# Before the build rather than after it, because a five-minute publish that cannot be copied
# anywhere is five minutes nobody gets back.
$running = @(Get-Process d47 -ErrorAction SilentlyContinue)

if ($running.Count -gt 0) {
    if (-not $Force) {
        Write-Error "d47 is running (pid $($running.Id -join ', ')). Close it, or pass -Force to stop it."
    }

    Write-Step 'Stopping d47'
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 20
}

if ($NoBuild) {
    if (-not (Test-Path (Join-Path $publish 'd47.exe'))) {
        Write-Error "-NoBuild, but there is no published build at $publish. Run without it."
    }

    Write-Step 'Not building, because -NoBuild.'
}
else {
    # Named for what it is, off the newest tag, so About says which release this was cut from and
    # that it is not that release, and the title bar marks it as a local build.
    $tag = (git -C $repo describe --tags --abbrev=0 2>$null)
    $version = if ($LASTEXITCODE -eq 0 -and $tag) { "$($tag.TrimStart('v'))-local" } else { '0.1.0-local' }

    Write-Step "Publishing $version"
    Write-Note 'Release. A Debug build would read dev-install\data and see none of your settings.'

    dotnet publish $project -c Release -p:Version=$version

    if ($LASTEXITCODE -ne 0) {
        Write-Error 'The publish failed, so nothing has been copied.'
    }
}

# Before the copy and after the build, so a failed publish costs no snapshot and a successful one
# cannot replace anything until the data it is replacing has been kept. One implementation, invoked
# rather than repeated: the rules about what is archived live in that file and only there.
if (-not $NoBackup) {
    & (Join-Path $PSScriptRoot 'data-backup.ps1') -InstallRoot $InstallRoot
}

Write-Step "Installing over $InstallRoot"

# Exactly the installer's two entries, and checked one at a time: a second robocopy overwrites the
# first one's exit code, so a failed executable behind a successful runtimes\ would report success.
#
# /E rather than /MIR. Mirroring would delete data\ and the uninstaller with it, and data\ is the
# one folder in this repository's rules that is never collateral (2026-08-23).
function Copy-Payload {
    param([string] $From, [string] $To, [string[]] $Extra)

    robocopy $From $To @Extra /NJH /NJS /NP /NDL | Out-Null

    # Robocopy's exit code is a bitfield: under 8 is a success of some kind, 8 and above a failure.
    if ($LASTEXITCODE -ge 8) {
        Write-Error "The copy failed (robocopy $LASTEXITCODE) on $From. Half replaced; get-ver latest puts it back."
    }

    $global:LASTEXITCODE = 0
}

Copy-Payload $publish $InstallRoot @('d47.exe')
Copy-Payload (Join-Path $publish 'runtimes') (Join-Path $InstallRoot 'runtimes') @('/E')

if (-not $NoSelfTest) {
    Write-Step 'Checking the payload'

    & $exe --selftest

    if ($LASTEXITCODE -ne 0) {
        Write-Error "--selftest failed, so this build is not fit to fly. get-ver latest puts a real one back."
    }
}

Write-Step 'Installed.'
Write-Note $exe
Write-Note 'It runs against the installed data\ folder, so this is your real settings and models.'
Write-Note 'Back to a published build: get-ver latest'
