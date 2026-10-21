# Works through the last triage's grid, one issue at a time, each in a fresh headless session.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -From 478 -BudgetUsd 15
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -StopOnFailure
#
# Run /triage overnight first, then start this from a plain console, not from inside a Claude
# session.
#
# Each session runs /issue-worker on the model, effort and advisor triage chose, with manual
# testing, the voice and every question to the maintainer switched off. Issues that are closed on
# GitHub or labelled needs-repro are skipped. An issue that does not end in a commit carrying
# "Fixes #N" is recorded as not landed; any tracked changes it left are stashed under its number
# and the queue moves on to the next issue. -StopOnFailure stops there instead.
#
# When a session ends without landing and a one-line probe on the same model also fails, the
# session stopped at the usage limit. The queue then probes every -ProbeMinutes until the limit
# resets and resumes the same session, up to -MaxLimitWaits times per issue. If the limit has not
# reset after -MaxWaitHours, the queue stops. The machine is kept awake until the queue ends.
# Nothing is pushed.

param(
    [string]$From,          # start at this issue, skipping the ones above it in the grid
    [double]$BudgetUsd = 0, # per-session spending cap; 0 means none
    [string]$PermissionMode = 'auto',
    [switch]$StopOnFailure,
    [int]$ProbeMinutes = 20,
    [double]$MaxWaitHours = 6,
    [int]$MaxLimitWaits = 3
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
if ($state.mode -ne 'overnight') {
    Report 'This grid is from a plain /triage, so it may hold issues that need the maintainer. /triage overnight leaves them out.'
}

# True when a one-line session on this model answers OK without an error.
function Test-Capacity([string]$model) {
    Push-Location $env:TEMP
    $ErrorActionPreference = 'Continue'
    try {
        $out = & claude -p 'Reply with OK.' --model $model --effort low --permission-prompts none --output-format json 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { return $false }
        try { $reply = $out | ConvertFrom-Json } catch { return $false }
        return (-not $reply.is_error) -and ($reply.result -match '\bOK\b')
    } finally {
        Pop-Location
    }
}

# Probes until the model answers. False when -MaxWaitHours pass first.
function Wait-ForCapacity([string]$model) {
    $deadline = (Get-Date).AddHours($MaxWaitHours)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds ($ProbeMinutes * 60)
        if (Test-Capacity $model) { return $true }
    }
    return $false
}

# ES_CONTINUOUS | ES_SYSTEM_REQUIRED: no sleep while this process runs.
Add-Type -Namespace D47 -Name Power -MemberDefinition @'
[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
'@
[void][D47.Power]::SetThreadExecutionState([uint32]2147483649)

$unattended = @'
This is an unattended overnight queue run. No one is at the keyboard, and the next issue starts
when this session exits. These instructions override the issue-worker skill where they conflict:
- Do not run /claude-voice and do not speak anything. The maintainer is asleep.
- Do not stop to ask questions. Where the issue leaves a small choice and its text supports one
  reading, take that reading and say which you took in the commit body.
- If the issue cannot be finished without the maintainer (a reproduction, a device, the game,
  the headset, or a choice the issue text does not settle), do not commit. Say what it needs in
  your final message and exit. Needing manual testing afterwards is not a reason to stop.
- No lanes are running. Work in the main checkout on main and commit there. Do not create, enter
  or merge through a worktree, whatever `git worktree list` shows.
- Ignore manual testing entirely. Do not run /test-drive, do not write manual test steps, and do
  not ask for /desktop, screenshots or a device. Manual testing never blocks a commit.
- Headless captures for your own checking are fine; skip the desktop-app check before taking them,
  do not send them, and name their paths in your final message.
- If the issue is bigger than it looked (the skill's list), or the build or filtered tests cannot
  be made green, do not commit. Leave a clear account in your final message and exit.
- Stage only files you changed. Leave untracked files you did not create alone.
- Do not push.
'@

$landed = @(); $missed = @(); $skipped = @()
try {
    foreach ($n in $numbers) {
        $entry = $state.issues.$n

        if (git log --format=%H --grep "^Fixes #$n$") {
            Report "#$n already has a Fixes commit; skipping."
            continue
        }

        $ErrorActionPreference = 'Continue'
        $info = gh issue view $n --json state,labels 2>$null | Out-String
        $ErrorActionPreference = 'Stop'
        try { $info = $info | ConvertFrom-Json } catch { $info = $null }
        if (-not $info) {
            Report "#$n could not be read from GitHub; working it anyway."
        } elseif ($info.state -eq 'CLOSED') {
            Report "#$n is closed on GitHub; skipping."
            continue
        } elseif (@($info.labels.name) -contains 'needs-repro') {
            $skipped += $n
            Report "#$n is labelled needs-repro; skipping."
            continue
        }

        $sessionArgs = @('-n', "#$n",
                  '--model', $entry.model, '--effort', $entry.effort,
                  '--permission-mode', $PermissionMode, '--permission-prompts', 'none',
                  '--append-system-prompt', $unattended)
        $advised = ''
        if ($entry.advisor -eq 'opus') {
            $sessionArgs += @('--advisor', 'opus')
            $advised = ' with an opus advisor'
        }
        if ($BudgetUsd -gt 0) { $sessionArgs += @('--max-budget-usd', $BudgetUsd) }

        $sessionId = [guid]::NewGuid().ToString()
        $cliArgs = @('-p', "/issue-worker $n", '--session-id', $sessionId) + $sessionArgs
        $log = Join-Path $logs "$n.log"
        Report "$(Get-Date -Format HH:mm) #$n on $($entry.model)/$($entry.effort)${advised}: $($entry.title)"

        $waits = 0; $outOfCapacity = $false
        while ($true) {
            $ErrorActionPreference = 'Continue'
            & claude @cliArgs 2>&1 | Tee-Object -FilePath $log -Append
            $exit = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'

            if (git log --format=%H --grep "^Fixes #$n$") { break }
            if (Test-Capacity $entry.model) { break }
            if ($waits -ge $MaxLimitWaits) {
                Report "  #$n hit the usage limit $waits times; giving up on it."
                break
            }
            $waits++
            Report "  $(Get-Date -Format HH:mm) #$n stopped at the usage limit; probing every $ProbeMinutes min."
            if (-not (Wait-ForCapacity $entry.model)) {
                Report "  The limit did not reset within $MaxWaitHours hours; stopping the queue."
                $outOfCapacity = $true
                break
            }
            Report "  $(Get-Date -Format HH:mm) #$n resuming session $sessionId."
            $cliArgs = @('-p', "The previous turn stopped at the usage limit. Carry on with /issue-worker $n from where it stopped. If you had already decided not to commit, keep that decision, say so and exit.",
                         '--resume', $sessionId) + $sessionArgs
        }

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
            if ($StopOnFailure -or $outOfCapacity) { break }
        }
    }
} finally {
    [void][D47.Power]::SetThreadExecutionState([uint32]2147483648)
    Report "$(Get-Date -Format HH:mm) Queue finished. Landed: $($landed -join ', '). Not landed: $($missed -join ', '). Skipped as needs-repro: $($skipped -join ', '). Nothing is pushed."
}
