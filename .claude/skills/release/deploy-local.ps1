<#
.SYNOPSIS
    Installs the version a release is about to cut over the local install, after backing the install
    up, or puts that backup back.

.DESCRIPTION
    Deploy: computes the next version exactly as tools\release.ps1 does, publishes D47.App in Release
    stamped with it, closes the installed d47, backs up its payload and data\, mirrors the new payload
    in, runs --selftest on it and starts the app again if it was running.

    Restore: closes the installed d47, saves its data\ to a dated replaced-data folder in the backup, puts the
    backed-up payload and data\ back and starts the app again if it was running or -Start is passed.
    A failed selftest leaves the app closed, so the restore after one passes -Start when the deploy
    closed it.

    data\ is copied without the folders DataBackup.LeftOut names: downloads, recordings, logs and
    music, which no build changes.

.EXAMPLE
    pwsh -File .claude\skills\release\deploy-local.ps1 -Patch
    pwsh -File .claude\skills\release\deploy-local.ps1 -Restore
#>
[CmdletBinding()]
param(
    [switch]$Patch,
    [switch]$Minor,
    [string]$Version,
    [switch]$Restore,
    [string]$Repo,
    [string]$Installed = "$env:LOCALAPPDATA\Programs\d47",
    [string]$Backup = "$env:LOCALAPPDATA\d47-release-backup",
    [string]$Staging = "$env:LOCALAPPDATA\d47-release-build",
    [switch]$NoBuild,
    [switch]$SkipSelfTest,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'

if (-not $Repo) {
    $top = try { & git rev-parse --show-toplevel 2>$null } catch { $null }
    $Repo = if ($top -and (Test-Path (Join-Path $top 'd47.slnx'))) { $top } else { 'C:\dev\d47' }
}

$Repo = [IO.Path]::GetFullPath($Repo)
$Installed = [IO.Path]::GetFullPath($Installed)
$Backup = [IO.Path]::GetFullPath($Backup)
$Staging = [IO.Path]::GetFullPath($Staging)

# What the installer and d47.zip carry. UpdateInstaller retires anything under these folders that a
# new build does not ship, so a mirror matches what an update does.
$payload = 'd47.exe', 'runtimes', 'ships', 'portraits', 'voices'

# DataBackup.LeftOut.
$leftOut = 'audio', 'avatar-clips', 'backups', 'flight', 'logs', 'models', 'ships', 'stories'

function Get-InstalledVersion([string]$folder) {
    $exe = Join-Path $folder 'd47.exe'
    if (-not (Test-Path $exe)) { return $null }
    $product = (Get-Item $exe).VersionInfo.ProductVersion
    if ($product) { $product.Split('+')[0] } else { $null }
}

function Stop-Installed {
    $running = @(Get-Process -Name d47 -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and $_.Path.StartsWith($Installed + '\', [StringComparison]::OrdinalIgnoreCase) }
        catch { $false }
    })

    if (-not $running) { return $false }

    Write-Host "closing the installed d47 (pid $($running.Id -join ', '))"

    # Asked to close first, so it writes its data out the way it normally would.
    foreach ($process in $running) {
        try { $process.CloseMainWindow() | Out-Null } catch { }
    }

    $waited = 0
    while ($waited -lt 20 -and (Get-Process -Id $running.Id -ErrorAction SilentlyContinue)) {
        Start-Sleep -Milliseconds 500
        $waited++
    }

    $stubborn = Get-Process -Id $running.Id -ErrorAction SilentlyContinue
    if ($stubborn) {
        Write-Host 'it did not close on being asked; stopping it'
        $stubborn | Stop-Process -Force
        Start-Sleep -Seconds 2
    }

    # Windows releases the file handles a moment after the process goes.
    Start-Sleep -Milliseconds 500
    Write-Host 'closed'
    return $true
}

function Copy-Mirror([string]$from, [string]$to, [string[]]$exclude = @()) {
    $arguments = @($from, $to, '/MIR', '/XJ', '/COPY:DAT', '/DCOPY:DAT', '/R:1', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($exclude) { $arguments += @('/XD') + $exclude }
    & robocopy @arguments | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed copying $from to $to (exit $LASTEXITCODE)." }
}

function Copy-Payload([string]$from, [string]$to) {
    New-Item -ItemType Directory -Path $to -Force | Out-Null
    foreach ($entry in $payload) {
        $source = Join-Path $from $entry
        $target = Join-Path $to $entry
        if (Test-Path $source -PathType Container) {
            Copy-Mirror $source $target
        }
        else {
            Copy-Item $source $target -Force
        }
    }
}

function Start-Installed {
    $exe = Join-Path $Installed 'd47.exe'
    Start-Process -FilePath $exe -WorkingDirectory $Installed | Out-Null
    Write-Host "started $exe again"
}

if (-not (Test-Path (Join-Path $Installed 'd47.exe'))) {
    throw "No d47.exe at $Installed."
}

# A Debug build reads and writes the installed data\ from wherever it runs, so a test drive would
# write into data\ while it is copied. A new install keeps its own data\.
$others = @(Get-Process -Name d47 -ErrorAction SilentlyContinue | Where-Object {
    $path = try { $_.Path } catch { $null }
    -not ($path -and ($path.StartsWith($Installed + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $path -like '*\d47-new-install\*'))
})
if ($others) {
    $where = ($others | ForEach-Object {
        try { "pid $($_.Id) at $($_.Path)" } catch { "pid $($_.Id)" }
    }) -join '; '
    throw "Another d47 is running ($where) and may write the installed data folder. Close it, then run this again."
}

if ($Restore) {
    $saved = Join-Path $Backup 'app'
    $versionFile = Join-Path $Backup 'version.txt'
    if (-not (Test-Path (Join-Path $saved 'd47.exe')) -or -not (Test-Path $versionFile)) {
        throw "No backup at $Backup to restore."
    }

    $was = Get-InstalledVersion $Installed
    $to = (Get-Content $versionFile -Raw).Trim()
    $wasRunning = Stop-Installed

    $data = Join-Path $Installed 'data'
    $replaced = Join-Path $Backup ('replaced-data-{0:yyyyMMdd-HHmmss}-v{1}' -f (Get-Date), $was)
    Copy-Mirror $data $replaced $leftOut
    Write-Host "saved the data v$was wrote to $replaced"

    Copy-Payload $saved $Installed
    Copy-Mirror (Join-Path $Backup 'data') $data $leftOut
    Write-Host "restored v$to over v$was at $Installed"

    if ($wasRunning -or $Start) { Start-Installed }
    exit 0
}

$chosen = @($Patch, $Minor).Where({ $_ })
if ($Version) {
    if ($chosen.Count -ne 0) { throw 'Pass -Version or one of -Patch and -Minor, not both.' }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be X.Y.Z. Got '$Version'." }
    $next = $Version
}
else {
    if ($chosen.Count -ne 1) { throw 'Pass exactly one of -Patch, -Minor or -Version.' }

    # The same computation as tools\release.ps1. --match "v*" so an asset tag is not mistaken for a release.
    & git -C $Repo fetch origin main --tags --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not reach origin.' }
    $latest = & git -C $Repo describe --tags --abbrev=0 --match 'v*' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $latest) { throw 'No v* tag found.' }
    if ($latest -notmatch '^v(\d+)\.(\d+)\.(\d+)$') { throw "Newest v* tag '$latest' is not vX.Y.Z." }
    $parts = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]
    $next = if ($Minor) { '{0}.{1}.0' -f $parts[0], ($parts[1] + 1) }
            else { '{0}.{1}.{2}' -f $parts[0], $parts[1], ($parts[2] + 1) }
}

Write-Host "deploying v$next to $Installed"

if (-not $NoBuild) {
    $project = Join-Path $Repo 'src\D47.App\D47.App.csproj'
    if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }

    Write-Host "publishing D47.App (Release, $next) - a minute or two"
    $log = & dotnet publish $project -c Release "-p:Version=$next" -o $Staging --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        $log | Select-Object -Last 25 | ForEach-Object { Write-Host $_ }
        throw 'The publish failed. Nothing was installed.'
    }
}

