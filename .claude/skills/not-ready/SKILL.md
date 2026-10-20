---
name: not-ready
description: List every open GitHub issue that cannot be built yet because it needs Claude Design (the `design` label) or more specification (the `under-speced` label), in two tables. Reports only; labels, files and starts nothing. Use when the user invokes /not-ready, or says "what needs design", "list the design issues", "what is under-speced", "which issues need Claude Design", "what isn't ready to build".
---

# Not ready

The two labels that keep an issue out of `/triage`:

- `design` — needs Claude Design before it can be built.
- `under-speced` — needs more specification or missing data before it can be built.

This skill lists the issues carrying either one. It reads and reports; it does not label, comment,
file or start a session.

## The query

One call. A comma inside `label:` is an OR in GitHub search:

```bash
gh issue list --state open --limit 500 --search 'label:design,under-speced' --json number,title,labels,author --jq '.[] | "\(.number)\t\([.labels[].name] | map(select(. == "design" or . == "under-speced" or . == "Vetted")) | join(","))\t\(.author.login)\t\(.title)"'
```

Do not fetch issue bodies. The title and labels are the report.

## The report

Two tables, newest issue first in each:

1. **Needs Claude Design** — issues labelled `design`.
2. **Under-speced** — issues labelled `under-speced` and not `design`.

An issue carrying both goes in the first table with "also under-speced" in its notes column, so no
issue appears twice.

Columns: issue, title, notes. Every issue number is a link,
`[#N](https://github.com/dseelinger/d47/issues/N)`. The notes column is empty unless the issue
carries both labels, or someone other than `dseelinger` opened it — then name the author and say
whether it carries `Vetted`.

Above the tables, one line with the count for each table. Below them, nothing: no ranking, no
recommendations, no proposed sessions. If the maintainer wants one of these worked, that is
`/architect` for a `design` issue and `/new-issue` for an `under-speced` one, on his word.

If both tables are empty, say so in one line and stop.
