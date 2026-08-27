# Change requests

**This file no longer holds any open request, and on 2026-08-27 it stopped being a queue.** A wanted
change that is not a defect is now a
[GitHub Issue labelled `change-request`](https://github.com/dseelinger/d47/issues?q=is%3Aissue+is%3Aopen+label%3Achange-request),
for the reason CLAUDE.md gives about planning generally: a queue held in a file conflicts on every
parallel branch, and this one was nothing *but* a queue. Items 39, 40 and 43 left that day as
[#102](https://github.com/dseelinger/d47/issues/102),
[#103](https://github.com/dseelinger/d47/issues/103) and
[#104](https://github.com/dseelinger/d47/issues/104).

**What remains is the numbering, and it remains because 61 comments in the source depend on it.**
The rules below are not history; they govern every number an issue may be given from here. Read them
before allocating one.

An entry's permanent record is still the line it gets in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it. An issue closing is not a record.

An entry states what is wanted and where the code is. Where one carries an **open question** that
changes the work materially, it says so — those want an answer before the code does, because the
answer is usually the difference between two different pieces of work rather than a flag.

Where an item contradicts a comment in the source, that is called out. Those comments are the
reasoning being overturned, and leaving one standing beside code that no longer obeys it turns
the file into a liar.

**Numbers are not reused.** Items cite each other by number, and reusing one would leave an old
citation resolving to a live entry about something else, reported by nothing — the trap the
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. Everything through 38 has
shipped and been pruned, so **the next number is 45** — the count is not the length of this file.

**41 was declined and its number is retired with it.** It asked for a picker among ElevenLabs'
synthesis models; the answer, 2026-08-25, was to move the pin to the best one and offer no choice
at all — see `ElevenLabsTtsProvider.DefaultModel` for why, and `CHANGELOG.md` for the release that
carried it. Declining an entry retires its number exactly as shipping one does: it was cited while
it was open, and a later entry reusing 41 would leave those citations resolving to something the
number was never about.

**And this very paragraph arrived as a merge conflict**, because two branches edited the line that
records the next number — which is the failure the line exists to prevent, arriving by the road it
warns about. It conflicted rather than resolving quietly, which is the outcome to want.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## How an issue gets one of these numbers

**At build time, not when it is filed** — the same rule phases take, and for the same reason. A
`change-request` issue is titled by its subject and identified by its **issue** number, which is
unique and permanent for free. It takes a **change-request** number only when source comments start
citing it, because being citable from code is the entire job that number does. Allocating one
earlier spends it on something that may never be built, which is how 41 became a retired hole.

So: when the commit that implements a request adds a comment citing it, take the next number from
the line above, cite it as `change-requests.md <N>`, and update that line in the same commit. The
number then belongs to that request permanently, whether or not it is ever written here again.
