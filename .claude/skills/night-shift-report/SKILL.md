---
name: night-shift-report
description: Show what the Night Shift scheduled task did — the last run by default, or the last N runs, the current week, or one date. Reads the run reports and checks each listed commit against git. Reads only; changes nothing. Use when the user invokes /night-shift-report, /night-shift-report <N|week|YYYY-MM-DD>, or says "what did the night shift do", "show last night's run", "night shift results".
---

# Night Shift report

The `night-shift` scheduled task works issues labelled **Night Shift** overnight, paced so weekly
usage never passes the share of the week elapsed. It writes one report per night to
`C:\Users\dougs\.claude\night-shift\report-YYYY-MM-DD.md`; a night can hold several `## Run` sections
when the task ran more than once. This skill shows those results in chat.

It reads and reports. It does not edit, commit, push, file or relabel anything.

## Which runs

| Argument | Runs shown |
| --- | --- |
| none | the most recent run (the last `## Run` section of the newest report) |
| a number `N` | the last N runs, across reports |
| `week` | every run since the last weekly reset — Sunday 13:00 local, or the `resetsAt` of the weekly window from `mcp__ccd_session_mgmt__get_usage` less 168 h when that can be read |
| a date `YYYY-MM-DD` | every run in that date's report |

## Check the commits

For each commit a report lists, run `git -C C:/dev/d47 log --oneline -1 <hash>` and
`git -C C:/dev/d47 branch -r --contains <hash>`. Mark each one:

- **local** — on main, not on `origin/main`: waiting for Doug's review and push
- **pushed** — on `origin/main`
- **missing** — the hash no longer resolves (amended, reset or rewritten)

## What to show

1. One line per run: date and time, weekly % at start → end against the line, and why it stopped.
2. Commits waiting for review and push, each with its issue as a link.
3. Issues in progress, each with its next step (from the report, or
   `C:\Users\dougs\.claude\night-shift\issue-<N>\notes.md`).
4. Findings and draft issues from exploratory work. Summarise long findings and give the notes path.
5. Decisions the run made that Doug should check.
6. For more than one run: totals — commits, issues finished, weekly % used by the shift.

Issue numbers are links: [#N](https://github.com/dseelinger/d47/issues/N). Plain statements; no
metaphor.

## No reports

If the folder has no reports, or none for the range asked, say so. Then load
`mcp__scheduled-tasks__list_scheduled_tasks` and `mcp__scheduled-tasks__list_task_runs` and show the
task's next run time and its last run's status and summary — a run that exited on a guard before
writing a report shows there.
