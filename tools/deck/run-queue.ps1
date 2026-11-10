# Works through the last triage's grid, one issue at a time, each in a fresh headless session.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -From 478 -BudgetUsd 15
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -StopOnFailure
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\deck\run-queue.ps1 -Lane B -RunDir <folder>
#
# Run /triage overnight first, then start this from a plain console, not from inside a Claude
# session. tools\deck\run-lanes.ps1 starts one of these per lane after /triage lanes overnight.
#
# Each session runs /issue-worker on the model, effort and advisor triage chose, with manual
# testing, the voice and every question to the maintainer switched off. Issues that are closed on
# GitHub or labelled needs-repro are skipped. An issue that does not end in a commit on main
# carrying "Fixes #N" is recorded as not landed, with the session's final message, in
# needs-you.md in the log folder, and the queue moves on to the next issue. -StopOnFailure stops
# there instead.
#
# Without -Lane, sessions work in the main checkout, and tracked changes a missed issue left are
# stashed under its number. With -Lane, the queue is that lane's issues, each session works in the
# lane's folder .claude\worktrees\lane-<X> on branch issue/<N> (tools\deck\lane_folder.py switches
# it) and fast-forwards main under the merge lock. A missed issue keeps its branch for the
# maintainer to resume, its unfinished edits are stashed as "lane <X> #<N> unfinished", and the lane
# goes on to its next issue. The lane stops if the main checkout gains tracked changes, since every
# lane's merge needs it clean.
#
# When a session ends without landing and a one-line probe on the same model also fails, the
# session stopped at the usage limit. The queue then probes every -ProbeMinutes until the limit
# resets and resumes the same session, up to -MaxLimitWaits times per issue. If the limit has not
# reset after -MaxWaitHours, the queue stops. The machine is kept awake until the queue ends.
# Nothing is pushed.
#
# -DryRun prints what each issue would run and starts no session, creates no worktree and changes
# nothing in git. -StatePath reads another triage state file.