foreach ($entry in $payload) {
    if (-not (Test-Path (Join-Path $Staging $entry))) { throw "The publish at $Staging is missing $entry." }
}

$stamped = Get-InstalledVersion $Staging
if ($stamped -ne $next) { throw "The publish at $Staging is v$stamped, not v$next." }

$current = Get-InstalledVersion $Installed
$wasRunning = Stop-Installed

# An install already carrying this version is an earlier deploy that was never restored, and the
# backup taken then holds the version before it. Overwriting it would lose that version.
$hasBackup = Test-Path (Join-Path $Backup 'version.txt')
if ($hasBackup -and $current -eq $next) {
    Write-Host "v$next is already installed; keeping the backup of v$((Get-Content (Join-Path $Backup 'version.txt') -Raw).Trim())"
}
else {
    Copy-Payload $Installed (Join-Path $Backup 'app')
    Copy-Mirror (Join-Path $Installed 'data') (Join-Path $Backup 'data') $leftOut
    Set-Content -Path (Join-Path $Backup 'version.txt') -Value $current -Encoding ascii
    Write-Host "backed up v$current to $Backup"
}

Copy-Payload $Staging $Installed
Write-Host "installed v$(Get-InstalledVersion $Installed)"

if (-not $SkipSelfTest) {
    $exe = Join-Path $Installed 'd47.exe'
    $selftest = Start-Process -FilePath $exe -ArgumentList '--selftest' -WorkingDirectory $Installed -WindowStyle Hidden -PassThru
    if (-not $selftest.WaitForExit(300000)) {
        $selftest | Stop-Process -Force
        throw 'SELFTEST: did not finish in five minutes.'
    }
    if ($selftest.ExitCode -ne 0) {
        throw "SELFTEST: exited $($selftest.ExitCode). The log is in $Installed\data\logs."
    }
    Write-Host 'selftest passed'
}

if ($wasRunning) { Start-Installed }
