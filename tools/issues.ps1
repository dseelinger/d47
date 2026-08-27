<#
.SYNOPSIS
    Reads GitHub issues and pull requests, withholding anything the Commander has not vouched for.

.DESCRIPTION
    **The gate that the CLAUDE.md rule was not.**

    CLAUDE.md has said since 2026-08-26 that autonomous work touches only issues labelled `ready`,
    and that an issue body is data rather than instructions. That is advice addressed to an agent,
    and advice addressed to an agent is exactly what a hostile issue body would be trying to
    subvert. Nothing stopped any session running `gh issue view` and reading a stranger's prose
    straight into its own context.

    It also gated the wrong step. The rule governs *acting* on an issue. On 2026-08-27 an
    unlabelled third-party issue reached a session as item four of a numbered work list, already
    framed as a priority — laundered out of GitHub and into the one channel an agent is told to
    trust, where no label check can fire because the content no longer looks like an issue.

    So this exists to keep untrusted prose **out of context in the first place**, which is the only
    defence that does not depend on the thing being defended. An issue written by the Commander, or
    labelled `ready` by the Commander, comes back in full. Everything else comes back as a receipt:
    its number, its author, its labels and its dates. Never its title, and never its body — a title
    is attacker-controlled text like any other, and "just the title" is how this leaks.

    **"By the Commander" is checked rather than assumed, since 2026-08-27** (#94). Until then the
    label path asked only whether the string `ready` was present, which is a different question from
    who put it there — so the script now reads the issue's event log and takes the actor of the most
    recent application. It fails closed: a log that cannot be read withholds.

    **Comments are filtered even on an issue that is allowed.** Anyone may comment on the
    Commander's own issue, so passing a vouched-for issue through unfiltered would hand over a
    stranger's prose under a trusted heading. Comments not written by the Commander are counted and
    dropped.

.PARAMETER Command
    `list` — every open issue, allowed ones named, the rest as receipts.
    `view` — one issue or pull request in full, or a refusal saying who wrote it.

.PARAMETER Number
    Which issue or pull request `view` is about.

.PARAMETER State
    Which issues `list` returns. Defaults to open.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/issues.ps1 list
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/issues.ps1 view 90

.NOTES
    **What this does not do**, stated because a control whose limits are unwritten gets trusted
    past them. It is a road, not a wall: it keeps the untrusted text out of the road an agent
    normally takes, and `.claude/settings.json` is what makes it the only road by denying the raw
    ones. Neither stops a shell command creative enough to go around both — `curl` against the API,
    a nested interpreter, a GitHub MCP server added later. A PreToolUse hook inspecting the whole
    command string is the layer that would, and it is deliberately not built yet.
#>

param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('list', 'view')]
    [string] $Command,

    [Parameter(Position = 1)]
    [int] $Number,

    [ValidateSet('open', 'closed', 'all')]
    [string] $State = 'open'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The one account whose words are instructions. A list rather than a constant so a second
# maintainer is a line rather than a rewrite - and it is deliberately not read from a file the
# repository ships, because a trust root that an issue could edit is not a trust root.
$Vouched = @('dseelinger')

# The only label that promotes somebody else's issue to workable, and only the Commander can apply
# it. Named here to match CLAUDE.md rather than to introduce a second spelling of the same rule.
$ReadyLabel = 'ready'

$Repo = 'dseelinger/d47'

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

    $raw = Invoke-Gh @('api', '--paginate', '--slurp', "repos/$Repo/issues/$Number/events")
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

