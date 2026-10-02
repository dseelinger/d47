<#
.SYNOPSIS
    Publishes the core avatar clips to the GitHub release `avatars-1`.

.DESCRIPTION
    Reads design\avatars\animations\<Core>\<State>.mp4, re-encodes each clip to 128x128 H.264
    without audio, and uploads it as <core-id>.<state>.mp4. A still in place of a clip becomes a
    one-second clip. Clips already in the release are skipped unless -Replace is given. The
    release is created with --latest=false when missing: UpdateChecker reads the latest release.

    Needs ffmpeg and gh on PATH.

.PARAMETER Replace
    Upload clips that are already in the release.

.PARAMETER DryRun
    Encode and list each clip with its size; upload nothing.

.PARAMETER Source
    The animations folder. Defaults to design\avatars\animations.

.EXAMPLE
    tools\publish-avatars.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [switch] $Replace,
    [switch] $DryRun,
    [string] $Source
)

$ErrorActionPreference = 'Stop'

$release = 'avatars-1'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Source) { $Source = Join-Path $root 'design\avatars\animations' }

# Folder display name to PersonaCatalog id.
$cores = [ordered]@{
    'COVAS'         = 'covas'
    'Warden'        = 'warden'
    'Cora'          = 'cora'
    'Analyst Prime' = 'analyst-prime'
    'LLaMo'         = 'llamo'
    'Sentinel'      = 'sentinel'
    'Kex'           = 'kex'
    'Mender'        = 'mender'
    'Chart'         = 'cartographer'
    'Quartermaster' = 'quartermaster'
    'Archivist'     = 'archivist'
    'The Heretic'   = 'heretic'
    'Custom core'   = 'custom'
}

# LoopState names, lower case.
$states = 'idle', 'listening', 'transcribing', 'thinking', 'speaking', 'answered', 'unsure', 'failed'
$stillTypes = '.jpg', '.jpeg', '.png'

foreach ($tool in 'ffmpeg', 'gh') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH" }
}
if (-not (Test-Path $Source)) { throw "No animations folder at $Source" }

$work = Join-Path ([System.IO.Path]::GetTempPath()) 'd47-publish-avatars'
New-Item -ItemType Directory -Force -Path $work | Out-Null

$ErrorActionPreference = 'Continue'
& gh release view $release 2>&1 | Out-Null
$exists = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'
$published = @{}
if ($exists) {
    foreach ($name in (& gh release view $release --json assets --jq '.assets[].name')) { $published[$name] = $true }
}

$files = @()
$present = @{}

foreach ($folder in Get-ChildItem -Path $Source -Directory) {
    if (-not $cores.Contains($folder.Name)) {
        Write-Warning "Unknown core folder '$($folder.Name)'; skipped"
        continue
    }
    $id = $cores[$folder.Name]
    foreach ($file in Get-ChildItem -Path $folder.FullName -File) {
        $state = $file.BaseName.ToLowerInvariant()
        if ($states -notcontains $state) {
            Write-Warning "Unknown state '$($file.Name)' in $($folder.Name); skipped"
            continue
        }
        $asset = "$id.$state.mp4"
        $present["$id/$state"] = $true
        if ($published.ContainsKey($asset) -and -not $Replace) { continue }

        $out = Join-Path $work $asset
        $input = @('-i', $file.FullName)
        if ($stillTypes -contains $file.Extension.ToLowerInvariant()) {
            $input = @('-loop', '1', '-framerate', '24', '-t', '1', '-i', $file.FullName)
        }
        & ffmpeg -y -loglevel error @input -map '0:v:0' -an -map_metadata -1 `
            -vf 'scale=128:128,fps=24,format=yuv420p' -c:v libx264 -pix_fmt yuv420p -movflags +faststart $out
        if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed on $($file.FullName)" }
        $files += Get-Item $out
    }
}

foreach ($file in $files) { '{0}  {1:N0} bytes' -f $file.Name, $file.Length }

if ($DryRun) {
    "Dry run: nothing uploaded. $($files.Count) clip(s) in $work"
}
elseif ($files.Count -gt 0) {
    if (-not $exists) {
        & gh release create $release --latest=false --title 'Core avatars' `
            --notes 'Core avatar clips downloaded by d47. Not an app version.'
        if ($LASTEXITCODE -ne 0) { throw "Could not create the release $release" }
    }
    & gh release upload $release @($files.FullName) --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
    foreach ($file in $files) { $published[$file.Name] = $true }
    "Uploaded $($files.Count) clip(s) to $release."
}
else {
    'Nothing to upload.'
}

$table = foreach ($core in $cores.GetEnumerator()) {
    $row = [ordered]@{ Core = $core.Key }
    foreach ($state in $states) {
        $asset = "$($core.Value).$state.mp4"
        $row[$state] = if ($published.ContainsKey($asset)) { 'x' } elseif ($DryRun -and $present["$($core.Value)/$state"]) { '+' } else { '.' }
    }
    [pscustomobject]$row
}
$table | Format-Table -AutoSize | Out-String
if ($DryRun) { 'x = in the release, + = would be uploaded, . = absent' }
