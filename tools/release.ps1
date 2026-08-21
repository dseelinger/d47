<#
.SYNOPSIS
    Commits what is in the working tree, merges it to main, and cuts a release.

.DESCRIPTION
    The release process is written down in CLAUDE.md and has been run by hand every time. This is
    that process, once, so it cannot be run differently twice — which is the same argument the
    build already makes: "if a workflow needs a checklist to run, fix the workflow."

    Three of the steps here are not conveniences. Each one is a rule this project has already paid
    for in a version number it could not reuse:

      * `dotnet test -c Release` runs before the tag, not after. The release workflow runs it too,
        and a failure there lands *after* the tag is published — leaving a tag with no release
        behind it, which costs a version number to correct.

      * The tag waits for CI to go green on the pushed commit. Same reason, one step earlier.
        Pushing a tag onto a commit whose build is still running is how v0.16.0 was spent.

      * A tag that already exists stops the script dead. A published tag is a receipt for one
        exact d47.exe and the checksum beside it, and the update checker compares a running
        build against it. Retagging makes one version number mean two binaries.

    The version is computed from the newest v* tag rather than read from any file: the tag is
    where the version lives, and the release workflow takes it from there too.

.PARAMETER Release
    Patch or Minor, case-insensitive.

    Minor is for a completed phase in list.md or a batch of wanted changes — anything where a
    Commander should be able to tell "there is a whole capability here now" from "some fixes
    landed". Patch is for the fixes. See CLAUDE.md.

.PARAMETER Message
    The commit message for whatever is uncommitted. Required only when there is something to
    commit; the script asks for it if it is needed and not given.

.PARAMETER Notes
    The tag's annotation — what changed, in a sentence or several. Defaults to the CHANGELOG
    heading for the version being cut, which is where that sentence already lives.

.PARAMETER Yes
    Skips the confirmation before the tag is pushed. For unattended runs; the confirmation is
    there because the tag is the one step with no way back.

.PARAMETER SkipCi
    Pushes the tag without waiting for CI. Only for a run where the CI result is already known,
    and it prints what it is skipping.

.EXAMPLE
    ./tools/release.ps1 -Release patch -Message "Remediation 14 item 1: the row layout"

.EXAMPLE
    ./tools/release.ps1 minor -Notes "Phase 35: the thing the phase does"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('Patch', 'Minor')]
    [string] $Release,

    [string] $Message,

    [string] $Notes,

    [switch] $Yes,

    [switch] $SkipCi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Remote = 'origin'
$Main = 'main'

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

# Git writes progress and hints to stderr on success, so a bare call in a script with
# ErrorActionPreference=Stop throws on commands that worked. The exit code is the truth.
# Windows PowerShell turns any stderr line from a native command into a terminating error while
# ErrorActionPreference is Stop — and git, dotnet and gh all write ordinary progress and warnings
# there. The exit code is the truth, so a native call runs with the preference relaxed and is
# checked explicitly afterwards.
function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

# No param block on purpose: a declared parameter makes the binder read `-A` in `git add -A` as
# an attempt at the parameter's own name, and the call fails before git is ever reached. $args
# takes every argument verbatim, which is the only thing wanted here.
function Invoke-Git {
    $call = $args
    $output = Invoke-Native { & git @call 2>&1 | ForEach-Object { "$_" } }
    $code = $LASTEXITCODE

    if ($code -ne 0) {
        $output | ForEach-Object { Write-Host $_ }
        throw "git $($call -join ' ') failed with exit code $code"
    }

    return $output
}

function Test-Clean {
    return [string]::IsNullOrWhiteSpace((Invoke-Git status --porcelain) -join "`n")
}

# ------------------------------------------------------------------ where we are

$root = (Invoke-Git rev-parse --show-toplevel) | Select-Object -First 1
Set-Location $root

Write-Step "Repository: $root"

$branch = (Invoke-Git rev-parse --abbrev-ref HEAD) | Select-Object -First 1

if ($branch -eq 'HEAD') {
    throw 'HEAD is detached. Check out a branch before releasing from it.'
}

Write-Note "On branch $branch"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'The GitHub CLI (gh) is not on PATH. It is needed to wait for CI before tagging.'
}

# ------------------------------------------------------------------ commit

if (Test-Clean) {
    Write-Step 'Nothing to commit'
}
else {
    Write-Step 'Committing the working tree'

    Invoke-Git status --short | ForEach-Object { Write-Note $_ }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        $Message = Read-Host 'Commit message'

        if ([string]::IsNullOrWhiteSpace($Message)) {
            throw 'A commit message is required for the changes in the working tree.'
        }
    }

    Invoke-Git add -A
    Invoke-Git commit -m $Message | Out-Null

    Write-Note (Invoke-Git log --oneline -1)
}

# ------------------------------------------------------------------ merge

if ($branch -eq $Main) {
    Write-Step "Already on $Main, nothing to merge"
}
else {
    Write-Step "Merging $branch into $Main"

    Invoke-Git checkout $Main | Out-Null

    # --no-ff, because a batch that arrived as several commits should read as one thing on main.
    # The merge commit is where the whole of it is named.
    Invoke-Git merge --no-ff $branch -m "Merge $branch" | Out-Null

    Write-Note (Invoke-Git log --oneline -1)
}

# ------------------------------------------------------------------ the version

Write-Step 'Working out the next version'

$tags = @(Invoke-Git tag --list 'v*' --sort=-v:refname)
$latest = $tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } | Select-Object -First 1

