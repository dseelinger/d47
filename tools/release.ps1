<#
.SYNOPSIS
    Cuts a release: runs the suite CI does not, publishes the avatar clips, works out the next
    version and asks the workflow to build it.

.DESCRIPTION
    Everything that matters happens in `.github/workflows/release.yml` — this decides the number,
    runs the whole suite, and dispatches. The workflow runs every project except D47.App.Tests
    again, before the tag exists, so a red run leaves nothing tagged and nothing published.

    The whole suite runs here, not only D47.App.Tests: the projects run in parallel, so it takes
    no longer, and a failure is found before dispatch instead of four minutes into the run.

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

Write-Host 'Running the suite...'
dotnet test d47.slnx -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'The suite failed. Nothing dispatched.' }

# The clips are not committed, so the runner cannot publish them. Uploads only what avatars-1 lacks.
Write-Host 'Publishing avatar clips...'
& "$PSScriptRoot\publish-avatars.ps1"

# `gh workflow run` names no run, so the newest id before dispatch is what the watcher waits past.
$before = gh run list --workflow release.yml --limit 1 --json databaseId --jq '.[0].databaseId'
if ($LASTEXITCODE -ne 0 -or -not $before) { $before = 0 }

gh workflow run release.yml -f version=$next
if ($LASTEXITCODE -ne 0) { throw 'Dispatch failed.' }

Write-Host "Dispatched v$next."

# Watching is display only, and the release is already the runner's from here. A watcher that
# cannot start says so and leaves the run alone.
$watched = $false
try {
    & "$PSScriptRoot\watch-release.ps1" -After ([long]$before)
    $watchCode = $LASTEXITCODE
    $watched = $true
}
catch {
    Write-Warning "Could not follow the run: $_"
    Write-Host 'It is still going. Re-attach with: tools\watch-release.ps1'
}

if (-not $watched) { return }
if ($watchCode -ne 0) { exit $watchCode }

# The release is live. The comparison reports; it removes nothing.
Write-Host "Comparing v$next with a rebuild of its tag..."
& "$PSScriptRoot\verify-release.ps1" -Version $next
$verifyCode = $LASTEXITCODE

Write-Host "Scanning v$next with Defender..."
$scanDir = Join-Path ([IO.Path]::GetTempPath()) "d47-scan-$next"
$scanCode = 2
try {
    Remove-Item -LiteralPath $scanDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $scanDir | Out-Null
    gh release download "v$next" --pattern d47.zip --pattern 'd47-setup-*.exe' --dir $scanDir
    if ($LASTEXITCODE -eq 0) {
        & "$PSScriptRoot\scan-release.ps1" $scanDir
        $scanCode = $LASTEXITCODE
    }
    else {
        Write-Host 'Could not download the release assets to scan.' -ForegroundColor Yellow
    }
}
finally {
    Remove-Item -LiteralPath $scanDir -Recurse -Force -ErrorAction SilentlyContinue
}

if ($scanCode -eq 1) {
    Write-Host ''
    Write-Host '############################################################' -ForegroundColor Red
    Write-Host "DEFENDER FLAGGED A FILE IN v$next. THE RELEASE IS LIVE." -ForegroundColor Red
    Write-Host 'The file and threat are listed above. Submit the file to Microsoft as a false positive:' -ForegroundColor Red
    Write-Host '  https://www.microsoft.com/en-us/wdsi/filesubmission' -ForegroundColor Red
    Write-Host '############################################################' -ForegroundColor Red
}
elseif ($scanCode -ne 0) {
    Write-Host "The Defender scan of v$next did not run (exit $scanCode); the release itself succeeded." -ForegroundColor Yellow
}

if ($verifyCode -eq 1) {
    Write-Host ''
    Write-Host '############################################################' -ForegroundColor Red
    Write-Host "v$next IS LIVE AND DOES NOT MATCH A REBUILD OF ITS TAG" -ForegroundColor Red
    Write-Host 'The differing entries are listed above. To take it out of releases/latest:' -ForegroundColor Red
    Write-Host "  gh release edit v$next --draft=true" -ForegroundColor Red
    Write-Host '############################################################' -ForegroundColor Red
    exit 4
}
if ($verifyCode -ne 0) {
    Write-Host "The comparison of v$next did not run (exit $verifyCode); the release itself succeeded." -ForegroundColor Yellow
}
if ($scanCode -eq 1) { exit 3 }
exit 0
