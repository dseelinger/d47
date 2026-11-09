<#
.SYNOPSIS
    Runs the eval cases of the project skills with `claude plugin eval` and reports each case's
    score and the cost.

.DESCRIPTION
    Runs every folder under .claude\skills that has an evals\ folder, or the one named by -Skill.
    Each case run is a paid `claude -p` child on the maintainer's credential.

    Results go to .claude\skills\<name>\evals\results\<timestamp>\, which git ignores. Exits 1 when
    any case scores below 1, or when a run fails or stops at the cost ceiling.

.PARAMETER Skill
    The one skill folder to run. Without it, every skill that has an evals\ folder runs.

.PARAMETER MaxCostUsd
    The cost ceiling for each skill, in US dollars. Defaults to 5.

.PARAMETER Baseline
    Also runs each case without the skill loaded and prints that score, for checking that a new
    case fails without the skill. Doubles the cost.

.EXAMPLE
    tools\evals\skill-evals.ps1 -Skill architect
#>
[CmdletBinding()]
param(
    [string] $Skill,
    [double] $MaxCostUsd = 5,
    [switch] $Baseline
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
while ($root -and -not (Test-Path (Join-Path $root 'd47.slnx'))) {
    $root = Split-Path -Parent $root
}
if (-not $root) {
    throw 'No d47.slnx above this script.'
}

$skillsDir = Join-Path $root '.claude\skills'
if ($Skill) {
    if (-not (Test-Path (Join-Path $skillsDir "$Skill\evals") -PathType Container)) {
        throw "No eval cases: .claude\skills\$Skill\evals does not exist."
    }
    $names = @($Skill)
}
else {
    $names = @(Get-ChildItem -Path $skillsDir -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'evals') -PathType Container } |
        ForEach-Object Name)
    if ($names.Count -eq 0) {
        throw 'No skill under .claude\skills has an evals folder.'
    }
}

$ablation = if ($Baseline) { 'with-without' } else { 'none' }
$cap = $MaxCostUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$total = 0.0
$failed = $false

Push-Location $root
try {
    foreach ($name in $names) {
        $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH-mm-ssZ')
        $outDir = Join-Path $skillsDir "$name\evals\results\$stamp"
        $json = Join-Path $outDir 'result.json'

        Write-Host "== $name"
        & claude plugin eval ".claude/skills/$name" --trust-plugin --scaffold --ablation $ablation `
            --no-publish --max-cost-usd $cap --output-dir $outDir --json $json
        $exit = $LASTEXITCODE

        if (-not (Test-Path $json)) {
            Write-Host "  no result: claude plugin eval exited $exit"
            $failed = $true
            continue
        }

        $result = Get-Content -Raw -Path $json | ConvertFrom-Json
        foreach ($case in $result.cases) {
            $line = '  {0,-24} {1:0.00}' -f $case.name, [double] $case.aggregates.score
            if ($Baseline -and $null -ne $case.aggregates.scoreWithout) {
                $line += '   without {0:0.00}' -f [double] $case.aggregates.scoreWithout
            }
            Write-Host $line
            if ([double] $case.aggregates.score -lt 1) {
                $failed = $true
                $missed = @($case.arms.with | ForEach-Object { $_.graders } |
                    Where-Object { $_.scored -and -not $_.passed } |
                    ForEach-Object name | Sort-Object -Unique)
                $errors = @($case.arms.with | Where-Object { $_.error } | ForEach-Object error)
                if ($missed.Count -gt 0) { Write-Host "    failed: $($missed -join ', ')" }
                foreach ($e in $errors) { Write-Host "    error: $e" }
            }
        }

        $cost = [double] $result.costUsd
        $total += $cost
        Write-Host ('  cost ${0:0.00}' -f $cost)
        if ($result.partial) {
            Write-Host '  stopped at the cost ceiling; results are partial'
        }
        if ($exit -ne 0) {
            $failed = $true
        }
    }
}
finally {
    Pop-Location
}

Write-Host ('Total cost ${0:0.00}' -f $total)
if ($failed) {
    exit 1
}
