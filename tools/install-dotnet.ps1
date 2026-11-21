<#
.SYNOPSIS
    Installs the SDK a global.json names into a folder that holds that SDK and nothing else, and
    returns the folder.

.DESCRIPTION
    Every PDB records the runtime the compiler ran on, and each assembly carries its PDB's ID, so
    the same source compiled on two runtime patches gives two different binaries. release.yml and
    tools\verify-release.ps1 both build from the folder this returns, where the only runtime is the
    one the SDK ships with. The caller puts the folder first on PATH and sets DOTNET_ROOT to it.

    Throws when the folder holds any runtime other than the SDK's own.

.EXAMPLE
    $dotnet = tools\install-dotnet.ps1 -Parent $env:TEMP
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Parent,
    [string]$GlobalJson = (Join-Path (Split-Path -Parent $PSScriptRoot) 'global.json')
)

$ErrorActionPreference = 'Stop'

$sdk = (Get-Content -Raw -LiteralPath $GlobalJson | ConvertFrom-Json).sdk.version
if ($sdk -notmatch '^\d+\.\d+\.\d+$') { throw "$GlobalJson names no exact SDK version (got '$sdk')." }

$dir = Join-Path $Parent "d47-dotnet-$sdk"
$config = Join-Path $dir "sdk\$sdk\dotnet.runtimeconfig.json"
$complete = Join-Path $dir '.d47-install-complete'

# A folder without the marker is an install that was interrupted, and is replaced.
if (-not (Test-Path -LiteralPath $complete)) {
    if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
    $installer = Join-Path ([IO.Path]::GetTempPath()) "dotnet-install-$([guid]::NewGuid().ToString('N')).ps1"
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    # dotnet-install runs `tar` by name. Git's GNU tar, first on PATH under Git Bash, reads `C:` as a host.
    $pathBefore = $env:PATH
    $env:PATH = "$([Environment]::SystemDirectory);$env:PATH"
    try {
        & $installer -Version $sdk -InstallDir $dir -NoPath | Out-Host
    }
    finally {
        $env:PATH = $pathBefore
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }
    if (-not (Test-Path -LiteralPath $config)) { throw "dotnet-install did not install SDK $sdk to $dir." }
    New-Item -ItemType File -Path $complete | Out-Null
}

$runtime = (Get-Content -Raw -LiteralPath $config | ConvertFrom-Json).runtimeOptions.framework.version
$frameworks = @(Get-ChildItem -Directory -LiteralPath (Join-Path $dir 'shared'))
if ($frameworks.Name -notcontains 'Microsoft.NETCore.App') { throw "$dir holds no Microsoft.NETCore.App runtime." }
foreach ($framework in $frameworks) {
    $versions = @(Get-ChildItem -Directory -LiteralPath $framework.FullName | ForEach-Object Name)
    if ($versions.Count -ne 1 -or $versions[0] -ne $runtime) {
        throw "$dir holds $($framework.Name) $($versions -join ', '); SDK $sdk ships with $runtime only."
    }
}

Write-Host "SDK $sdk on runtime $runtime in $dir"
$dir
