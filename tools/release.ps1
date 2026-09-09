<#
.SYNOPSIS
    Cuts a release: works out the next version and asks the workflow to build it.

.DESCRIPTION
    Everything that matters happens in `.github/workflows/release.yml` — this only decides the
    number and dispatches. The suite runs there, before the tag exists, so a red run leaves
    nothing tagged and nothing published.

.EXAMPLE
    tools\release.ps1 -Patch
#>
[CmdletBinding()]
param(
    [switch]$Major,
    [switch]$Minor,
    [switch]$Patch
)

$ErrorActionPreference = 'Stop'

$chosen = @($Major, $Minor, $Patch).Where({ $_ })
if ($chosen.Count -ne 1) {
    throw 'Pass exactly one of -Major, -Minor or -Patch.'
}

# --match "v*" so the ship-art-1 asset tag is not mistaken for a release.
$latest = git describe --tags --abbrev=0 --match 'v*' 2>$null
if ($LASTEXITCODE -ne 0 -or -not $latest) {
    throw 'No v* tag found. Create the first release by hand, then this takes over.'
}

if ($latest -notmatch '^v(\d+)\.(\d+)\.(\d+)$') {
    throw "Newest v* tag '$latest' is not vX.Y.Z."
}
$parts = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]

$next = if ($Major) { '{0}.0.0' -f ($parts[0] + 1) }
        elseif ($Minor) { '{0}.{1}.0' -f $parts[0], ($parts[1] + 1) }
        else { '{0}.{1}.{2}' -f $parts[0], $parts[1], ($parts[2] + 1) }

Write-Host "$latest -> v$next"
gh workflow run release.yml -f version=$next
if ($LASTEXITCODE -ne 0) { throw 'Dispatch failed.' }

Write-Host 'Dispatched. Watch it with: gh run watch'
