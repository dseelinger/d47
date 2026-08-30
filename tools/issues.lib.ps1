<#
.SYNOPSIS
    What more than one tool needs to know about issues: how to ask GitHub, whose text may be
    shown, and which issues a build worked.

.DESCRIPTION
    Dot-sourced, never run. It was all inside `issues.ps1` until 2026-08-30, when
    <https://github.com/dseelinger/d47/issues/207> needed the same trust rule at publish time —
    and a control copied into a second file is two controls that will disagree, which is the one
    thing a trust root must not become.

    Three things live here and they are one subject:

    - **`Invoke-Gh`**, because a native command writing to stderr under `ErrorActionPreference =
      Stop` is a trap this repository has already paid for twice.
    - **`Resolve-Trust`**, which is the whole of "may this text be shown". One answer, whether the
      asker is an agent reading an issue or a build stamping a title into a binary.
    - **`Get-ClosedIssueNumbers`**, the `Fixes #N` extraction over a commit range — which
      `prerelease.ps1` has used to pick minor-or-patch since 2026-08-27 and which is also, exactly,
      "the issues this build worked".

.NOTES
    A dot-sourced file cannot take `param()` and mean it, so this declares no parameters and
    assigns the three constants below into the caller's scope. That is the point: one spelling of
    the vouched account, one spelling of the label, one spelling of the repository.
#>

# The one account whose words are instructions. A list rather than a constant so a second
# maintainer is a line rather than a rewrite - and it is deliberately not read from a file the
# repository ships, because a trust root that an issue could edit is not a trust root.
$Vouched = @('dseelinger')

# The only label that promotes somebody else's issue to workable, and only the Commander can apply
# it. Named here to match CLAUDE.md rather than to introduce a second spelling of the same rule.
$ReadyLabel = 'ready'

# **Named so a caller cannot take it by accident, and this is not fussiness.** PowerShell
# variable names are case-insensitive, so a script that sets `$repo` to a checkout path — which
# `get-local.ps1` does, on its first line of work — is setting *this*. Every `gh` call then went out
# with `--repo C:\dev\d47`, failed, and was caught by the fail-soft path that exists for being
# offline: ten issues stamped as unknown, in a run whose only visible symptom was a warning that
# reads exactly like a network problem (#207, found by driving it).
$IssueRepo = 'dseelinger/d47'

# gh writes ordinary progress and refusals to stderr, and Windows PowerShell turns any stderr line
# from a native command into a terminating error while ErrorActionPreference is Stop. This is the
# same trap that broke release.ps1's -PreRelease switch on v0.78.0; it is not repeated here.
function Invoke-Native {
    param([scriptblock] $Command)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try { & $Command }
    finally { $ErrorActionPreference = $previous }
}

function Invoke-Gh {
    param([string[]] $Arguments)

    $output = Invoke-Native { & gh @Arguments 2>&1 | ForEach-Object { "$_" } }
    $code = $LASTEXITCODE

    if ($code -ne 0) {
        Write-Error "gh $($Arguments -join ' ') failed with exit code ${code}: $($output -join ' ')"
    }

    return ($output -join "`n")
}

# Who most recently applied $ReadyLabel, or $null if nobody currently has. Throws when the log
# cannot be read, which the caller turns into a withholding rather than an allowance.
#
# The *events* endpoint rather than the issue's own JSON, because `gh issue view` reports which
# labels are on an issue and never who put them there - and who put them there is the entire
# question. --paginate because a long-lived issue's older events would otherwise fall off the first
# page, and --slurp to collect those pages into one array rather than several concatenated ones.
#
# The filtering is done here rather than with `--jq`, and that is not a preference. A jq filter
# needs double quotes around every string, and Windows PowerShell's legacy native-argument passing
# re-parses an argument containing them - gh saw three positional arguments and refused. No argument
# this function passes contains a quote or a space.
#
# Ordered oldest-first, so the last match wins: a label can be applied, removed and re-applied, and
# only the most recent application says anything about the issue as it stands now.
function Get-ReadyApprover {
    param([int] $Number)

    $raw = Invoke-Gh @('api', '--paginate', '--slurp', "repos/$IssueRepo/issues/$Number/events")
    $pages = $raw | ConvertFrom-Json

    # --slurp yields an array of pages; each page is an array of events. Flattened rather than
    # assumed to be one page, which is the whole reason --paginate is here.
    $events = @()
    foreach ($page in $pages) {
        if ($page -is [System.Array]) { $events += $page } else { $events += , $page }
    }

    $last = $null
    foreach ($e in $events) {
        # StrictMode makes a missing property a terminating error, and most events have no label.
        $names = $e.PSObject.Properties.Name
        if ($names -notcontains 'event') { continue }
        if ($e.event -ne 'labeled' -and $e.event -ne 'unlabeled') { continue }
        if ($names -notcontains 'label' -or $null -eq $e.label) { continue }
        if ($e.label.name -ne $ReadyLabel) { continue }
        $last = $e
    }

    if ($null -eq $last) { return $null }

    # Currently unlabelled, or an actor GitHub could not name (a deleted account). Neither is an
    # approval, and neither is an error worth failing the whole run over.
    if ($last.event -ne 'labeled') { return $null }
    if (($last.PSObject.Properties.Name) -notcontains 'actor' -or $null -eq $last.actor) { return $null }
    if ([string]::IsNullOrWhiteSpace($last.actor.login)) { return $null }

    return $last.actor.login
}

