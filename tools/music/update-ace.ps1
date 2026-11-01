<#
Updates the ACE-Step checkout in D47_ACE_STEP when origin/main has new commits.
Pulls, runs uv sync and generates a 10-second test track. If any step fails, returns the checkout to the
commit it was on, runs uv sync again and logs the failure. Every outcome is one line in generate.log.
Always exits 0: the week's run carries on with whichever version is installed.
#>
$ErrorActionPreference = 'Continue'

$data = $env:D47_MUSIC_SOURCE
$ace = $env:D47_ACE_STEP
if (-not $data -or -not $ace) { Write-Host 'D47_MUSIC_SOURCE and D47_ACE_STEP must be set.'; exit 1 }
$logFile = Join-Path $data 'generate.log'

function Write-Log([string]$msg) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} ACE-Step update: {1}' -f (Get-Date), $msg
    Write-Host $line
    Add-Content -Path $logFile -Value $line -Encoding utf8
}

function Invoke-Step([string]$name, [scriptblock]$run) {
    $global:LASTEXITCODE = 0
    $out = & $run 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $last = ($out -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)
        return "$name failed (exit $LASTEXITCODE): $last"
    }
    return $null
}

function Get-Head { (git -C $ace rev-parse HEAD).Trim() }

$fetch = Invoke-Step 'git fetch' { git -C $ace fetch origin }
if ($fetch) { Write-Log $fetch; exit 0 }

$behind = [int](git -C $ace rev-list --count HEAD..origin/main)
$old = Get-Head
if ($behind -eq 0) { Write-Log "no update, at $($old.Substring(0, 9))"; exit 0 }

if (git -C $ace status --porcelain --untracked-files=no) {
    Write-Log "skipped, $behind new commits but the checkout has local changes; still at $($old.Substring(0, 9))"
    exit 0
}

$failure = Invoke-Step 'git pull --ff-only' { git -C $ace pull --ff-only origin main }
if (-not $failure) { Push-Location $ace; $failure = Invoke-Step 'uv sync' { uv sync }; Pop-Location }
if (-not $failure) {
    $test = Join-Path ([IO.Path]::GetTempPath()) ('ace-update-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $test | Out-Null
    $python = Join-Path $ace '.venv\Scripts\python.exe'
    $failure = Invoke-Step 'test track' { & $python (Join-Path $PSScriptRoot 'generate.py') --smoke $test }
    if (-not $failure -and -not (Get-ChildItem $test -Recurse -File -Filter *.mp3 | Where-Object Length -gt 0)) {
        $failure = 'test track failed: no audio written'
    }
    Remove-Item $test -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $failure) {
    Write-Log "updated from $($old.Substring(0, 9)) to $((Get-Head).Substring(0, 9))"
    exit 0
}

git -C $ace reset --hard $old | Out-Null
Push-Location $ace
$restore = Invoke-Step 'uv sync' { uv sync }
Pop-Location
$tail = if ($restore) { "; rollback $restore" } else { '' }
Write-Log "$failure; rolled back to $($old.Substring(0, 9)) and carried on$tail"
exit 0
