# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are
[GitHub Issues](https://github.com/dseelinger/d47/issues). Everything here behaves as built; the
request is that it be built differently.

An entry leaves this file when it ships, and the line it gets in [CHANGELOG.md](../../CHANGELOG.md)
under the release that carried it is its permanent record.

An entry states what is wanted and where the code is. Where one carries an **open question** that
changes the work materially, it says so — those want an answer before the code does, because the
answer is usually the difference between two different pieces of work rather than a flag.

Where an item contradicts a comment in the source, that is called out. Those comments are the
reasoning being overturned, and leaving one standing beside code that no longer obeys it turns
the file into a liar.

**Numbers are not reused.** Items cite each other by number, and reusing one would leave an old
citation resolving to a live entry about something else, reported by nothing — the trap the
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. Everything through 33 has
shipped and been pruned, so **the next number is 38** — the count is not the length of this file.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## Open

Nothing. Items 34, 36 and 37 shipped on 2026-08-24 and their record is the
[CHANGELOG.md](../../CHANGELOG.md) line under the release that carried them.