param(
    [string]$From,          # start at this issue, skipping the ones above it in the grid
    [double]$BudgetUsd = 0, # per-session spending cap; 0 means none
    [string]$PermissionMode = 'auto',
    [switch]$StopOnFailure,
    [int]$ProbeMinutes = 20,
    [double]$MaxWaitHours = 6,
    [int]$MaxLimitWaits = 3,
    [string]$Lane,          # work this lane's issues in worktrees
    [string]$RunDir,        # log folder; run-lanes.ps1 passes one folder to every lane
    [string]$StatePath,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repo
if (-not $StatePath) { $StatePath = Join-Path $repo '.claude\triage-state.json' }
$lock = Join-Path $repo '.git\d47-merge.lock'

# Tracked changes in the main checkout. --no-optional-locks keeps status from taking index.lock,
# which would make a lane's merge fail.
function Get-MainDirt { git -C $repo --no-optional-locks status --porcelain --untracked-files=no }

function Test-Landed([string]$n) { [bool](git -C $repo log main --format=%H --grep "^Fixes #$n$") }

if (Get-MainDirt) {
    if (-not $DryRun) { throw 'Tracked files are modified. Commit or stash them before starting the queue.' }
    Write-Host 'Tracked files are modified; a real run would refuse to start.'
}

$state = Get-Content $StatePath -Raw | ConvertFrom-Json
if ($Lane) {
    $Lane = $Lane.ToUpper()
    if (-not $state.lanes -or -not $state.lanes.$Lane) { throw "The triage state has no lane $Lane. Run /triage lanes overnight." }
    $numbers = @($state.lanes.$Lane | ForEach-Object { "$_" })
} else {
    $numbers = @($state.issues.PSObject.Properties.Name)
}
if ($From) {
    $start = [array]::IndexOf($numbers, $From)
    if ($start -lt 0) { throw "Issue $From is not in the queue." }
    $numbers = $numbers[$start..($numbers.Count - 1)]
}

if (-not $RunDir) { $RunDir = Join-Path $env:LOCALAPPDATA ("Temp\d47-queue\" + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$logs = $RunDir
New-Item -ItemType Directory -Force $logs | Out-Null
$name = if ($Lane) { "Lane $Lane" } else { 'Queue' }
$summary = Join-Path $logs ($(if ($Lane) { "lane-$Lane.txt" } else { 'summary.txt' }))
$needsYou = Join-Path $logs 'needs-you.md'

# Several lanes append to the same files, so a write that meets another lane's retries.
function Append([string]$path, [string]$text) {
    for ($i = 0; $i -lt 50; $i++) {
        try { [IO.File]::AppendAllText($path, $text + [Environment]::NewLine); return } catch { Start-Sleep -Milliseconds 200 }
    }
    Write-Host "Could not write to ${path}: $text"
}
function Report([string]$line) {
    $line = if ($Lane) { "[$Lane] $line" } else { $line }
    Write-Host $line
    Append $summary $line
}
function Record-Miss([string]$n, [string]$title, [string]$why, [string]$detail) {
    $where = if ($Lane) { " Branch issue/$n; any unfinished edits are stashed as 'lane $Lane #$n unfinished'." } else { '' }
    $text = "## #$n $title`n`n$name. $why$where`n"
    if ($detail) { $text += "`n" + (($detail.Trim() -split "`r?`n" | ForEach-Object { "> $_" }) -join "`n") + "`n" }
    Append $needsYou $text
}

Report "$name of $($numbers.Count), triage generated $($state.generated). Logs in $logs"
if ($state.mode -ne 'overnight') {
    Report 'This grid is not from /triage overnight, so it may hold issues that need the maintainer.'
}

# The session's final message, from --output-format json, or the last lines of its output.
function Get-FinalMessage([string]$out) {
    foreach ($line in ($out -split "`r?`n" | Where-Object { $_ -match '^\s*\{' })) {
        try { $reply = $line | ConvertFrom-Json; if ($null -ne $reply.result) { return [string]$reply.result } } catch { }
    }
    return (($out -split "`r?`n" | Select-Object -Last 15) -join "`n")
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

$laneFolder = Join-Path $PSScriptRoot 'lane_folder.py'

# Runs lane_folder.py. Returns its stdout, or $null with its stderr in $script:laneWhy.
function Invoke-LaneFolder([string[]]$laneArgs) {
    $ErrorActionPreference = 'Continue'
    $out = & python $laneFolder @laneArgs 2>&1
    $ok = $LASTEXITCODE -eq 0
    $ErrorActionPreference = 'Stop'
    $text = (@($out) | ForEach-Object { "$_" }) -join "`n"
    if ($ok) { return $text.Trim() }
    $script:laneWhy = $text.Trim()
    return $null
}

# The lane's folder on branch issue/<n>, or $null.
function Get-Worktree([string]$n) {
    return Invoke-LaneFolder @('prepare', $Lane, $n)
}

# Removes the merge lock when its owner line names this issue: the session ended holding it.
function Clear-OwnLock([string]$n) {
    $owner = Join-Path $lock 'owner'
    if ((Test-Path $owner) -and ((Get-Content $owner -Raw) -match "^issue $n,")) {
        Remove-Item -Recurse -Force $lock
        Report "  #$n ended holding the merge lock; released it."
    }
}

if (-not $DryRun) {
    # ES_CONTINUOUS | ES_SYSTEM_REQUIRED: no sleep while this process runs.
    Add-Type -Namespace D47 -Name Power -MemberDefinition @'
[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
'@
    [void][D47.Power]::SetThreadExecutionState([uint32]2147483649)
}

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

# {N}, {LANE}, {DIR}, {MAIN}, {MAINFWD} and {MISSED} are filled in per issue. It holds no double quotes:
# Windows PowerShell does not escape them in a native command's arguments.
$unattendedLane = @'
This is an unattended run of lane {LANE}. Three lanes run at the same time, each one headless
session per issue. No one is at the keyboard, and the next issue in this lane starts when this
session exits. These instructions override the issue-worker skill where they conflict:
- Do not run /claude-voice and do not speak anything.
- Do not stop to ask questions, anywhere in the skill. Each place the skill says to stop and ask,
  stop: do not commit or merge what is unfinished, say why in your final message, and exit. Your
  final message is copied into the maintainer's list of issues to come back to, so make it say
  what stopped the issue and what it needs from him.
- Where the issue leaves a small choice and its text supports one reading, take that reading and
  say which you took in the commit body.
- If the issue cannot be finished without the maintainer (a reproduction, a device, the game,
  the headset, or a choice the issue text does not settle), do not commit. Needing manual testing
  afterwards is not a reason to stop.
- This session started inside its lane's worktree, {DIR}, on branch issue/{N}.
  Work there. Do not call EnterWorktree or ExitWorktree. The main checkout is {MAIN}.
- The runner has already checked lane order. Issues in this lane that did not land in this run:
  {MISSED}. The maintainer has said to go ahead anyway, so the check at the top of the skill's
  Lanes section that the issue before this one has merged does not stop you. The Check what it
  needs first section still applies: if this issue needs one of those issues, or any issue that is
  not done, do not start; say which and exit.
- If the main checkout has uncommitted changes to tracked files, do not merge; say so and exit.
- Take the merge lock in the foreground, not with run_in_background: in a headless run the notice
  that a background command finished may never arrive. Run this with a 600000 ms timeout, and run
  it again each time it prints WAITING:

      L={MAINFWD}/.git/d47-merge.lock; end=$((SECONDS+540))
      while ! mkdir $L 2>/dev/null; do
        if find $L/owner -mmin +45 2>/dev/null | grep -q .; then echo STALE: $(cat $L/owner); exit 1; fi
        if [ $SECONDS -ge $end ]; then echo WAITING; exit 0; fi
        sleep 5
      done
      echo issue {N}, taken $(date -u +%FT%TZ) > $L/owner
      echo ACQUIRED

  After 90 minutes of WAITING, give up: say so and exit. On STALE, do not remove the lock; report
  the owner line and exit.
- Of the four merge steps, run 1, 2 and 3. Do not run step 4: nothing is pushed. After step 3,
  release the lock. The runner detaches the lane's folder and deletes the branch after this
  session exits.
- A conflict in the rebase in a file other than CHANGELOG.md: abort the rebase, release the lock,
  name the files and the commits in your final message and exit.
- Ignore manual testing entirely. Do not run /test-drive, do not write manual test steps, and do
  not ask for /desktop, screenshots or a device.
- Headless captures for your own checking are fine; skip the desktop-app check before taking them,
  do not send them, and name their paths in your final message.
- If the issue is bigger than it looked (the skill's list), or the build or filtered tests cannot
  be made green, do not commit. Leave a clear account in your final message and exit.
- Stage only files you changed. Leave untracked files you did not create alone.
- Never edit, commit or stash anything in the main checkout's working tree.
'@

$landed = @(); $missed = @(); $skipped = @()
try {
    foreach ($n in $numbers) {
        $entry = $state.issues.$n
        $title = if ($entry) { $entry.title } else { '' }

        if (Test-Landed $n) {
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
            Record-Miss $n $title 'Skipped: labelled needs-repro.' ''
            continue
        }

        $model = if ($entry -and $entry.model) { $entry.model } else { 'sonnet' }
        $effort = if ($entry -and $entry.effort) { $entry.effort } else { 'medium' }
        $sessionArgs = @('-n', $(if ($Lane) { "Lane $Lane #$n" } else { "#$n" }),
                  '--model', $model, '--effort', $effort,
                  '--permission-mode', $PermissionMode, '--permission-prompts', 'none',
                  '--output-format', 'json')
        if ($Lane) {
            $missedText = if ($missed.Count) { ($missed | ForEach-Object { "#$_" }) -join ', ' } else { 'none' }
            $prompt = $unattendedLane.Replace('{N}', $n).Replace('{LANE}', $Lane).Replace('{MAINFWD}', $repo.Replace('\', '/')).Replace('{MAIN}', $repo).Replace('{MISSED}', $missedText).Replace('{DIR}', (Join-Path $repo ".claude\worktrees\lane-$Lane"))
        } else {
            $prompt = $unattended
        }
        $sessionArgs += @('--append-system-prompt', $prompt)
        $advised = ''
        if ($entry.advisor -eq 'opus') {
            $sessionArgs += @('--advisor', 'opus')
            $advised = ' with an opus advisor'
        }
        if ($BudgetUsd -gt 0) { $sessionArgs += @('--max-budget-usd', $BudgetUsd) }

        $dir = $repo
        if ($Lane -and -not $DryRun) {
            $dir = Get-Worktree $n
            if (-not $dir) {
                $missed += $n
                Report "  #${n}: could not ready the lane's folder; moving on. $script:laneWhy"
                Record-Miss $n $title "Not started: the lane's folder could not be readied. $script:laneWhy" ''
                continue
            }
        } elseif ($Lane) {
            $dir = Join-Path $repo ".claude\worktrees\lane-$Lane"
        }

        if ($DryRun) {
            Report "Would run #$n on $model/$effort${advised} in ${dir}: $title"
            continue
        }

        $sessionId = [guid]::NewGuid().ToString()
        $cliArgs = @('-p', "/issue-worker $n", '--session-id', $sessionId) + $sessionArgs
        $log = Join-Path $logs "$n.log"
        Report "$(Get-Date -Format HH:mm) #$n on $model/$effort${advised}: $title"

        $waits = 0; $outOfCapacity = $false; $final = ''
        while ($true) {
            Push-Location $dir
            $ErrorActionPreference = 'Continue'
            $out = & claude @cliArgs 2>&1 | Out-String
            $exit = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            Pop-Location
            Append $log $out
            $final = Get-FinalMessage $out

            if (Test-Landed $n) { break }
            if (Test-Capacity $model) { break }
            if ($waits -ge $MaxLimitWaits) {
                Report "  #$n hit the usage limit $waits times; giving up on it."
                break
            }
            $waits++
            Report "  $(Get-Date -Format HH:mm) #$n stopped at the usage limit; probing every $ProbeMinutes min."
            if (-not (Wait-ForCapacity $model)) {
                Report "  The limit did not reset within $MaxWaitHours hours; stopping."
                $outOfCapacity = $true
                break
            }
            Report "  $(Get-Date -Format HH:mm) #$n resuming session $sessionId."
            $cliArgs = @('-p', "The previous turn stopped at the usage limit. Carry on with /issue-worker $n from where it stopped. If you had already decided not to commit, keep that decision, say so and exit.",
                         '--resume', $sessionId) + $sessionArgs
        }

        $fixed = Test-Landed $n
        $dirtyMain = $false
        if ($Lane) {
            Clear-OwnLock $n
            if ($fixed) {
                if ($null -eq (Invoke-LaneFolder @('done', $Lane))) { Report "  #${n}: $script:laneWhy" }
            } elseif ($null -eq (Invoke-LaneFolder @('park', $Lane, $n))) {
                Report "  #${n}: could not park the lane's folder: $script:laneWhy"
            }
            if (Get-MainDirt) {
                Start-Sleep -Seconds 30
                $dirtyMain = [bool](Get-MainDirt)
            }
        } elseif (Get-MainDirt) {
            git stash push --message "run-queue #$n leftovers" | Out-Null
            Report "  #$n left tracked changes; stashed as 'run-queue #$n leftovers'."
        }

        if ($fixed) {
            $landed += $n
            Report "  #$n landed."
        } else {
            $missed += $n
            Report "  #$n did not land (exit $exit). See $log"
            $why = if ($outOfCapacity) { 'Not landed: the usage limit did not reset.' } else { 'Not landed. The session''s final message:' }
            Record-Miss $n $title $why $final
        }
        if ($dirtyMain) {
            Report "  The main checkout has tracked changes, so no lane can merge. Stopping lane $Lane."
            Append $needsYou "## Lane $Lane stopped after #$n`n`nThe main checkout has uncommitted changes to tracked files, which blocks every lane's merge. Its later issues were not started.`n"
            break
        }
        if (-not $fixed -and ($StopOnFailure -or $outOfCapacity)) { break }
    }
} finally {
    if (-not $DryRun) { [void][D47.Power]::SetThreadExecutionState([uint32]2147483648) }
    Report "$(Get-Date -Format HH:mm) $name finished. Landed: $($landed -join ', '). Not landed: $($missed -join ', '). Skipped as needs-repro: $($skipped -join ', '). Nothing is pushed."
}
