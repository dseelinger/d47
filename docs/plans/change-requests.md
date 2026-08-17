# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are in
[bugs.md](../../bugs.md). Everything here behaves as built; the request is that it be built
differently.

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
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. The next batch starts
at 11.

---

## None open.

The ten raised hand-testing 0.15.0 on 2026-08-16 all shipped together in 0.21.0. Their record is
that section of the changelog, which keeps them in the order they were built.

Two of them left something worth knowing about:

- **Item 9's headset defaults have never been seen in a headset.** The arithmetic is tested and
  the first-show placement is written down, but whether knee height *reads* as the right place —
  and whether it is wrong for a seated Commander — is a question only somebody wearing one can
  answer.
- **Item 6 turned up a defect on the way past**, in the log-level rows rather than in anything it
  was asked to change: three of them named namespaces that do not exist and so controlled
  nothing. Fixed in 0.21.1, and `TechnicalLogBridge` now reads the one list rather than keeping
  a copy.
