<#
.SYNOPSIS
    Builds main in a worktree of its own, runs the whole suite, a style pass and the journal replay, and writes one
    Markdown report. Commits, pushes and changes nothing in the checkout it is run from.

.DESCRIPTION
    Paid providers are unreachable for the run: every D47_* switch and provider key is cleared from its environment,
    and after restore all HTTP goes to a closed local port, so a networked test fails instead of calling out.
    Skips the night when Elite Dangerous is running. A run that stops on an error still writes what it had.

.PARAMETER Root
    The folder that holds the worktree (src), each night's raw output (runs) and the reports (reports).
#>
param(
    [string] $Root = 'C:\dev\d47-nightly',
    [string] $Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string] $Journals = (Join-Path $env:USERPROFILE 'Saved Games\Frontier Developments\Elite Dangerous'),
    [string] $Corpus = 'C:\d47-corpus',
    # Run even while Elite Dangerous is open; for a run started by hand.
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
(Get-Process -Id $PID).PriorityClass = 'BelowNormal'

$stamp = Get-Date -Format 'yyyy-MM-dd'
$reports = Join-Path $Root 'reports'
$run = Join-Path $Root "runs\$stamp"
$src = Join-Path $Root 'src'
$report = Join-Path $reports "$stamp.md"
New-Item -ItemType Directory -Force $reports, $run | Out-Null
Start-Transcript -LiteralPath (Join-Path $run 'transcript.txt') -Append | Out-Null

# The newest earlier report that got as far as building: a skipped night is no baseline.
$previous = Get-ChildItem $reports -Filter '*.md' | Where-Object Name -ne "$stamp.md" | Sort-Object Name -Descending |
    Where-Object { Select-String -LiteralPath $_.FullName -Pattern '^## Build' -Quiet } | Select-Object -First 1
$earlier = if ($previous) { Get-Content -LiteralPath $previous.FullName -Raw } else { '' }
$lines = [System.Collections.Generic.List[string]]::new()

trap {
    $lines.Add('')
    $lines.Add("**Stopped on an error:** $_")
    $lines.Add("At: $($_.InvocationInfo.PositionMessage -replace '\s+', ' ')")
    $lines | Set-Content $report -Encoding utf8
    Stop-Transcript | Out-Null
    exit 1
}

function Mark([string] $line) {
    if ($earlier -and -not $earlier.Contains($line)) { "$line **(new)**" } else { $line }
}

$busy = Get-Process -Name 'EliteDangerous64' -ErrorAction SilentlyContinue
if ($busy -and -not $Force) {
    "# Nightly check, $stamp`n`nSkipped: Elite Dangerous was running." | Set-Content $report
    exit 0
}

# The run's environment: no switch that turns on a live test, no provider key.
Get-ChildItem Env: | Where-Object {
    $_.Name -like 'D47_*' -or $_.Name -like '*_API_KEY' -or $_.Name -match '^(ANTHROPIC|OPENAI|ELEVENLABS|XAI|GROQ|DEEPGRAM)'
} | ForEach-Object { Remove-Item "Env:$($_.Name)" }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# The worktree follows local main, detached, so it never moves a branch.
$gitLog = Join-Path $run 'git.txt'
if (-not (Test-Path (Join-Path $src '.git'))) {
    git -C $Repo worktree prune 2>&1 | Out-File $gitLog
    git -C $Repo -c core.longpaths=true worktree add --detach $src main 2>&1 | Out-File -Append $gitLog
    if ($LASTEXITCODE -eq 0) { git -C $src config core.longpaths true }
} else {
    git -C $src checkout --detach --force main 2>&1 | Out-File $gitLog
    git -C $src clean -fd 2>&1 | Out-File -Append $gitLog
}
if ($LASTEXITCODE -ne 0) {
    "# Nightly check, $stamp`n`nStopped: the worktree at $src could not be updated. See $gitLog." | Set-Content $report
    exit 1
}
$commit = (git -C $src log -1 --format='%h %s').Trim()
$sln = Join-Path $src 'd47.slnx'

$lines.Add("# Nightly check, $stamp")
$lines.Add('')
$lines.Add("Commit: $commit")
$lines.Add('')

# --force: a no-op restore skips the vulnerability audit.
$restoreLog = Join-Path $run 'restore.txt'
dotnet restore $sln --force --nologo 2>&1 | Out-File $restoreLog

