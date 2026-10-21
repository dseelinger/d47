---
name: not-ready
description: List every open GitHub issue that cannot be built yet because it needs Claude Design (the `design` label) or a pass through /architect (the `needs-architect` label), with spikes in a table of their own. Reports only; labels, files and starts nothing. Use when the user invokes /not-ready, or says "what needs design", "list the design issues", "what needs the architect", "what is under-speced", "which issues need Claude Design", "what isn't ready to build".
---

# Not ready

The two labels that keep an issue out of `/triage`:

- `design` — needs Claude Design before it can be built.
- `needs-architect` — needs a pass through `/architect` before it can be built. An issue that only
  waits on other issues or on outside data does not carry it.

This skill lists the issues carrying either one, leaving out any issue labelled `tabled` or
`needs-gameplay`. It reads and reports; it does not label, comment, file or start a session.

## The query

One call. A comma inside `label:` is an OR in GitHub search; `-label:` excludes:

```bash
gh issue list --state open --limit 500 --search 'label:design,needs-architect -label:tabled -label:needs-gameplay' --json number,title,labels,author --jq '.[] | "\(.number)\t\([.labels[].name] | map(select(. == "design" or . == "needs-architect" or . == "Spike" or . == "Vetted")) | join(","))\t\(.author.login)\t\(.title)"'
```

Do not fetch issue bodies. The title and labels are the report.

## The report

Three tables, newest issue first in each:

1. **Needs Claude Design** — issues labelled `design`.
2. **Spikes** — issues labelled `Spike` and not `design`.
3. **Needs the Architect** — issues labelled `needs-architect` and neither `design` nor `Spike`.

An issue goes in the first table it qualifies for, so no issue appears twice. An issue in the first
table that also carries `needs-architect` has "also needs architect" in its notes column, and one that
also carries `Spike` has "spike".

Columns: issue, title, notes. Every issue number is a link,
`[N](https://github.com/dseelinger/d47/issues/N)`. The notes column is empty unless the issue
is in the Claude Design table and carries `needs-architect` or `Spike`, or someone other than `dseelinger` opened
it — then name the author and say whether it carries `Vetted`.

Leave out an empty table. Above the tables, one line with the count for each table. Below them,
nothing: no ranking, no recommendations, no proposed sessions. If the maintainer wants one of
these worked, that is `/architect`, on his word.

If every table is empty, say so in one line and stop.
