---
name: close-day-shift
description: Check that tonight's Night Shift run can do work — the task is enabled, the working tree is clean, no other session holds the checkout, there is an eligible issue, the PC will stay awake, and usage leaves budget under the pace line — and report each check as ready or blocked, with the fix. Commits nothing without Doug's word. Use when the user invokes /close-day-shift, or says "close the day shift", "am I ready for the night shift", "hand over to the night shift", "is tonight's run good to go".
---

# Close the day shift

The `night-shift` scheduled task (`C:\Users\dougs\.claude\scheduled-tasks\night-shift\SKILL.md`)
runs at about 1:14 AM local. It exits without working if any of its guards fail. This skill checks
the same guards now, while Doug can still fix them, and reports each as **ready** or **blocked**.

Read the task file first. Its guards and pace rule are the authority; this skill checks what they
say, not a copy of them.

## The checks

Run all of them, then report. Don't stop at the first blocked check.

1. **Task enabled.** `mcp__scheduled-tasks__list_scheduled_tasks`: `night-shift` is enabled. Show
   `nextRunAt` in local time. `mcp__scheduled-tasks__list_task_runs`: give the last run's status
   and summary, if there is one.
2. **Tree clean.** `git -C C:/dev/d47 status --porcelain` is empty. If not, list the files. The
   night run never starts on uncommitted work.
3. **Nothing in progress by hand.** No unfinished merge, rebase or cherry-pick
   (`.git/MERGE_HEAD`, `.git/rebase-merge`, `.git/rebase-apply`, `.git/CHERRY_PICK_HEAD`).
4. **No other session in the checkout.** `mcp__ccd_session_mgmt__list_sessions`: list every session
   other than this one whose working directory is `C:\dev\d47`, with its state. A running session
   is blocked. An idle one is ready, with a note: if it starts working again after 1 AM, the run
   skips.
5. **An issue to work.** `gh issue list --repo dseelinger/d47 --label "Night Shift" --state open
   --json number,title,labels`, dropping any issue labelled `under-speced` or `design`. Also read
   `C:\Users\dougs\.claude\night-shift\state.json` if it exists: an issue in progress counts, and
   is the one that goes first. Show the queue in the order the run will take it: the one in
   progress, then the rest, lowest number first.
6. **Budget tonight.** `mcp__ccd_session_mgmt__get_usage`. Compute the pace line at `nextRunAt`
   the way the task file says (week window from the weekly `resetsAt`, 168 h long). Show:
   - the weekly all-models % used now
   - the line at the run's start
   - the headroom (the line minus the current %)

   Headroom of zero or less is blocked. Daytime work between now and 1 AM uses up headroom, so say
   that when the headroom is small (under 3 points). If the next run falls after the weekly reset,
   usage will start from zero; say so.
7. **PC stays awake.**
   `powercfg //query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE`: an AC index of `0x00000000` means it
   never sleeps. Any other value is the sleep timeout in seconds. Blocked if the PC would sleep
   before the run and still be asleep at run time. Also say that the desktop app must be open at
   1:14 AM; if it's closed, the task runs at the next launch instead.
8. **Night commits reviewed.** `git -C C:/dev/d47 log origin/main..main --oneline`. This doesn't
   block anything, but list unpushed commits so Doug knows what's waiting for his review and push
   before more pile up. Say which ones look like night-shift work if a report names them.

## Report

A table, one row per check: the check, **ready** or **blocked**, and the detail. Under it, one line
per blocked check with the fix. End with one line: good to go, or the number of blocked checks.

Issue numbers are links: [#N](https://github.com/dseelinger/d47/issues/N). Plain statements; no
metaphor.

## Fixing

This skill reports. It changes things only when Doug says so, and then only these:

- **Dirty tree:** offer to commit the changes. Show the file list and a proposed message in the
  repository's form, with a CHANGELOG entry if the change is user-visible, and commit on his word.
  Never stash, discard or reset his work; that is his call to make himself. Never push.
- **Task disabled:** offer to enable it with `mcp__scheduled-tasks__update_scheduled_task`.
- **Empty queue:** suggest that /new-issue can file work labelled `Night Shift`, or that he can
  label an existing issue.

Everything else — sessions, sleep settings, the app being open — is Doug's to change. Say what to
change and leave it.
