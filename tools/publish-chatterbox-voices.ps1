<#
.SYNOPSIS
    Publishes the Chatterbox voice clips to the GitHub release `chatterbox-voices-1`.

.DESCRIPTION
    Uploads <id>.wav for every row of assets\voices\chatterbox\catalog.tsv from the folder
    tools\gen-chatterbox-voices.py wrote. Each clip's SHA-256 and size must match its row; a
    mismatch stops the upload. Clips already in the release are skipped unless -Replace is given.
    The release is created with --latest=false when missing: UpdateChecker reads the latest release.

    Needs gh on PATH.

.PARAMETER Source
    The folder the generator wrote the clips to.

.PARAMETER Replace
    Upload clips that are already in the release.

.PARAMETER DryRun
    Verify and list the clips; upload nothing.

.EXAMPLE
    tools\publish-chatterbox-voices.ps1 -Source C:\datasets\chatterbox-voices -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Source,
    [switch] $Replace,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

$release = 'chatterbox-voices-1'
$root = Split-Path -Parent $PSScriptRoot
$catalog = Join-Path $root 'assets\voices\chatterbox\catalog.tsv'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'gh is not on PATH' }
if (-not (Test-Path $Source)) { throw "No clip folder at $Source" }

$rows = Import-Csv -Path $catalog -Delimiter "`t"
$files = foreach ($row in $rows) {
    $file = Join-Path $Source "$($row.id).wav"
    if (-not (Test-Path $file)) { throw "$($row.id).wav is not in $Source" }
    $item = Get-Item $file
    if ($item.Length -ne [int64] $row.bytes) { throw "$($row.id).wav is $($item.Length) bytes; catalog.tsv says $($row.bytes)" }
    if ((Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $row.sha256) { throw "$($row.id).wav does not match its sha256 in catalog.tsv" }
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

if ($DryRun) {
    "Dry run: nothing uploaded. $($pending.Count) of $($files.Count) clip(s) would go to $release ({0:N1} MB)." -f ($total / 1MB)
}
elseif ($pending.Count -gt 0) {
    if (-not $exists) {
        & gh release create $release --latest=false --title 'Chatterbox voices' `
            --notes 'Chatterbox reference clips from LibriTTS-R (CC BY 4.0) downloaded by d47. Not an app version.'
        if ($LASTEXITCODE -ne 0) { throw "Could not create the release $release" }
    }
    foreach ($batch in ($pending | ForEach-Object -Begin { $i = 0 } -Process { [pscustomobject]@{ Group = [math]::Floor($i++ / 50); File = $_ } } | Group-Object Group)) {
        & gh release upload $release @($batch.Group.File.FullName) --clobber
        if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
    }
    "Uploaded $($pending.Count) clip(s) to $release."
}
else {
    'Nothing to upload.'
}
