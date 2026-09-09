<#
.SYNOPSIS
    Cuts a release: runs the suite CI does not, works out the next version and asks the workflow
    to build it.

.DESCRIPTION
    Everything that matters happens in `.github/workflows/release.yml` — this decides the number,
    runs D47.App.Tests, and dispatches. The other six projects run there, before the tag exists,
    so a red run leaves nothing tagged and nothing published.

    D47.App.Tests runs here because it costs 2m52s on a four-vCPU runner and 76s on this machine,
    and it is the only project where that gap is worth anything.

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

# The workflow checks out origin/main. A green suite against anything else — uncommitted work, an
# unpushed commit, another branch — says nothing about the commit that gets built and attested.
if (git status --porcelain) {
    throw 'Working tree is not clean. The workflow builds origin/main; commit or stash first.'
}

$branch = git rev-parse --abbrev-ref HEAD
if ($branch -ne 'main') {
    throw "On '$branch'. The workflow builds main."
}

git fetch origin main --tags --quiet
if ($LASTEXITCODE -ne 0) { throw 'Could not reach origin.' }

if ((git rev-parse HEAD) -ne (git rev-parse origin/main)) {
    throw 'HEAD and origin/main differ. Push first — the workflow builds what is on the remote.'
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

Write-Host 'Running D47.App.Tests...'
dotnet test tests/D47.App.Tests -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'D47.App.Tests failed. Nothing dispatched.' }

gh workflow run release.yml -f version=$next
if ($LASTEXITCODE -ne 0) { throw 'Dispatch failed.' }

Write-Host 'Dispatched. Watch it with: gh run watch'
