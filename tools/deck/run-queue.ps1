# Works through the last triage's grid, one issue at a time, each in a fresh headless session.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -From 478 -BudgetUsd 15
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -StopOnFailure
#
# Each session runs /issue-worker on the model and effort triage chose, with manual testing, the
# voice and every question to the maintainer switched off. An issue that does not end in a commit
# carrying "Fixes #N" is recorded as not landed; any tracked changes it left are stashed under its
# number and the queue moves on to the next issue. -StopOnFailure stops there instead. The machine
# is kept awake until the queue ends. Nothing is pushed.

param(
    [string]$From,          # start at this issue, skipping the ones above it in the grid
    [double]$BudgetUsd = 0, # per-issue spending cap; 0 means none
    [string]$PermissionMode = 'auto',
    [switch]$StopOnFailure
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
$summary = Join-Path $logs 'summary.txt'
function Report([string]$line) {
    Write-Host $line
    Add-Content -Path $summary -Value $line
}
Report "Queue of $($numbers.Count), triage generated $($state.generated). Logs in $logs"

# ES_CONTINUOUS | ES_SYSTEM_REQUIRED: no sleep while this process runs.
Add-Type -Namespace D47 -Name Power -MemberDefinition @'
[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
'@
[void][D47.Power]::SetThreadExecutionState([uint32]2147483649)

$unattended = @'
This is an unattended overnight queue run. No one is at the keyboard, and the next issue starts
when this session exits. These instructions override the issue-worker skill where they conflict:
- Do not run /neural-voice and do not speak anything. The maintainer is asleep.
- Do not stop to ask questions. Where the issue leaves a choice, take the reading the issue text
  supports best and say which you took in the commit body.
- Ignore manual testing entirely. Do not run /test-drive, do not write manual test steps, and do
  not ask for /desktop, screenshots or a device. Manual testing never blocks a commit.
- Headless captures for your own checking are fine; skip the desktop-app check before taking them,
  do not send them, and name their paths in your final message.
- If the issue is bigger than it looked (the skill's list), or the build or filtered tests cannot
  be made green, do not commit. Leave a clear account in your final message and exit.
- Stage only files you changed. Leave untracked files you did not create alone.
- Do not push.
'@

$landed = @(); $missed = @()
try {
    foreach ($n in $numbers) {
        $entry = $state.issues.$n

        if (git log --format=%H --grep "^Fixes #$n$") {
            Report "#$n already has a Fixes commit; skipping."
            continue
        }

        $cliArgs = @('-p', "/issue-worker $n", '-n', "#$n",
                  '--model', $entry.model, '--effort', $entry.effort,
                  '--permission-mode', $PermissionMode,
                  '--append-system-prompt', $unattended)
        if ($BudgetUsd -gt 0) { $cliArgs += @('--max-budget-usd', $BudgetUsd) }

        $log = Join-Path $logs "$n.log"
        Report "$(Get-Date -Format HH:mm) #$n on $($entry.model)/$($entry.effort): $($entry.title)"
        $ErrorActionPreference = 'Continue'
        & claude @cliArgs 2>&1 | Tee-Object -FilePath $log
        $exit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'

        $fixed = git log --format=%H --grep "^Fixes #$n$"
        $dirty = git status --porcelain --untracked-files=no
        if ($dirty) {
            git stash push --message "run-queue #$n leftovers" | Out-Null
            Report "  #$n left tracked changes; stashed as 'run-queue #$n leftovers'."
        }
        if ($fixed) {
            $landed += $n
            Report "  #$n landed."
        } else {
            $missed += $n
            Report "  #$n did not land (exit $exit). See $log"
            if ($StopOnFailure) { break }
        }
    }
} finally {
    [void][D47.Power]::SetThreadExecutionState([uint32]2147483648)
    Report "$(Get-Date -Format HH:mm) Queue finished. Landed: $($landed -join ', '). Not landed: $($missed -join ', '). Nothing is pushed."
}
