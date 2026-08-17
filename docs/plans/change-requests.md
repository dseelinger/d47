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
at 20.

---

## Open

Nothing open.

---

## Shipped

**The ten raised hand-testing 0.15.0 on 2026-08-16** all shipped together in 0.21.0. Their record
is that section of the changelog, which keeps them in the order they were built.

Two of them left something worth knowing about:

- **Item 9's headset defaults have never been seen in a headset.** The arithmetic is tested and
  the first-show placement is written down, but whether knee height *reads* as the right place —
  and whether it is wrong for a seated Commander — is a question only somebody wearing one can
  answer.
- **Item 6 turned up a defect on the way past**, in the log-level rows rather than in anything it
  was asked to change: three of them named namespaces that do not exist and so controlled
  nothing. Fixed in 0.21.1, and `TechnicalLogBridge` now reads the one list rather than keeping
  a copy.

**Items 11 to 14, raised on 2026-08-17**, shipped in 0.22.0: the NPC preamble, comms on the
Technical page, the radio treatment for everybody who is not aboard, and an NPC's voice being
theirs for as long as the Commander is in the system. Their record is that section of the
changelog.

Two of them also turned up something on the way past. The empty-sender case was being read aloud
as " says: …" — 8821 events in the corpus have an empty `From` rather than a missing one. And the
crew's voice assignments shared the per-system table with the NPCs, so a hired gunner changed
voice on every hyperspace jump; they are aboard, so they now last the session.

**The five raised hand-testing 0.21.x on 2026-08-17** — items 15 to 19, all of them about the
settings surface — shipped together in 0.23.0. Their record is that section of the changelog: the
search matching a section's own name, **Verify Key** shut until a key is typed, the ElevenLabs key
row moved up beside the provider that needs it, and the voice picker's audition becoming a play
glyph on each row now that a click highlights rather than chooses.

Item 18's open question was answered **both ways**: the price is a line above the list *and* the
pointer text on every glyph. A tooltip alone would have made a cost you have to hover to discover,
which is what Phase 11 put the number on the button to prevent.

Two of them turned something up on the way past. Item 18 was only possible because of 19, which is
why they shipped together — and building the picker's rows per keystroke, as the first cut did,
cost it the highlight on the value the Commander arrived with: a list holds its selection by
object, and a text box raises `TextChanged` as its template applies, so the filter re-ran and
handed the list a different row for the same voice before the window had finished opening.
