# Remediation 10

Reported 2026-08-18 against v0.34.0, from hand-testing the desktop window. Each item is checked
off as it ships, and **checked only once it has been seen to work** — a change that compiles is
not a fixed item.

Remediation 9 shipped whole in [v0.23.1](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

Four of these overturn a decision that is written down in the source. Those are called out
against the item, because a comment left standing beside code that no longer obeys it turns the
file into a liar — the same rule [docs/plans/change-requests.md](docs/plans/change-requests.md)
states for its own entries.

---

## The panel's chrome

- [ ] **1. The tab strip overlaps itself.** Transcript through Settings, plus the Conversation /
  Technical / Log file mode control, plus the search box and Copy all compete for one row, and
  below a certain window width they collide. Three changes, settled with the Commander:
  the **mode control becomes a dropdown inside the panel** rather than a segmented pill beside
  the tabs; **search and Copy move into the pane** as well, leaving the strip for page selection
  alone; and the tabs themselves **truncate and gain ‹ › arrows** so every tab stays reachable at
  any width. Not yet seen in a headset, where the strip is narrower still.
- [ ] **2. Copy is scoped to Transcript, and moves into the pane.** It has no visibility rule at
  all today, so it sits there on Checklist, Loadout, Engineers and Settings, where it copies the
  transcript the Commander is not looking at.
- [ ] **3. Copy is not vertically centred, and says the wrong thing.** "Copy All", because "Copy"
  beside a page of selectable text reads as copying the selection.
- [ ] **4. The help glyph is not centred in its hover highlight.** The drawn `?` sits off-centre
  in the button's highlighted box.
- [ ] **5. Opening the log file looks like nothing happening.** It reads a file off disk and takes
  long enough to doubt. There is a busy glyph on the mode already; it is not being seen.

## Reading the page

- [ ] **6. Next / previous do not take you to the match.** Stepping should scroll the occurrence
  into view and draw the current one in a different theme colour from the rest.
- [ ] **7. The log file has no startup or shutdown events.** On start: version, build, whether VR
  came up, which providers are configured, the data folder. On stop: why, and whether it was
  clean.
- [ ] **8. "Push-to-talk is bound to Oem4."** It is bound to `[` and should say so. `Gestures`
  already does exactly this and even records having fixed this once; the log line does not call it.
- [ ] **9. `JBFqnCBsd6RMkjVDRZzb` means nothing to a human.** The spoken-line log should say the
  role, the name and the id. **Overturns a comment** in `SpeechPipeline.Record` arguing for the
  bare id.

## The checklist

- [ ] **10. Accepting a removal did not remove it.** Reported verbatim: d47 proposed removing the
  one item on the list, the Commander accepted, d47 said "Removed from the list", and it was still
  there. A defect, and the only one here that is a lie rather than a rough edge.
- [ ] **11. Add a line will not take the keyboard.** In the desktop window it should accept typing
  and Ctrl+V, not only speech and the on-screen board.
- [ ] **12. "Say it — I am listening" is not true under push-to-talk.** It is not listening until
  the key is held.
- [ ] **13. Checklist lines are not editable.** Especially the ones the Commander wrote.
- [ ] **14. There is no way to delete the current line by hand.**
- [ ] **15. Import and export.** Everything, as JSON — a full round-trip of the checklist file,
  derived lines and provenance included, settled with the Commander as a move-machines feature
  rather than a share-with-a-friend one.
- [ ] **16. "universal" is not a good descriptor; "custom" is.** Everything the Commander or the
  model sees — labels, wording, the tool schema and its docs page. The enum member and the value
  on disk stay as they are, so no migration and no risk to an existing checklist.

## Hearing

- [ ] **17. "Unlock Lei Cheung" was heard as "Unlockly Chung".** Elite's proper nouns are exactly
  what Whisper invents around. d47 already holds engineer, system, ship and module names; the
  transcriber is to be biased toward them rather than corrected afterwards.

## The on-screen keyboard

- [ ] **18. It should be QWERTY.** **Overturns a comment**, stated twice — in `PanelPrompts` and
  in `OffscreenSurface` — arguing that a staggered alphabetic board is faster to hunt with a ray.
  The Commander has ruled otherwise; both copies are to be changed together, and the duplicated
  table is to become one.
