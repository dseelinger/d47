<#
.SYNOPSIS
    The repository's own protection rules, as code rather than as something clicked once.

.DESCRIPTION
    Two rulesets, both of which enforce something this repository already says in writing and
    currently relies on nobody getting wrong.

    **main history.** No force-push, no deletion. `main` is where every release is cut from, and a
    rewritten history means a published tag points at a commit that no longer exists.

    **released tags.** No moving a `v*` tag, no deleting one. CLAUDE.md: *"A published tag never
    moves ... it is a receipt for one exact d47.exe and the checksum beside it, and the update
    checker compares a running build's version against it. Retagging makes one version number mean
    two different binaries, which is the one thing a version number exists not to do."* That has
    been a convention enforced by care. This makes GitHub refuse it. Creating a new tag is
    untouched, which is all `release.ps1` ever does.

    **Neither ruleset has a bypass actor**, deliberately. A rule the owner can step over is a
    reminder rather than a rule, and both of these forbid things nobody has a legitimate reason to
    do — including the owner, including a script, including an agent with a shell.

    **What is deliberately NOT here: requiring a pull request on `main`.** It reads like the
    obvious first rule and it would break the release process on contact — `release.ps1` merges the
    working branch and pushes `main` directly (line 361), so a PR requirement stops a release
    rather than slowing one. Adopting it means changing how releases land, which is a decision
    about the workflow rather than about protection, and it is the Commander's to make.

    Written as a script because `.claude/settings.json` denies `gh api` to agents — it is the road
    around `tools/issues.ps1`, since an issue body is one such call away. This is the sanctioned
    road for the administrative half, and it has the better property anyway: what protects the
    repository is reviewable in the repository, and re-appliable, rather than being a thing
    somebody once set in a web form.

.PARAMETER Apply
    Create or update the rulesets. Without it this only reports.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/repo-guard.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/repo-guard.ps1 -Apply
#>

param(
    [switch] $Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Repo = 'dseelinger/d47'

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

# gh writes ordinary progress and refusals to stderr, and Windows PowerShell turns any stderr line
# from a native command into a terminating error while ErrorActionPreference is Stop. Same trap
# that broke release.ps1's -PreRelease switch on v0.78.0.
function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

# `2>&1` makes a native call emit its output a line at a time, and ConvertFrom-Json handed a line
# array parses something other than the document. Joined first, the way issues.ps1 already does.
function Invoke-GhJson {
    param([string[]] $Arguments)

    $output = (Invoke-Native { & gh @Arguments 2>&1 | ForEach-Object { "$_" } }) -join "`n"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "gh $($Arguments -join ' ') failed: $output"
    }

    return $output | ConvertFrom-Json
}

# Every ruleset on the repository, as a real array.
#
# `ConvertFrom-Json` in Windows PowerShell hands an empty JSON array back as a single object rather
# than as nothing, so `@(...)` around it produces a one-element array holding an empty array - and
# the loop below then asks that for a `.name` it has never had. Piping through ForEach-Object is
# what unrolls it, and an empty document then yields an empty array as it should.
# Returned through @(...) at every call site as well: a function that returns an empty array has it
# unrolled away to $null on the way out, and $null.Count is a terminating error under StrictMode.
function Get-Rulesets {
    return @(Invoke-GhJson @('api', "repos/$Repo/rulesets") | ForEach-Object { $_ })
}

# The two rulesets. `bypass_actors` is empty on both and should stay that way - see the note above.
$Rulesets = @(
    [ordered]@{
        name        = 'main history'
        target      = 'branch'
        enforcement = 'active'
        conditions  = [ordered]@{ ref_name = [ordered]@{ include = @('~DEFAULT_BRANCH'); exclude = @() } }
        rules       = @(
            [ordered]@{ type = 'deletion' },
            [ordered]@{ type = 'non_fast_forward' }
        )
        bypass_actors = @()
    },
    [ordered]@{
        name        = 'released tags'
        target      = 'tag'
        enforcement = 'active'
        conditions  = [ordered]@{ ref_name = [ordered]@{ include = @('refs/tags/v*'); exclude = @() } }

        # Creation is absent on purpose: cutting a new tag is the whole of what release.ps1 does.
        # What is refused is moving one and deleting one.
        rules       = @(
            [ordered]@{ type = 'deletion' },
            [ordered]@{ type = 'non_fast_forward' },
            [ordered]@{ type = 'update' }
        )
        bypass_actors = @()
    }
)

Write-Step "Reading the rulesets on $Repo"

$existing = @(Get-Rulesets)

if ($existing.Count -eq 0) {
    Write-Note 'None. Nothing is protected.'
}
else {
    foreach ($ruleset in $existing) {
        Write-Note "$($ruleset.name) [$($ruleset.target), $($ruleset.enforcement)] id=$($ruleset.id)"
    }
}

foreach ($wanted in $Rulesets) {
    $match = $existing | Where-Object { $_.name -eq $wanted.name } | Select-Object -First 1
    $verb = if ($match) { 'update' } else { 'create' }
    $rules = ($wanted.rules | ForEach-Object { $_.type }) -join ', '

    Write-Step "$verb '$($wanted.name)' [$($wanted.target)]: $rules"

    if (-not $Apply) {
        Write-Note 'Reporting only. Pass -Apply to make the change.'
        continue
    }

    $body = $wanted | ConvertTo-Json -Depth 8 -Compress
    $temp = New-TemporaryFile

    try {
        # Through a file rather than a here-string on the command line: the JSON contains braces
        # and quotes that a shell is entitled to reinterpret, and a mangled payload here would be
        # a protection rule that silently protects something else.
        # WriteAllText with a BOM-less encoder, not Set-Content -Encoding utf8: Windows PowerShell
        # writes a byte-order mark with that switch, and GitHub answers a BOM-led body with
        # "Problems parsing JSON" and a 400 that says nothing about the three bytes at the front.
        [System.IO.File]::WriteAllText($temp.FullName, $body, [System.Text.UTF8Encoding]::new($false))

        $arguments = if ($match) {
            @('api', '-X', 'PUT', "repos/$Repo/rulesets/$($match.id)", '--input', "$temp")
        }
        else {
            @('api', '-X', 'POST', "repos/$Repo/rulesets", '--input', "$temp")
        }

        $result = (Invoke-Native { & gh @arguments 2>&1 | ForEach-Object { "$_" } }) -join "`n"

        if ($LASTEXITCODE -ne 0) {
            Write-Error "GitHub refused '$($wanted.name)': $result"
        }

        Write-Note "done."
    }
    finally {
        Remove-Item $temp -ErrorAction SilentlyContinue
    }
}

if ($Apply) {
    Write-Step 'Reading them back, because "probably applied" is not applied'

    $after = @(Get-Rulesets)

    foreach ($ruleset in $after) {
        $detail = Invoke-GhJson @('api', "repos/$Repo/rulesets/$($ruleset.id)")
        $rules = ($detail.rules | ForEach-Object { $_.type }) -join ', '
        $bypass = if ($detail.bypass_actors.Count -eq 0) { 'no bypass' } else { "$($detail.bypass_actors.Count) bypass actor(s)" }

        Write-Note "$($ruleset.name) [$($ruleset.target), $($ruleset.enforcement)]: $rules - $bypass"
    }
}
