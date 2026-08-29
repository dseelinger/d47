<#
.SYNOPSIS
    Snapshots the installed d47's data folder, keeps the last ten, and puts one back.

.DESCRIPTION
    **A build migrates the Commander's data, and a version number can go backwards.** `settings.json`
    is append-only and the stores are tolerant, but a newer build can still write a `data\` folder an
    older one reads differently — so swapping the executable and leaving the data alone is only half
    a rollback. `get-ver` and `get-local` both call this before they replace anything, which makes
    going back a real option rather than a hope.

    Snapshots live in `data\backups\`, newest last, ten kept. A zip is written to TEMP and moved in,
    because writing an archive into the folder being archived is a race with itself.

    **Four folders are left out, and one of them is the whole reason this is affordable.**

      `models\`    1,064 MB of the installed 1,072 — the local voice and the Whisper models. They
                   are downloaded, not written by d47, and are identical across versions. Ten
                   backups including them would be ten gigabytes to protect eight megabytes.
      `logs\`      Churn, and the running app holds today's file open. Read them where they are.
      `updates\`   Downloaded installers, re-fetchable by name with `get-ver`.
      `backups\`   Itself, or each snapshot would contain every snapshot before it.
      `flight\`    The audio flight recorder's clips (#164). Disposable evidence rather than
                   Commander data, capped and off unless it was asked for.

    `audio\` is **kept**: the Commander's own cues, beds and ambience are theirs and nothing else
    holds a copy.

    **A restore takes a snapshot first**, so putting the wrong one back is itself undoable. It
    expands over the top rather than mirroring — a mirror would be a delete pass over a folder that
    also holds a gigabyte of models, and a wrong path in a delete pass is the failure this whole
    file exists to make survivable.

.PARAMETER List
    Show what is held, newest last, with sizes and dates. Changes nothing.

.PARAMETER Restore
    Put one back: a file name from `-List`, or `latest`.

.PARAMETER Keep
    How many to hold. Ten by default; the oldest beyond it are deleted after a new one is taken.

.PARAMETER IncludeModels
    Archive `models\` too. For a deliberate full copy before something drastic — not for the
    automatic snapshot, which would be a gigabyte every install.

.PARAMETER Label
    What to call this one, beside the date. Defaults to the version of the executable being
    replaced, which is the useful name: *the data as the 0.84.2 build left it*.

.PARAMETER InstallRoot
    Where the installed d47 lives. Defaults to the folder the installer uses.

.EXAMPLE
    tools/data-backup.ps1
    tools/data-backup.ps1 -List
    tools/data-backup.ps1 -Restore latest
    tools/data-backup.ps1 -Restore data-0.84.2-20260828-101500.zip
#>

param(
    [switch] $List,

    [string] $Restore,

    # **Never zero** (<https://github.com/dseelinger/d47/issues/171>). -Keep 0 turned the backup
    # command into a delete-all-snapshots command, which is one typo away in an unattended
    # invocation and is not undoable: the trim uses Remove-Item -Force, not the recycle bin.
    [ValidateRange(1, 100)]
    [int] $Keep = 10,

    [switch] $IncludeModels,

    [string] $Label,

    # A snapshot the trim must not drop, by full path. Set only by the restore path below, on
    # itself.
    [string] $Protect,

    [string] $InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\d47')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

$data = Join-Path $InstallRoot 'data'
$backups = Join-Path $data 'backups'

# Named rather than spelled out at each use, so the two lists that must agree — what is archived and
# what is skipped on the way back in — are one list.
$Skip = @('backups', 'flight', 'logs', 'updates') + $(if ($IncludeModels) { @() } else { @('models') })

function Get-Payload {
    param([string] $Root)

    Get-ChildItem $Root -Recurse -File -Force | Where-Object {
        $relative = $_.FullName.Substring($Root.Length).TrimStart('\')
        $top = ($relative -split '\\')[0]

        $Skip -notcontains $top
    }
}

<#
    .SYNOPSIS
        The version of the build being replaced, which is what a snapshot is worth naming after.
#>
function Get-InstalledVersion {
    $exe = Join-Path $InstallRoot 'd47.exe'

    if (-not (Test-Path $exe)) {
        return 'unknown'
    }

    $stamp = (Get-Item $exe).VersionInfo.ProductVersion

    if (-not $stamp) {
        return 'unknown'
    }

    # The SDK appends "+<sha>" to every build. Forty characters of hash in a file name is noise,
    # and the same information is in the changelog against the version.
    $plus = $stamp.IndexOf('+')

    if ($plus -ge 0) { $stamp.Substring(0, $plus) } else { $stamp }
}

# @() at every call site, not just inside: an array handed back by a function is unrolled on the
# way out, so a one-snapshot folder arrives as a bare FileInfo and .Count is a StrictMode error
# rather than 1. The same trap get-ver.ps1 names about its asset match.
function Get-Snapshots {
    if (-not (Test-Path $backups)) {
        return @()
    }

    @(Get-ChildItem $backups -Filter 'data-*.zip' -File | Sort-Object LastWriteTime)
}

if (-not (Test-Path $data)) {
    Write-Error "There is no data folder at $data. Nothing to back up."
}

# ---------------------------------------------------------------- list

if ($List) {
    $held = @(Get-Snapshots)

    if ($held.Count -eq 0) {
        Write-Step "No snapshots yet in $backups"
        return
    }

    Write-Step "$($held.Count) snapshot(s), oldest first"

    foreach ($snapshot in $held) {
        Write-Host ('    {0,-44} {1,9:N0} KB  {2}' -f
            $snapshot.Name,
            ($snapshot.Length / 1KB),
            $snapshot.LastWriteTime.ToString('yyyy-MM-dd HH:mm'))
    }

    Write-Note "tools/data-backup.ps1 -Restore latest"
    return
}

# ---------------------------------------------------------------- restore

if ($Restore) {
    $held = @(Get-Snapshots)

    $wanted = if ($Restore -eq 'latest') {
        $held | Select-Object -Last 1
    }
    else {
        $held | Where-Object { $_.Name -eq $Restore -or $_.BaseName -eq $Restore } | Select-Object -First 1
    }

    if (-not $wanted) {
        $names = ($held | ForEach-Object { $_.Name }) -join ', '
        Write-Error "No snapshot called '$Restore'. Held: $(if ($names) { $names } else { 'none' })"
    }

    $running = @(Get-Process d47 -ErrorAction SilentlyContinue)

    if ($running.Count -gt 0) {
        Write-Error "d47 is running (pid $($running.Id -join ', ')). Close it first — it holds these files open and would write over what is put back."
    }

    # Before, not after. Putting the wrong one back is the mistake this is most likely to be used
    # to fix, and it must not be the one thing that cannot be undone.
    # **-Protect is what stops the deepest rollback destroying itself**
    # (<https://github.com/dseelinger/d47/issues/171>). At the steady state this tool creates —
    # ten held, one per deploy — the pre-restore snapshot is an eleventh, so the trim drops the
    # oldest. When the Commander reached for the oldest, that was the file about to be read, and
    # Expand-Archive then threw on a path that no longer existed under ErrorActionPreference Stop.
    # The restore that reaches deepest was the one that self-destructed, which is the exact
    # opposite of this tool's own promise that putting the wrong one back is itself undoable.
    Write-Step 'Snapshotting what is there now, first'
    & $PSCommandPath -InstallRoot $InstallRoot -Keep $Keep -Label 'pre-restore' -Protect $wanted.FullName

    Write-Step "Restoring $($wanted.Name)"

    # Over the top rather than mirrored: a mirror is a delete pass over a folder that also holds a
    # gigabyte of models, and this is not the place for a delete pass.
    Expand-Archive -Path $wanted.FullName -DestinationPath $data -Force

    Write-Step 'Restored.'
    Write-Note 'Files the snapshot did not carry were left alone; nothing was deleted.'
    return
}

# ---------------------------------------------------------------- back up

$version = if ($Label) { $Label } else { Get-InstalledVersion }

# Sortable, and unique to the second — two installs in one minute is an ordinary afternoon here.
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$name = "data-$version-$stamp.zip"

$payload = @(Get-Payload $data)

if ($payload.Count -eq 0) {
    Write-Step 'Nothing to back up.'
    return
}

New-Item -ItemType Directory -Force -Path $backups | Out-Null

# Built in TEMP and moved in. An archive written inside the folder it is archiving is a race with
# itself: the enumeration above has already run, but the file would still be sitting in the tree
# for anything that looks again.
$staged = Join-Path ([System.IO.Path]::GetTempPath()) $name

Write-Step "Backing up data as $name"

$archive = [System.IO.Compression.ZipFile]::Open($staged, 'Create')

try {
    foreach ($file in $payload) {
        $relative = $file.FullName.Substring($data.Length).TrimStart('\').Replace('\', '/')
        $entry = $archive.CreateEntry($relative, 'Optimal')
        $entry.LastWriteTime = $file.LastWriteTime

        # FileShare.ReadWrite so a file something else has open is copied rather than refusing the
        # whole snapshot. What lands is that file as of this moment, which is what a snapshot is.
        $source = [System.IO.File]::Open(
            $file.FullName, 'Open', [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)

        try {
            $target = $entry.Open()

            try { $source.CopyTo($target) } finally { $target.Dispose() }
        }
        finally { $source.Dispose() }
    }
}
finally { $archive.Dispose() }

Move-Item -Path $staged -Destination (Join-Path $backups $name) -Force

$size = (Get-Item (Join-Path $backups $name)).Length
Write-Note "$($payload.Count) files, $('{0:N0}' -f ($size / 1KB)) KB"

if (-not $IncludeModels) {
    Write-Note 'models\, logs\, flight\ and updates\ were left out — downloaded or disposable, not Commander data.'
}

# Oldest first, so what is dropped is what is dropped.
$held = @(Get-Snapshots)
$excess = $held.Count - $Keep

# The cap still holds — what changes is which one goes. A protected snapshot is passed over and the
# next oldest is dropped in its place, so a restore does not quietly raise the ceiling either.
$droppable = @($held | Where-Object { $_.FullName -ne $Protect })

if ($excess -gt 0) {
    foreach ($old in $droppable | Select-Object -First $excess) {
        Write-Note "dropping $($old.Name)"
        Remove-Item $old.FullName -Force
    }
}

Write-Note "$([Math]::Min($held.Count, $Keep)) of $Keep kept in $backups"
