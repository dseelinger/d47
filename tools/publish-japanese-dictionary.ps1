<#
.SYNOPSIS
    Publishes the IPAdic dictionary files to the GitHub release `japanese-dictionary-1`.

.DESCRIPTION
    Uploads dicrc, unk.dic, char.bin, matrix.bin and sys.dic from the MeCab.DotNet 1.2.0
    package's content\dic folder. Each file's size and SHA-256 must match the pins in
    src\D47.Core\Speech\JapaneseDictionaryAssets.cs; a mismatch stops the upload. Files
    already in the release are skipped unless -Replace is given. The release is created
    with --latest=false when missing: UpdateChecker reads the latest release.

    The release notes carry the IPAdic licence text from NOTICE, which every copy must include.

    Needs gh on PATH.

.PARAMETER Source
    The folder holding the five files. Defaults to the package in the NuGet cache.

.PARAMETER Replace
    Upload files that are already in the release.

.PARAMETER DryRun
    Verify and list the files; upload nothing.

.EXAMPLE
    tools\publish-japanese-dictionary.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string] $Source = (Join-Path $(if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }) 'mecab.dotnet\1.2.0\content\dic'),
    [switch] $Replace,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

$release = 'japanese-dictionary-1'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\D47.Core\Speech\JapaneseDictionaryAssets.cs'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'gh is not on PATH' }
if (-not (Test-Path $Source)) { throw "No dictionary folder at $Source" }

$pins = [regex]::Matches((Get-Content -Raw $assets), 'Asset\("([^"]+)", ([0-9_]+), "([0-9a-f]{64})"\)')
if ($pins.Count -ne 5) { throw "Expected five pinned files in $assets, found $($pins.Count)" }

$files = foreach ($pin in $pins) {
    $name = $pin.Groups[1].Value
    $bytes = [int64] ($pin.Groups[2].Value -replace '_', '')
    $sha256 = $pin.Groups[3].Value
    $file = Join-Path $Source $name
    if (-not (Test-Path $file)) { throw "$name is not in $Source" }
    $item = Get-Item $file
    if ($item.Length -ne $bytes) { throw "$name is $($item.Length) bytes; JapaneseDictionaryAssets says $bytes" }
    if ((Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sha256) { throw "$name does not match its sha256 in JapaneseDictionaryAssets" }
    $item
}

$ErrorActionPreference = 'Continue'
& gh release view $release 2>&1 | Out-Null
$exists = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'

$published = @{}
if ($exists) {
    foreach ($name in (& gh release view $release --json assets --jq '.assets[].name')) { $published[$name] = $true }
}

$pending = @($files | Where-Object { $Replace -or -not $published.ContainsKey($_.Name) })
$total = ($pending | Measure-Object -Property Length -Sum).Sum

$notice = Get-Content -Raw (Join-Path $root 'NOTICE')
$licence = [regex]::Match($notice, '(?s) +Copyright 2000, 2001, 2002, 2003 Nara.*?above as far as the program is concerned\.')
if (-not $licence.Success) { throw 'NOTICE does not hold the IPAdic licence text' }
$licence = ($licence.Value -split "`r?`n" | ForEach-Object { $_.Trim() }) -join "`n"

$notes = "The IPAdic dictionary, as compiled in the MeCab.DotNet 1.2.0 NuGet package, downloaded by d47 to read Japanese text as kana. Not an app version.`n`n$licence"

if ($DryRun) {
    "Dry run: nothing uploaded. $($pending.Count) of $($files.Count) file(s) would go to $release ({0:N1} MB)." -f ($total / 1MB)
}
elseif ($pending.Count -gt 0) {
    if (-not $exists) {
        & gh release create $release --latest=false --title 'Japanese dictionary' --notes $notes
        if ($LASTEXITCODE -ne 0) { throw "Could not create the release $release" }
    }
    & gh release upload $release @($pending.FullName) --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
    "Uploaded $($pending.Count) file(s) to $release."
}
else {
    'Nothing to upload.'
}
