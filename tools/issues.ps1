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
    The gh plumbing and the trust rule itself live in `issues.lib.ps1`, dot-sourced below, because
    `get-local.ps1` needs the same answer to "may this text be shown" when it stamps issue titles
    into a local build (#207).

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

# The gh plumbing, the trust rule and the vouched account, from the one file that holds them
# (<https://github.com/dseelinger/d47/issues/207>). They lived here until a local build needed the
# same door at publish time to decide whose issue titles it may stamp into itself — and a trust root
# copied into a second file is two trust roots that will disagree.
. (Join-Path $PSScriptRoot 'issues.lib.ps1')

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
        $raw = Invoke-Gh @('issue', 'list', '--repo', $IssueRepo, '--state', $State,
            '--limit', '200', '--json', 'number,title,author,labels,createdAt')

        $items = $raw | ConvertFrom-Json

        if ($items.Count -eq 0) {
            Write-Host "No $State issues."
            break
        }

        $shown = 0
        $withheld = 0

        Write-Host "$($items.Count) $State issue(s) in ${IssueRepo}:`n"

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

        $raw = Invoke-Gh @('issue', 'view', "$Number", '--repo', $IssueRepo,
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
