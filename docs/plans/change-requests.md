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

Raised hand-testing 0.21.x on 2026-08-17. All five are about the settings surface, and none of
them is a defect.

### 15. Settings search should match section names

Typing "Speech" finds rows and not the section called Speech, so a search for a section's own name
looks like it found nothing at the top of what it was looking for. `IFilterablePage` and the search
in `SettingsView.axaml.cs`.

### 16. "Verify key" should be inert until a key has been typed

It is offered on an empty box, where the only answer it can give is that an empty key is not a
valid one. `SecretEditor`.

### 17. The ElevenLabs key belongs beside the provider that needs it

It is at the bottom of the Speech section, several rows below the dropdown that made it relevant.
The row already knows when it applies — see `SpeechCapability.KeyRowFor` — so this is where it
sits, not whether it is shown.

### 18. Auditioning a voice is a glyph on the row, not a button with a price on it

"Hear it (about $0.013)" is a button and a disclosure where a play control would do. Wanted: play
and stop glyphs at the right of each voice in the list. **Open question:** the cost disclosure is
there because auditioning an ElevenLabs voice spends the Commander's money, and Phase 11 put the
number on the button deliberately. Moving to a glyph needs somewhere for that to go — a tooltip, a
line under the list, or once for the whole picker. `PickerWindow.axaml`, `AuditionLine`.

### 19. Clicking a voice should highlight it, not choose it

The picker commits and closes on a single click, so there is no way to look at the list. Wanted:
click selects, and **Use this** commits. This is also what makes item 18 possible — a play glyph on
a row that dismisses the window on click cannot be pressed. `PickerWindow.axaml.cs`.

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
