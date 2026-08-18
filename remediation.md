# Remediation 10

Reported 2026-08-18 against v0.34.0, from hand-testing the desktop window. Each item is checked
off as it ships, and **checked only once it has been seen to work** — a change that compiles is
not a fixed item.

Remediation 9 shipped whole in [v0.23.1](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

Several of these overturn a decision that is written down in the source. Those are called out
against the item, because a comment left standing beside code that no longer obeys it turns the
file into a liar — the same rule [docs/plans/change-requests.md](docs/plans/change-requests.md)
states for its own entries.

---

## The panel's chrome

- [x] **1. The tab strip overlaps itself.** Transcript through Settings, plus the Conversation /
  Technical / Log file mode control, plus the search box and Copy all compete for one row, and
  below a certain window width they collide. Three changes, settled with the Commander:
  the **mode control becomes a dropdown inside the panel** rather than a segmented pill beside
  the tabs; **search and Copy move into the pane** as well, leaving the strip for page selection
  alone; and the tabs themselves **truncate and gain ‹ › arrows** so every tab stays reachable at
  any width. Not yet seen in a headset, where the strip is narrower still.
- [x] **2. Copy is scoped to Transcript, and moves into the pane.** It has no visibility rule at
  all today, so it sits there on Checklist, Loadout, Engineers and Settings, where it copies the
  transcript the Commander is not looking at.
- [x] **3. Copy is not vertically centred, and says the wrong thing.** "Copy All", because "Copy"
  beside a page of selectable text reads as copying the selection.
- [x] **4. The help glyph is not centred in its hover highlight.** The drawn `?` sits off-centre
  in the button's highlighted box.
- [ ] **5. Opening the log file looks like nothing happening.** It reads a file off disk and takes
  long enough to doubt. There is a busy glyph on the mode already; it is not being seen.

## Reading the page

- [x] **6. Next / previous do not take you to the match.** Stepping should scroll the occurrence
  into view and draw the current one in a different theme colour from the rest.

  **Half of it already worked and was already tested.** Every hit is drawn muted and the current
  one accented, and `SearchTheTabTests` has asserted that since Phase 12. What did not work was the
  scroll, and the reason is one line: the offset was set immediately after the inlines were
  rebuilt, so the text layout the hit is measured against and the extent the offset is clamped to
  were both from before the change — and clamping against a scroller that has not measured the new
  content clamps to zero. A layout pass in between is the fix.
- [ ] **7. The log file has no startup or shutdown events.** On start: version, build, whether VR
  came up, which providers are configured, the data folder. On stop: why, and whether it was
  clean. *Built; not yet ticked — `AppHost` is not constructible in a test, so this is confirmed by
  running d47 and reading the log rather than by an assertion.*

  Three lines. A thin one before settings or the headset exist, whose job is to be there when
  startup dies before anything fuller can be said; the full one once the headset has been brought
  up, since that is the last thing that can answer for itself; and on the way out the reason and a
  **clean marker that is the absence of a line** — "is stopping" is written first and "stopped
  cleanly" last, so a teardown that died leaves the first standing alone. The reason is only ever
  something d47 actually knows: the window closed, or an update is replacing this build. A Windows
  shutdown and a kill both unwind saying nothing about themselves, and the default says the process
  is ending rather than inventing which.
- [x] **8. "Push-to-talk is bound to Oem4."** It is bound to `[` and should say so. `Gestures`
  already does exactly this and even records having fixed this once; the log line does not call it.
- [x] **9. `JBFqnCBsd6RMkjVDRZzb` means nothing to a human.** The spoken-line log should say the
  role, the name and the id. **Overturns a comment** in `SpeechPipeline.Record` arguing for the
  bare id.

## The checklist

- [x] **10. Accepting a removal did not remove it.** Reported verbatim: d47 proposed removing the
  one item on the list, the Commander accepted, d47 said "Removed from the list", and it was still
  there. A defect, and the only one here that is a lie rather than a rough edge.

  **The removal was never reached, and the removal code was never wrong.** Three tests walk the
  whole path over the two real files and pass unchanged. `accept_proposal` is protected — never
  advertised to the model, refused if it asks — so the only ways in are the panel and five exact
  whole-utterance phrases: *accept the proposal*, *accept the proposals*, *accept that*, *add it to
  my checklist*, *do it then*. The Commander said **"Accept."**, which is none of them, so it fell
  through to the model, which has no tool for this and said it had done it anyway.

  Two changes, settled with the Commander. The **bare words now route** — *accept*, *accepted*,
  *accept it*, and the same for declining — and they are live at all times, because saying one with
  nothing pending is answered honestly and cannot act on anything. The **conversational answers**
  — *yes*, *go ahead*, *do it*, *confirm*, *no*, *forget it* — route **only while a proposal is
  waiting**, which needed a `When` condition on a command phrase. Bound for a whole session, "yes"
  would swallow every yes in the conversation; bound to the moment there is a question, it is the
  answer to it. Command phrases are deliberately outside the tool schema, so none of this moved a
  byte of the cached prefix.

  And the model can no longer be the only witness to what happened. **Four prompt-side defences
  were already in place** when this was reported — the tool is protected, the prompt says every
  turn that d47 cannot accept on the Commander's behalf, the reply says "I cannot make this change
  myself", and the guardrails say never to claim an untaken action — and a model said "Accepted.
  Removed from the list" through all four. So the turn loop now asks the store what is outstanding
  before the model speaks and again after, and states the answer itself when nothing changed. It is
  silent on the turn that resolves the thing, which is what keeps it a fact rather than a nag.

  **One residual, stated rather than hidden:** a proposal outlives the session it was raised in, so
  a Commander who leaves one unanswered has "yes" and "no" bound until they answer it. That is the
  cost of the option chosen, and the standing line above is also what makes the pending proposal
  impossible to forget about.
- [x] **11. Add a line will not take the keyboard.** In the desktop window it should accept typing
  and Ctrl+V, not only speech and the on-screen board.
- [x] **12. "Say it — I am listening" is not true under push-to-talk.** It is not listening until
  the key is held. **Overturns a comment** arguing the one sentence was worded for both modes and
  that the microphone row beside it says which state d47 is really in. It now says what would open
  the gate — hold this key, press this key, say this name — and only continuous mode claims to be
  listening.
- [x] **13. Checklist lines are not editable.** Especially the ones the Commander wrote.
- [x] **14. There is no way to delete the current line by hand.**
- [x] **15. Import and export.** Everything, as JSON — a full round-trip of the checklist file,
  derived lines and provenance included, settled with the Commander as a move-machines feature
  rather than a share-with-a-friend one.
- [x] **16. "universal" is not a good descriptor; "custom" is.** Everything the Commander or the
  model sees — labels, wording, the tool schema and its docs page. The enum member and the value
  on disk stay as they are, so no migration and no risk to an existing checklist.

## Hearing

- [x] **17. "Unlock Lei Cheung" was heard as "Unlockly Chung".** Elite's proper nouns are exactly
  what Whisper invents around. d47 already holds engineer, system, ship and module names; the
  transcriber is to be biased toward them rather than corrected afterwards.

  **The biasing was built in Phase 6 and never connected.** `properNouns` has been a parameter of
  `TranscribeAsync` the whole time, the journal-derived list has been built and capped and handed
  over on every utterance, and the transcriber counted it in a log line and dropped it. "Transcribed
  2.4s of audio in 310ms with 23 name hints" was written while nothing was biased by anything —
  the worst shape a gap can have, because it reports as working. It is an initial prompt now, and
  the processor is rebuilt only when the names change, which is a handful of times an hour rather
  than once per utterance.

  **And the journal half could never have caught this one.** An engineer the Commander has not
  unlocked appears nowhere in their journal, so the single name they were saying was the one name
  the list could not offer. The engineers are a closed shipped set, so twenty of them are reserved
  at the end of the list — journal names still come first, because where the Commander is now beats
  every engineer in the galaxy, but a Commander with a large fleet no longer crowds them out.

  *Whether it now hears the name is a question for a microphone, not an assertion.* What is
  asserted is that the names become a prompt and that the prompt reaches the processor.

## The on-screen keyboard

- [x] **18. It should be QWERTY.** **Overturns a comment**, stated twice — in `PanelPrompts` and
  in `OffscreenSurface` — arguing that a staggered alphabetic board is faster to hunt with a ray.
  The Commander has ruled otherwise; both copies are to be changed together, and the duplicated
  table is to become one.
