<#
.SYNOPSIS
    Publishes the working copy of the model catalog to the `models` branch, which every install
    fetches.

.DESCRIPTION
    Runs the ModelCatalog tests against the working copy of
    src\D47.Core\Catalog\model-catalog.json and stops on any failure. Then commits that one file,
    at the root, to the `models` branch and pushes it. The current branch, the index and the
    working tree are not touched.

    Refuses a catalog whose `published` date is earlier than the one on the branch: an install
    uses the newest catalog it has, so an older date would never be used.

.PARAMETER Remote
    The remote whose `models` branch is written. Defaults to origin.

.EXAMPLE
    tools\publish-models.ps1
#>
[CmdletBinding()]
param(
    [string] $Remote = 'origin'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$branch = 'models'
$name = 'model-catalog.json'
$catalog = Join-Path $root "src\D47.Core\Catalog\$name"

& dotnet test (Join-Path $root 'tests\D47.Core.Tests') --filter 'FullyQualifiedName~ModelCatalog'
if ($LASTEXITCODE -ne 0) {
    throw 'The ModelCatalog tests failed. Nothing was pushed.'
}

$published = (Get-Content -Raw -Path $catalog | ConvertFrom-Json).published

$parent = $null
& git -C $root ls-remote --exit-code $Remote "refs/heads/$branch" | Out-Null
if ($LASTEXITCODE -eq 0) {
    & git -C $root fetch --quiet $Remote "refs/heads/$branch"
    if ($LASTEXITCODE -ne 0) { throw "Could not fetch $branch from $Remote." }
    $parent = (& git -C $root rev-parse FETCH_HEAD).Trim()

    $current = (& git -C $root show "${parent}:$name" | Out-String | ConvertFrom-Json).published
    if ([string]::CompareOrdinal($published, $current) -lt 0) {
        throw "The catalog is published $published, earlier than the $current already on $branch. Nothing was pushed."
    }
}
elseif ($LASTEXITCODE -ne 2) {
    throw "Could not read $Remote."
}

$blob = (& git -C $root hash-object -w --path=$name $catalog).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not store the catalog.' }

if ($parent -and (& git -C $root rev-parse "${parent}:$name").Trim() -eq $blob) {
    Write-Host "$branch already holds this catalog. Nothing was pushed."
    return
}

# A throwaway index, so the real one is never written.
$index = Join-Path ([System.IO.Path]::GetTempPath()) "d47-publish-models-$PID.index"
$previousIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $index
    & git -C $root read-tree --empty
    & git -C $root update-index --add --cacheinfo "100644,$blob,$name"
    $tree = (& git -C $root write-tree).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not write the tree.' }
}
finally {
    $env:GIT_INDEX_FILE = $previousIndex
    Remove-Item -Force -ErrorAction SilentlyContinue $index
}

$message = "Publish the model catalog of $published"
$commit = if ($parent) {
    (& git -C $root commit-tree $tree -p $parent -m $message).Trim()
}
else {
    (& git -C $root commit-tree $tree -m $message).Trim()
}
if ($LASTEXITCODE -ne 0) { throw 'Could not commit the catalog.' }

& git -C $root push $Remote "${commit}:refs/heads/$branch"
if ($LASTEXITCODE -ne 0) { throw "Could not push $branch to $Remote." }

Write-Host "Published the model catalog of $published to $branch ($($commit.Substring(0, 8)))."