# Two keys open this door, and until 2026-08-27 only one of them was checked for who turned it
# (#94). An author is an identity GitHub assigns and cannot be forged through the API. A label was
# only ever a string until you ask who applied it - and `ready` means "approved by the maintainer",
# which is a claim about a *person*. So the label path now reads the event log and takes the actor.
#
# It was never a live hole: applying a label needs triage permission, so a member of the public
# cannot open the door they are standing at. It was an assumption about how the repository happens
# to be configured, sitting underneath a control whose whole job is not to assume - and it would
# have become real, silently, the first time a collaborator, an App or a workflow gained that scope.
#
# **It fails closed.** An event log that cannot be read withholds the issue, because a control that
# opens when it cannot check is not a control. The cost of that is one API call per issue that is
# *only* vouched by its label - never for the Commander's own, which is nearly all of them.
#
# **And it answers for a binary as well as for an agent** (#207). A local build stamps issue titles
# into itself so the badge can list what it worked; a title is attacker-controlled text whether it
# is read into a model's context or drawn in d47's own chrome, so the same door decides both.
function Resolve-Trust {
    param([string] $Author, [string[]] $Labels, [int] $Number)

    if ($Vouched -contains $Author) {
        return [pscustomobject]@{ Allowed = $true; Mark = 'yours   '; Why = "written by $Author" }
    }

    if ($Labels -notcontains $ReadyLabel) {
        return [pscustomobject]@{ Allowed = $false; Mark = ''; Why = 'not vouched' }
    }

    try {
        $approver = Get-ReadyApprover -Number $Number
    }
    catch {
        # To stderr rather than into the receipt: a control that fails closed silently is one
        # nobody can tell from a control that is working. This text is gh's own and the endpoint's,
        # never the issue's.
        Write-Warning "Could not read who applied '$ReadyLabel' to #${Number}: $($_.Exception.Message)"

        return [pscustomobject]@{
            Allowed = $false; Mark = ''
            Why     = "labelled $ReadyLabel, but who applied it could not be read"
        }
    }

    if ($null -eq $approver) {
        return [pscustomobject]@{
            Allowed = $false; Mark = ''
            Why     = "labelled $ReadyLabel, but no application of it is recorded"
        }
    }

    if ($Vouched -notcontains $approver) {
        return [pscustomobject]@{
            Allowed = $false; Mark = ''
            Why     = "labelled $ReadyLabel by $approver, who is not the Commander"
        }
    }

    return [pscustomobject]@{ Allowed = $true; Mark = 'ready   '; Why = "labelled $ReadyLabel by $approver" }
}

<#
    Which issues the commits in a range say they close, ascending and deduplicated.

    **Read out of the commits rather than out of GitHub's closed list, and that is not a
    preference.** An issue closes when the commit reaching it is pushed. `prerelease.ps1` asks this
    *before* anything is pushed — the changelog has to be written against the version first — so a
    closed-issue query can never see the issues that the release it is deciding about is the one
    closing. It said "patch" for a batch of three on 2026-08-27 and was wrong in exactly that way.

    `get-local.ps1` asks the same question for a different reason (#207): the set of issues a local
    build worked is the set its commits named, and a build for testing is a build whose badge should
    say what to test. One implementation, because two definitions of "worked in this build" would be
    free to disagree, and the version stamp already uses the same window — the newest tag to HEAD.

    **It sees only what a commit wrote down.** Work still in the tree, or committed without a
    `Fixes #N`, is invisible to this, and a caller showing the result to anybody owes them that
    sentence rather than letting an empty list read as "nothing was done".
#>
function Get-ClosedIssueNumbers {
    param([string] $Root, [string] $Range)

    $log = (Invoke-Native { git -C $Root log $Range --format=%B }) -join "`n"

    return @(
        [regex]::Matches($log, '(?i)\b(?:fixes|closes|resolves)\s+#(\d+)') |
            ForEach-Object { [int]$_.Groups[1].Value } |
            Sort-Object -Unique)
}
