# Archive

Files that stopped being maintained but are still worth reading. Nothing here is current.

## Why these are kept rather than deleted

Two reasons, and only the second is obvious.

**They are cited by name from the code.** 382 comments under `src/` and `tests/` say
`remediation.md` and about twenty say `bugs.md`, usually with an item number. `git rm` would leave
every one of them pointing at nothing, findable only by somebody who already knew the file had
existed. Keeping the filenames under this directory means a citation still resolves to something a
`grep` can reach.

**Work here was sometimes marked as done and was not.** Reviewing a change that was supposed to
have worked is ordinary here, and the reasoning behind a fix — what was ruled out, what was only a
lead, what was measured against the journal corpus — is in these files and in nothing else. That
reasoning is the reason the repository can be argued with. It is not something to make somebody
run `git log` to find.

## What is here

| File | Was | Retired | Replaced by |
|---|---|---|---|
| [bugs.md](bugs.md) | The open-defect queue | 2026-08-24 | [Issues labelled `bug`](https://github.com/dseelinger/d47/issues?q=is%3Aissue+is%3Aopen+label%3Abug) |
| [remediation.md](remediation.md) | The current batch of wanted fixes | 2026-08-24 | [Issues](https://github.com/dseelinger/d47/issues) |

Both carry a header saying what was open when they were retired and which issue each open entry
became.

## Reading a citation

**`bugs.md`, any entry.** An entry left that file when it shipped, so a citation of a *closed*
entry never resolved against the file even when it was live — its record is the line it got in
[`CHANGELOG.md`](../../CHANGELOG.md) under the release that fixed it. Read the changelog first.
The archived copy holds what was still open on 2026-08-24.

**`remediation.md N, item M`.** The file only ever held one batch. Batch **17** is the copy
archived here; **batches 1 to 16 are in git history**, and their record is `CHANGELOG.md`. To read
an earlier batch:

```bash
git log --follow --oneline -- remediation.md
```

then check out the revision that still had it. `--follow` is what carries the log across the move
into this directory.

## What did not move

[`list.md`](../../list.md) stays where it is and stays the queue for capability work. It is not a
tracker — it is the product description, every shipped item carrying its own acceptance criteria,
and over a thousand code comments cite `list.md Phase N`. A phase joins the frozen set the day it
ships, and the numbers must keep meaning what they meant.

[`CHANGELOG.md`](../../CHANGELOG.md) is unaffected and is still the permanent record of everything
that shipped. Issues replace the *waiting*, not the *record*.