if (-not $latest) {
    $next = 'v0.1.0'
    Write-Note 'No v* tag yet, so this is the first one.'
}
else {
    $parts = $latest.TrimStart('v').Split('.')
    $major = [int] $parts[0]
    $minor = [int] $parts[1]
    $patch = [int] $parts[2]

    if ($Release -ieq 'Minor') {
        $minor += 1
        $patch = 0
    }
    else {
        $patch += 1
    }

    $next = "v$major.$minor.$patch"
    Write-Note "$latest is the newest, so a $($Release.ToLowerInvariant()) is $next"
}

# A published tag never moves. Stopping here is the whole point of checking.
if ($tags -contains $next) {
    throw "$next already exists. A published tag never moves — cut the next number instead."
}

# ------------------------------------------------------------------ the annotation

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $version = $next.TrimStart('v')
    $changelog = Join-Path $root 'CHANGELOG.md'

    if (Test-Path $changelog) {
        $heading = Select-String -Path $changelog -Pattern "^##\s+$([regex]::Escape($version))\s" |
            Select-Object -First 1
    }
    else {
        $heading = $null
    }

    if ($heading) {
        # The changelog line is the release's permanent record, so it is also the best thing to
        # put on the tag: one sentence, already written, already reviewed.
        $Notes = ($heading.Line -replace '^##\s+', '').Trim()
        Write-Note "Annotation from CHANGELOG.md: $Notes"
    }
    else {
        Write-Warning "CHANGELOG.md has no '## $version' entry. The changelog line is a release's permanent record."
        $Notes = Read-Host "Tag annotation for $next"

        if ([string]::IsNullOrWhiteSpace($Notes)) {
            throw 'A tag annotation is required.'
        }
    }
}

# ------------------------------------------------------------------ the gate

Write-Step 'dotnet test -c Release'
Write-Note 'The release workflow runs this too, and a failure there lands after the tag is published.'

Invoke-Native { & dotnet test -c Release --nologo }

if ($LASTEXITCODE -ne 0) {
    throw 'Tests failed. Nothing has been pushed.'
}

# ------------------------------------------------------------------ push

Write-Step "Pushing $Main to $Remote"

Invoke-Git push $Remote $Main | Out-Null

$head = (Invoke-Git rev-parse HEAD) | Select-Object -First 1

Write-Note "main is at $($head.Substring(0, 12))"

# ------------------------------------------------------------------ wait for CI

if ($SkipCi) {
    Write-Step 'Skipping the CI wait (-SkipCi)'
    Write-Note 'A tag pushed onto a red commit leaves a version number that cannot be reused.'
}
else {
    Write-Step 'Waiting for CI on that commit'

    $run = $null

    # The run does not exist the instant the push returns. Two minutes of looking is generous for
    # a webhook and short enough that a genuine failure to start is still noticed.
    #
    # `--commit` does the matching on GitHub's side, and that is not a style preference. Matching
    # in PowerShell went wrong twice: Windows PowerShell hands a JSON array through the pipeline
    # as one object, so `Where-Object { $_.headSha -eq $head }` compares the whole *collection* of
    # head shas against one value and lets every run through — the first cut printed ten run ids
    # and watched whichever gh took first, which happened to be the right one. Moving the match
    # into a jq expression then broke on quoting, because a jq pipe inside a double-quoted
    # PowerShell string is a pipe PowerShell reads first. A flag has neither problem.
    foreach ($attempt in 1..24) {
        $ids = Invoke-Native {
            & gh run list --branch $Main --workflow ci.yml --commit $head --limit 5 `
                --json databaseId -q '.[].databaseId'
        }

        $ids = @($ids | Where-Object { $_ -match '^\d+$' })

        if ($ids.Count -gt 0) {
            $run = $ids[0]
            break
        }

        Start-Sleep -Seconds 5
    }

    if (-not $run) {
        throw "No CI run appeared for $($head.Substring(0, 12)). Nothing has been tagged."
    }

    # One id, and it is checked rather than assumed: watching the wrong run reports a green that
    # belongs to somebody else's commit, which is the one failure this whole wait exists to stop.
    if ($run -isnot [string]) {
        throw "Expected one CI run id for $($head.Substring(0, 12)), got: $($run -join ', ')"
    }

    Write-Note "Run $run"

    Invoke-Native { & gh run watch $run --exit-status } | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw 'CI is not green. Nothing has been tagged.'
    }

    Write-Note 'CI is green.'
}

# ------------------------------------------------------------------ tag

if (-not $Yes) {
    Write-Host ''
    Write-Host "About to tag $next and push it." -ForegroundColor Yellow
    Write-Host "  $Notes" -ForegroundColor Yellow
    Write-Host 'A published tag never moves.' -ForegroundColor Yellow

    $answer = Read-Host 'Cut it? (y/N)'

    if ($answer -notmatch '^(y|yes)$') {
        Write-Step 'Stopped. main is pushed; nothing is tagged.'
        return
    }
}

Write-Step "Tagging $next"

# Signed and annotated, as every published tag of this project is.
# The annotation goes through a file rather than through -m, because Windows PowerShell
# re-parses quotes inside a native command's arguments: a CHANGELOG heading containing a
# double quote — `"Copy that"` — was split, and git read the second word as a ref and failed
# with `Failed to resolve 'that' as a valid ref`. Nothing was tagged, which is the script
# working, but it cost a run. A file has no quoting rules for PowerShell to apply.
$annotation = [System.IO.Path]::GetTempFileName()

try {
    [System.IO.File]::WriteAllText($annotation, $Notes, [System.Text.UTF8Encoding]::new($false))
    Invoke-Git tag -s $next -F $annotation | Out-Null
}
finally {
    Remove-Item $annotation -ErrorAction SilentlyContinue
}
Invoke-Git push $Remote $next | Out-Null

Write-Step "$next is pushed. The release workflow builds, checksums and publishes it."
Write-Note "gh run watch (gh run list --workflow release.yml --limit 1 --json databaseId -q '.[0].databaseId')"
