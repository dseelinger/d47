<#
.SYNOPSIS
    Scans files and folders with Microsoft Defender and reports each detection.

.DESCRIPTION
    Updates Defender's signatures, then runs an on-demand custom scan of each path with
    remediation disabled. Exit codes: 0 every scan clean, 1 a detection, 2 a scan could not run
    (MpCmdRun missing, a path missing, or an exit code other than 0 or 2). A scan that did not run
    never reads as clean.

.EXAMPLE
    tools\scan-release.ps1 d47.zip installer\d47-setup-1.2.3.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)]
    [string[]]$Path,

    [string]$MpCmdRun = (Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $MpCmdRun -PathType Leaf)) {
    Write-Host "MpCmdRun.exe not found at $MpCmdRun; nothing was scanned." -ForegroundColor Red
    exit 2
}

Write-Host 'Updating Defender signatures...'
& $MpCmdRun -SignatureUpdate | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Signature update exited $LASTEXITCODE; scanning with the signatures already installed."
}

$detected = $false
$failed = $false

foreach ($item in $Path) {
    if (-not (Test-Path -LiteralPath $item)) {
        Write-Host "NOT SCANNED: $item does not exist." -ForegroundColor Red
        $failed = $true
        continue
    }

    $full = (Resolve-Path -LiteralPath $item).ProviderPath
    Write-Host "Scanning $full..."
    $output = @(& $MpCmdRun -Scan -ScanType 3 -File $full -DisableRemediation 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE

    if ($code -eq 0) {
        Write-Host "CLEAN: $full"
    }
    elseif ($code -eq 2) {
        $threats = @($output | ForEach-Object { if ($_ -match '^\s*Threat\s*:\s*(.+?)\s*$') { $Matches[1] } } | Select-Object -Unique)
        $name = if ($threats.Count) { $threats -join ', ' } else { 'threat name not reported' }
        Write-Host "DETECTED: $full  $name" -ForegroundColor Red
        $output | Out-Host
        $detected = $true
    }
    else {
        Write-Host "NOT SCANNED: $full  MpCmdRun exited $code." -ForegroundColor Red
        $output | Out-Host
        $failed = $true
    }
}

if ($detected) { exit 1 }
if ($failed) { exit 2 }
exit 0