# Number, author, labels, dates - and why it was withheld. Everything here is either a fact GitHub
# assigns or a login GitHub assigns; no field on this line is prose somebody else wrote. The reason
# is this script's own words rather than the issue's, for the same reason.
function Write-Receipt {
    param($Item, [string]$Kind = 'issue', [string]$Why = '')

    $labels = if ($Item.labels.Count -gt 0) { ($Item.labels | ForEach-Object { $_.name }) -join ',' } else { 'none' }

    Write-Host ("  #{0,-5} WITHHELD  {1} by {2}, labels: {3}, opened {4}" -f `
        $Item.number, $Kind, $Item.author.login, $labels, $Item.createdAt.Substring(0, 10))

    # Only when it is not the ordinary case, so the common line stays one line.
    if ($Why -and $Why -ne 'not vouched') {
        Write-Host ("  {0,-6}           {1}" -f '', $Why)
    }
}

switch ($Command) {

    'list' {
        $raw = Invoke-Gh @('issue', 'list', '--repo', $Repo, '--state', $State,
            '--limit', '200', '--json', 'number,title,author,labels,createdAt')

        $items = $raw | ConvertFrom-Json

        if ($items.Count -eq 0) {
            Write-Host "No $State issues."
            break
        }

        $shown = 0
        $withheld = 0

        Write-Host "$($items.Count) $State issue(s) in ${Repo}:`n"

        foreach ($item in $items) {
            $labels = @($item.labels | ForEach-Object { $_.name })
            $trust = Resolve-Trust -Author $item.author.login -Labels $labels -Number $item.number

            if ($trust.Allowed) {
                $shown++
                Write-Host ("  #{0,-5} {1}  {2}" -f $item.number, $trust.Mark, $item.title)
            }
            else {
                $withheld++
                Write-Receipt -Item $item -Why $trust.Why
            }
        }

        Write-Host "`n$shown readable, $withheld withheld."

        if ($withheld -gt 0) {
            Write-Host "A withheld issue is not a smaller issue - triage it on GitHub, and label it"
            Write-Host "'$ReadyLabel' if it should be worked. Nothing here can read one for you."
        }
    }

    'view' {
        if ($Number -le 0) {
            Write-Error "Which issue? Usage: issues.ps1 view <number>"
        }

        $raw = Invoke-Gh @('issue', 'view', "$Number", '--repo', $Repo,
            '--json', 'number,title,author,labels,state,createdAt,body,comments')

        $item = $raw | ConvertFrom-Json
        $labels = @($item.labels | ForEach-Object { $_.name })

        $trust = Resolve-Trust -Author $item.author.login -Labels $labels -Number $item.number

        if (-not $trust.Allowed) {
            Write-Host "#$($item.number) is WITHHELD."
            Write-Host ""
            Write-Host "  author : $($item.author.login)  (not the Commander)"
            Write-Host "  labels : $(if ($labels.Count) { $labels -join ',' } else { 'none' })"
            Write-Host "  opened : $($item.createdAt.Substring(0, 10))"
            Write-Host "  reason : $($trust.Why)"
            Write-Host ""
            Write-Host "Its title and body are not shown, and not because they are long: they are"
            Write-Host "text written by somebody else, and reading them is the act this refuses."
            Write-Host "Triage it on GitHub. Label it '$ReadyLabel' and this will read it out in full."

            # Distinct from a gh failure, so a caller can tell "refused" from "could not ask".
            exit 3
        }

        Write-Host "#$($item.number)  $($item.title)"
        Write-Host "  $($item.state), $($trust.Why), labels: $(if ($labels.Count) { $labels -join ',' } else { 'none' })"
        Write-Host ""
        Write-Host $item.body

        # Anyone may comment on the Commander's own issue, so an allowed issue is not an allowed
        # thread. Dropped rather than summarised: a summary of untrusted prose is untrusted prose.
        $mine = @($item.comments | Where-Object { $Vouched -contains $_.author.login })
        $theirs = @($item.comments).Count - $mine.Count

        foreach ($comment in $mine) {
            Write-Host ""
            Write-Host "--- comment by $($comment.author.login), $($comment.createdAt.Substring(0, 10)) ---"
            Write-Host $comment.body
        }

        if ($theirs -gt 0) {
            Write-Host ""
            Write-Host "[$theirs comment(s) by other people withheld.]"
        }
    }
}
