# Works through the last triage's grid, one issue at a time, each in a fresh headless session.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -From 478 -BudgetUsd 15
#
# Each session runs /issue-worker on the model and effort triage chose. The queue stops at the first
# issue that does not end in a commit carrying "Fixes #N", or that leaves tracked files modified,
# because every later issue would be built on top of it. Nothing is pushed.

param(
    [string]$From,          # start at this issue, skipping the ones above it in the grid
    [double]$BudgetUsd = 0, # per-issue spending cap; 0 means none
    [string]$PermissionMode = 'auto'
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $repo

if (git status --porcelain --untracked-files=no) {
    throw 'Tracked files are modified. Commit or stash them before starting the queue.'
}

$state = Get-Content '.claude\triage-state.json' -Raw | ConvertFrom-Json
$numbers = @($state.issues.PSObject.Properties.Name)
if ($From) {
    $start = [array]::IndexOf($numbers, $From)
    if ($start -lt 0) { throw "Issue $From is not in the triage grid." }
    $numbers = $numbers[$start..($numbers.Count - 1)]
}

$logs = Join-Path $env:LOCALAPPDATA ("Temp\d47-queue\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $logs | Out-Null
Write-Host "Queue of $($numbers.Count), triage generated $($state.generated). Logs in $logs"

$unattended = @'
This is an unattended queue run. No one is at the keyboard, and the next issue starts when this
session exits. These instructions override the issue-worker skill where they conflict:
- Do not stop to ask questions. Where the issue leaves a choice, take the reading the issue text
  supports best and say which you took in the commit body.
- If the issue is bigger than it looked (the skill's list), or the build or tests cannot be made
  green, do not commit. Leave a clear account in your final message and exit.
- Skip /test-drive and do not ask for /desktop or for screenshots. Headless captures for your own
  checking are fine; name their paths in your final message instead of sending them.
- Write the manual test steps, if any, in your final message.
- Stage only files you changed. Leave untracked files you did not create alone.
- Do not push.
'@

foreach ($n in $numbers) {
    $entry = $state.issues.$n

    if (git log --format=%H --grep "^Fixes #$n$") {
        Write-Host "#$n already has a Fixes commit; skipping."
        continue
    }

    $cliArgs = @('-p', "/issue-worker $n", '-n', "#$n",
              '--model', $entry.model, '--effort', $entry.effort,
              '--permission-mode', $PermissionMode,
              '--append-system-prompt', $unattended)
    if ($BudgetUsd -gt 0) { $cliArgs += @('--max-budget-usd', $BudgetUsd) }

    $log = Join-Path $logs "$n.log"
    Write-Host "$(Get-Date -Format HH:mm) #$n on $($entry.model)/$($entry.effort): $($entry.title)"
    & claude @cliArgs 2>&1 | Tee-Object -FilePath $log
    $exit = $LASTEXITCODE

    $fixed = git log --format=%H --grep "^Fixes #$n$"
    $dirty = git status --porcelain --untracked-files=no
    if ($exit -ne 0 -or -not $fixed -or $dirty) {
        Write-Host "Stopped at #$n (exit $exit, committed: $([bool]$fixed), tracked changes: $([bool]$dirty)). See $log"
        exit 1
    }
}

Write-Host 'Queue finished. Nothing is pushed.'
