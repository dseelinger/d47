<#
.SYNOPSIS
    Publishes the ambient music library to the `music-1` release of dseelinger/d47-music.

.DESCRIPTION
    Source holds one subfolder per music folder (the names in Situations.All,
    src\D47.Core\Audio\Ambience.cs), each with its tracks as *.mp3. For each subfolder this writes
    <folder>.zip with the tracks at its root, then manifest.json with one entry per folder: name,
    version, zip file name, byte size, SHA-256 and track count. Folders are listed in the order of
    -Order, then the rest in the order of Situations.All.

    A folder's version is 1 at first and rises by one when its zip's SHA-256 differs from the one in
    the manifest already in the release. Zips are written with fixed entry timestamps and sorted
    entries, so the same tracks give the same hash.

    The script prints what it will upload and asks for a yes. -WhatIf builds the zips and manifest
    in a temp folder and uploads nothing.

    Needs gh on PATH. The repository must exist; the release is created with --latest=false when missing.

.PARAMETER Source
    The folder of generated tracks, laid out as <Source>\<folder>\*.mp3.

.PARAMETER Order
    Folders to list first in the manifest, in download order.

.PARAMETER Repo
    The repository that holds the release.

.EXAMPLE
    tools\publish-music.ps1 -Source C:\dev\d47-music\library -WhatIf
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Source,
    [string[]] $Order = @('docked', 'supercruise', 'normal-space'),
    [string] $Repo = 'dseelinger/d47-music',
    [switch] $WhatIf
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

$release = 'music-1'
$limit = 2GB
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Source -PathType Container)) { throw "No music folder at $Source" }

# Situations.All: the five named folders, then the ByTrack values.
$ambience = Get-Content (Join-Path $root 'src\D47.Core\Audio\Ambience.cs') -Raw
$known = [System.Collections.Generic.List[string]]::new()
foreach ($m in [regex]::Matches($ambience, 'public const string \w+ = "([^"]+)";')) { $known.Add($m.Groups[1].Value) }
$byTrack = [regex]::Match($ambience, 'ByTrack =.*?\};', 'Singleline').Value
foreach ($m in [regex]::Matches($byTrack, '\] = (?:"([^"]+)"|\w+),')) {
    if ($m.Groups[1].Success -and -not $known.Contains($m.Groups[1].Value)) { $known.Add($m.Groups[1].Value) }
}
if ($known.Count -lt 30) { throw "Read only $($known.Count) folder names from Ambience.cs" }

$folders = @(Get-ChildItem $Source -Directory)
foreach ($f in $folders) {
    if ($f.Name -cnotin $known) { throw "$($f.Name) is not a folder in Situations.All" }
}
foreach ($name in $Order) {
    if ($name -cnotin $known) { throw "-Order names $name, which is not a folder in Situations.All" }
}
$ordered = @($Order | Where-Object { $_ -in $folders.Name }) + @($known | Where-Object { $_ -in $folders.Name -and $_ -notin $Order })
if ($ordered.Count -eq 0) { throw "No subfolders in $Source" }

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'gh is not on PATH' }

$work = Join-Path ([IO.Path]::GetTempPath()) "d47-music-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null

# The manifest already published, for the version numbers.
$previous = @{}
$ErrorActionPreference = 'Continue'
& gh release download $release --repo $Repo --pattern manifest.json --dir $work 2>&1 | Out-Null
$ErrorActionPreference = 'Stop'
$old = Join-Path $work 'manifest.json'
if ($LASTEXITCODE -eq 0 -and (Test-Path $old)) {
    foreach ($e in (Get-Content $old -Raw | ConvertFrom-Json).folders) { $previous[$e.name] = $e }
    Remove-Item $old
}

$stamp = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$entries = foreach ($name in $ordered) {
    $tracks = @(Get-ChildItem (Join-Path $Source $name) -Filter *.mp3 -File | Sort-Object Name)
    if ($tracks.Count -eq 0) { throw "$name has no .mp3 tracks" }

    $zipPath = Join-Path $work "$name.zip"
    $stream = [IO.File]::Create($zipPath)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($t in $tracks) {
                $entry = $zip.CreateEntry($t.Name, [IO.Compression.CompressionLevel]::NoCompression)
                $entry.LastWriteTime = $stamp
                $in = [IO.File]::OpenRead($t.FullName)
                $out = $entry.Open()
                try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $stream.Dispose() }

    $size = (Get-Item $zipPath).Length
    if ($size -ge $limit) { throw "$name.zip is $size bytes; a release asset must stay under 2 GiB" }
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $version = 1
    if ($previous.ContainsKey($name)) {
        $version = [int] $previous[$name].version
        if ($previous[$name].sha256 -ne $hash) { $version++ }
    }

    [ordered]@{
        name   = $name
        version = $version
        zip    = "$name.zip"
        bytes  = $size
        sha256 = $hash
        tracks = $tracks.Count
    }
}

$manifestPath = Join-Path $work 'manifest.json'
[ordered]@{ folders = @($entries) } | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding utf8

$files = @($entries | ForEach-Object { Join-Path $work $_.zip }) + $manifestPath
"Release $release of $Repo"
foreach ($e in $entries) {
    '  {0,-26} v{1,-3} {2,6} track(s) {3,10:N1} MB' -f $e.zip, $e.version, $e.tracks, ($e.bytes / 1MB)
}
"  manifest.json"

if ($WhatIf) {
    "WhatIf: nothing uploaded. Files are in $work"
    return
}

if ((Read-Host "Upload $($files.Count) file(s) to $Repo $release? (yes/no)") -ne 'yes') {
    "Not uploaded. Files are in $work"
    return
}

$ErrorActionPreference = 'Continue'
& gh release view $release --repo $Repo 2>&1 | Out-Null
$exists = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'
if (-not $exists) {
    & gh release create $release --repo $Repo --latest=false --title 'Ambient music' `
        --notes 'Ambient music generated with ACE-Step 1.5 (MIT), downloaded by d47. Not an app version.'
    if ($LASTEXITCODE -ne 0) { throw "Could not create the release $release" }
}
& gh release upload $release @files --repo $Repo --clobber
if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
"Uploaded $($files.Count) file(s) to $release."
Remove-Item $work -Recurse -Force
