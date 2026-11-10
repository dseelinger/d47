# Starts every lane of the last triage at the same time, each in its own console running
# run-queue.ps1 -Lane <letter>. Each lane works its issues in order, one headless session per issue
# in the lane's folder on the issue's own branch, merges into main under the merge lock, and goes on to its next issue
# whether or not the last one landed.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-lanes.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-lanes.ps1 -BudgetUsd 15
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-lanes.ps1 -DryRun
#
# Run /triage lanes overnight first. Every issue that did not land is listed, with the session's
# final message, in needs-you.md in the run's folder under %LOCALAPPDATA%\Temp\d47-lanes, and
# latest.txt there names the newest run. Nothing is pushed.

param(
    [double]$BudgetUsd = 0,
    [string]$PermissionMode = 'auto',
    [string]$StatePath,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $StatePath) { $StatePath = Join-Path $repo '.claude\triage-state.json' }

$refusals = @()
if (git -C $repo --no-optional-locks status --porcelain --untracked-files=no) {
    $refusals += 'Tracked files in the main checkout are modified, and every lane merges into it. Commit or stash them first.'
}
if ((git -C $repo branch --show-current) -ne 'main') { $refusals += 'The main checkout is not on main.' }
if ($refusals -and -not $DryRun) { throw ($refusals -join ' ') }
$refusals | ForEach-Object { Write-Host "A real run would refuse: $_" }

$state = Get-Content $StatePath -Raw | ConvertFrom-Json
if (-not $state.lanes) { throw 'The triage state has no lanes. Run /triage lanes overnight.' }
$letters = @($state.lanes.PSObject.Properties.Name | Sort-Object)
if ($state.mode -ne 'overnight') {
    Write-Host 'This triage is not from /triage lanes overnight, so its lanes may hold issues that need the maintainer.'
}

$root = Join-Path $env:LOCALAPPDATA 'Temp\d47-lanes'
$run = Join-Path $root (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Force $run | Out-Null
Set-Content -Path (Join-Path $root 'latest.txt') -Value $run
Set-Content -Path (Join-Path $run 'needs-you.md') -Value "# Lanes run $(Split-Path $run -Leaf): issues to come back to`n"

# Sessions started from inside a Claude session would inherit its identity.
$inherited = @(Get-ChildItem env: | Where-Object { $_.Name -like 'CLAUDE*' -or $_.Name -like 'MCP_*' -or $_.Name -eq 'ANTHROPIC_BASE_URL' } |
    ForEach-Object { $_.Name })
$inherited | ForEach-Object { Remove-Item "env:$_" -ErrorAction SilentlyContinue }

$shell = (Get-Process -Id $PID).Path
foreach ($letter in $letters) {
    $queue = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'run-queue.ps1'),
               '-Lane', $letter, '-RunDir', $run, '-StatePath', $StatePath, '-PermissionMode', $PermissionMode)
    if ($BudgetUsd -gt 0) { $queue += @('-BudgetUsd', $BudgetUsd) }
    if ($DryRun) {
        $queue += '-DryRun'
        & $shell @queue
    } else {
        $issues = @($state.lanes.$letter) -join ', '
        Write-Host "Lane ${letter}: $issues"
        Start-Process -FilePath $shell -ArgumentList (@('-NoExit') + ($queue | ForEach-Object { if ("$_" -match '\s') { "`"$_`"" } else { "$_" } })) -WorkingDirectory $repo
        # Spaces out each lane's first worktree add and first full build.
        if ($letter -ne $letters[-1]) { Start-Sleep -Seconds 30 }
    }
}
Write-Host "Logs and needs-you.md in $run"