# From here on nothing reaches the internet.
$env:HTTP_PROXY = 'http://127.0.0.1:9'
$env:HTTPS_PROXY = 'http://127.0.0.1:9'
$env:ALL_PROXY = 'http://127.0.0.1:9'
$env:NO_PROXY = 'localhost,127.0.0.1,::1'

# --- Dependencies: NU1901-NU1904 from restore, as errors under TreatWarningsAsErrors or as warnings.
$lines.Add('## Dependencies')
$lines.Add('')
$advisories = @(Select-String -LiteralPath $restoreLog -Pattern '(?:error|warning) NU190[1-4]:' | ForEach-Object {
    ($_.Line -replace '^.*?(?:error|warning) (NU190[1-4]):\s*(?:Warning As Error:\s*)?', '$1: ' -replace '\s*\[[^\]]*\]\s*$', '').Trim()
} | Sort-Object -Unique)
if ($advisories.Count -eq 0) {
    $lines.Add('No known vulnerable packages.')
} else {
    foreach ($a in $advisories) { $lines.Add((Mark "- $a")) }
}
$lines.Add('')

# --- Build
$buildLog = Join-Path $run 'build.txt'
dotnet build $sln -c Release --no-restore --nologo 2>&1 | Out-File $buildLog
$built = $LASTEXITCODE -eq 0
$lines.Add('## Build')
$lines.Add('')
if ($built) {
    $lines.Add('Release build: clean.')
} else {
    $lines.Add('Release build: **failed**. First errors:')
    $lines.Add('')
    Select-String -LiteralPath $buildLog -Pattern ': error ' | Select-Object -First 10 |
        ForEach-Object { $lines.Add('- ' + ($_.Line -replace '^\s*', '' -replace [regex]::Escape($src + '\'), '')) }
}
$lines.Add('')

# --- Suite
$lines.Add('## Test suite')
$lines.Add('')
if ($built) {
    $trx = Join-Path $run 'trx'
    if (Test-Path $trx) { Remove-Item -Recurse -Force $trx }
    dotnet test $sln -c Release --no-build --nologo --logger trx --results-directory $trx 2>&1 | Out-File (Join-Path $run 'test.txt')

    $results = foreach ($file in Get-ChildItem $trx -Filter '*.trx') {
        [xml] $doc = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($r in $doc.TestRun.Results.UnitTestResult) {
            [pscustomobject] @{
                Name     = $r.testName
                Outcome  = $r.outcome
                Seconds  = [TimeSpan]::Parse($r.duration).TotalSeconds
                Message  = if ($r.Output.ErrorInfo.Message) { ($r.Output.ErrorInfo.Message -split "`n")[0].Trim() } else { '' }
            }
        }
    }

    $passed = @($results | Where-Object Outcome -eq 'Passed').Count
    $skipped = @($results | Where-Object Outcome -eq 'NotExecuted').Count
    $failed = @($results | Where-Object Outcome -eq 'Failed')
    $lines.Add("$passed passed, $($failed.Count) failed, $skipped skipped.")
    $lines.Add('')

    if ($failed.Count -gt 0) {
        $lines.Add('### Failures')
        $lines.Add('')
        foreach ($f in $failed) {
            # Run alone: a test that passes by itself depends on shared state or order.
            $alone = Join-Path $run ('alone-' + [Math]::Abs($f.Name.GetHashCode()) + '.txt')
            dotnet test $sln -c Release --no-build --nologo --filter "FullyQualifiedName=$($f.Name)" 2>&1 | Out-File $alone
            $verdict = if ($LASTEXITCODE -eq 0) { 'passes alone: shared state or ordering' } else { 'fails alone too' }
            $lines.Add((Mark "- ``$($f.Name)``") + " ($verdict)")
            if ($f.Message) { $lines.Add("  $($f.Message)") }
        }
        $lines.Add('')
    }

    $lines.Add('### Slowest tests')
    $lines.Add('')
    $results | Sort-Object Seconds -Descending | Select-Object -First 10 |
        ForEach-Object { $lines.Add(('- {0:0.0}s ``{1}``' -f $_.Seconds, $_.Name)) }
    $lines.Add('')
} else {
    $lines.Add('Not run: the build failed.')
    $lines.Add('')
}

# --- Style pass: rules that are only suggestions in the real build, raised to warnings in this worktree only.
$lines.Add('## Style findings (report only)')
$lines.Add('')
$editorconfig = Join-Path $src '.editorconfig'
@'
root = true

[*.cs]
dotnet_diagnostic.IDE0004.severity = warning
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.IDE0051.severity = warning
dotnet_diagnostic.IDE0052.severity = warning
dotnet_diagnostic.IDE0060.severity = warning
'@ | Set-Content $editorconfig
$styleLog = Join-Path $run 'style.txt'
dotnet build $sln -c Release --no-restore --no-incremental --nologo `
    -p:TreatWarningsAsErrors=false -p:GenerateDocumentationFile=true -p:NoWarn=CS1591 2>&1 | Out-File $styleLog
if ($LASTEXITCODE -ne 0) { $lines.Add("The style build failed; see $styleLog.") }
Remove-Item $editorconfig

$names = @{ IDE0004 = 'unnecessary cast'; IDE0005 = 'unused using'; IDE0051 = 'unused private member'; IDE0052 = 'private member never read'; IDE0060 = 'unused parameter' }
$findings = Select-String -LiteralPath $styleLog -Pattern 'warning (IDE00(04|05|51|52|60)):' | ForEach-Object {
    if ($_.Line -match '^\s*(?<file>.+?)\((?<line>\d+),\d+\): warning (?<id>IDE\d+): (?<text>.+?)( \(https:[^)]*\))? \[') {
        [pscustomobject] @{
            File = $Matches.file -replace [regex]::Escape($src + '\'), ''
            Line = [int] $Matches.line
            Rule = $names[$Matches.id]
            Text = $Matches.text
        }
    }
} | Sort-Object File, Line, Rule -Unique

if (-not $findings) {
    $lines.Add('None.')
} else {
    $lines.Add('| Rule | In src | In tests |')
    $lines.Add('|---|---|---|')
    foreach ($group in $findings | Group-Object Rule | Sort-Object Count -Descending) {
        $inSrc = @($group.Group | Where-Object { $_.File -like 'src\*' }).Count
        $lines.Add("| $($group.Name) | $inSrc | $($group.Count - $inSrc) |")
    }
    $lines.Add('')

    # Unused usings are many and harmless: a count per file. Everything else is listed.
    $lines.Add('### Unused usings, files with the most')
    $lines.Add('')
    $findings | Where-Object Rule -eq 'unused using' | Group-Object File | Sort-Object Count -Descending | Select-Object -First 15 |
        ForEach-Object { $lines.Add("- $($_.Count) in $($_.Name)") }
    $lines.Add('')

    $lines.Add('### Everything else')
    $lines.Add('')
    foreach ($f in $findings | Where-Object Rule -ne 'unused using') {
        $lines.Add((Mark "- $($f.File):$($f.Line) $($f.Rule): $($f.Text)"))
    }
}
$lines.Add('')

# --- Journal replay
$lines.Add('## Journal replay')
$lines.Add('')
$folders = @()
if (Test-Path $Journals) { $folders += $Journals }
if (Test-Path $Corpus) { $folders += (Get-ChildItem $Corpus -Directory | Sort-Object Name).FullName }
$replay = Join-Path $src 'tools\D47.Replay\bin\Release\net10.0-windows\d47-replay.dll'
if ($built -and $folders.Count -gt 0 -and (Test-Path $replay)) {
    $replayReport = Join-Path $run 'replay.md'
    $previousReplay = if ($previous) { Join-Path $Root "runs\$($previous.BaseName)\replay.md" } else { '' }
    dotnet $replay --out $replayReport --previous $previousReplay @folders 2>&1 | Out-File (Join-Path $run 'replay.txt')
    $lines.Add("Folder 1 is the Saved Games journals; the rest are $Corpus in name order.")
    $lines.Add('')
    # Demote the replay's headings by one level so it sits under this section.
    Get-Content -LiteralPath $replayReport | Select-Object -Skip 1 | ForEach-Object { if ($_ -match '^#') { "#$_" } else { $_ } } |
        ForEach-Object { $lines.Add($_) }
} else {
    $lines.Add('Not run: the build failed, no journal folder was found, or this commit has no tools/D47.Replay.')
}

$lines | Set-Content $report -Encoding utf8
Write-Output "Report: $report"
Stop-Transcript | Out-Null
