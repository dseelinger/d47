<#
.SYNOPSIS
    Follows a release run and prints each step as it finishes, with how long it took.

.DESCRIPTION
    Display only. Nothing here can change what the runner does: stopping the watcher, losing the
    network or closing the terminal leaves the run going. Re-attach at any time — with -RunId for
    a particular run, with no arguments for the newest release run.

    Exits 1 if the run it watched failed, so a caller can tell.

.EXAMPLE
    tools\watch-release.ps1

.EXAMPLE
    tools\watch-release.ps1 -RunId 34413793104
#>
[CmdletBinding()]
param(
    # The run to follow. Omit to take the newest release run.
    [long]$RunId,

    # Wait for a run newer than this one to appear. release.ps1 passes the id it saw before
    # dispatching, because `gh workflow run` returns nothing that identifies the run it started.
    [long]$After,

    [ValidateRange(1, 300)]
    [int]$IntervalSeconds = 10
)

$ErrorActionPreference = 'Stop'

function Invoke-Gh {
    param([string[]]$Arguments)

    $output = & gh @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($output -join "`n") | ConvertFrom-Json
}

function Format-Span {
    param([TimeSpan]$Span)

    if ($Span.TotalSeconds -lt 60) { return '{0}s' -f [int]$Span.TotalSeconds }
    if ($Span.TotalMinutes -lt 60) { return '{0}m{1:00}s' -f [int]$Span.TotalMinutes, $Span.Seconds }
    return '{0}h{1:00}m' -f [int]$Span.TotalHours, $Span.Minutes
}

function Get-Elapsed {
    param($Step)

    if (-not $Step.started_at) { return $null }
    $from = [datetimeoffset]::Parse($Step.started_at)
    $to = if ($Step.completed_at) { [datetimeoffset]::Parse($Step.completed_at) } else { [datetimeoffset]::UtcNow }
    return $to - $from
}

# The status line is rewritten in place, so anything printed permanently has to clear it first.
$statusWidth = 0

function Clear-StatusLine {
    if ($statusWidth -gt 0) {
        Write-Host ("`r" + (' ' * $statusWidth) + "`r") -NoNewline
        $script:statusWidth = 0
    }
}

function Write-StatusLine {
    param([string]$Text)

    Clear-StatusLine
    Write-Host "`r$Text" -NoNewline
    $script:statusWidth = $Text.Length
}

function Write-Line {
    param([string]$Text, [string]$Colour)

    Clear-StatusLine
    if ($Colour) { Write-Host $Text -ForegroundColor $Colour } else { Write-Host $Text }
}

if (-not $RunId) {
    $deadline = (Get-Date).AddSeconds(120)
    while ($true) {
        $newest = Invoke-Gh @('run', 'list', '--workflow', 'release.yml', '--limit', '1', '--json', 'databaseId')
        if ($newest -and $newest.Count -gt 0 -and [long]$newest[0].databaseId -gt $After) {
            $RunId = [long]$newest[0].databaseId
            break
        }
        if ((Get-Date) -ge $deadline) { throw 'No release run appeared within two minutes.' }
        Start-Sleep -Seconds 2
    }
}

$run = Invoke-Gh @('api', "repos/{owner}/{repo}/actions/runs/$RunId")
if (-not $run) { throw "Could not read run $RunId." }

Write-Host "Watching $($run.html_url)"
Write-Host "Ctrl-C stops the watching, not the run."

$printedSteps = [System.Collections.Generic.HashSet[string]]::new()
$printedJobs = [System.Collections.Generic.HashSet[string]]::new()
$nameWidth = 46

while ($true) {
    $jobs = (Invoke-Gh @('api', "repos/{owner}/{repo}/actions/runs/$RunId/jobs?per_page=100")).jobs
    $running = $null

    foreach ($job in $jobs) {
        if ($printedJobs.Add("$($job.id)")) {
            $where = if ($job.runner_name) { $job.runner_name } else { $job.status }
            Write-Line ''
            Write-Line "$($job.name)  [$where]" 'Cyan'
        }

        foreach ($step in $job.steps) {
            if ($step.status -ne 'completed') {
                if ($step.status -eq 'in_progress') { $running = @{ Job = $job; Step = $step } }
                continue
            }
            if (-not $printedSteps.Add("$($job.id)/$($step.number)")) { continue }

            $elapsed = Get-Elapsed $step
            $duration = if ($null -eq $elapsed) { '' } else { Format-Span $elapsed }
            $name = "  $($step.name)".PadRight($nameWidth)

            switch ($step.conclusion) {
                'success'   { Write-Line "$name $duration" }
                'skipped'   { Write-Line "$name skipped" 'DarkGray' }
                default     { Write-Line "$name $duration  $($step.conclusion.ToUpperInvariant())" 'Red' }
            }
        }
    }

    $run = Invoke-Gh @('api', "repos/{owner}/{repo}/actions/runs/$RunId")
    if ($run -and $run.status -eq 'completed') { break }

    if ($running) {
        $elapsed = Format-Span (Get-Elapsed $running.Step)
        Write-StatusLine ("  $($running.Step.name)".PadRight($nameWidth) + " $elapsed...")
    }
    elseif ($run) {
        Write-StatusLine "  $($run.status)..."
    }

    Start-Sleep -Seconds $IntervalSeconds
}

Clear-StatusLine

$total = if ($run.run_started_at) { Format-Span ([datetimeoffset]::Parse($run.updated_at) - [datetimeoffset]::Parse($run.run_started_at)) } else { '' }

Write-Host ''
if ($run.conclusion -eq 'success') {
    Write-Host "$($run.conclusion) in $total" -ForegroundColor Green
    exit 0
}

Write-Host "$($run.conclusion) after $total" -ForegroundColor Red
Write-Host "Logs: gh run view $RunId --log-failed"
exit 1
