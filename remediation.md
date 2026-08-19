# Remediation 16

Reported from 2026-08-19 against **v0.38.1**, plus two defects found by reading somebody else's
code. Each is checked off as it ships, and **checked only once it has been seen to work** — a
change that compiles is not a fixed item.

**Remediation 15 is finished and its record has moved.** All fifteen items shipped in
[v0.38.1](CHANGELOG.md), and the line each got there is its permanent record. This file is the
current batch and not a growing archive, which is why 15 is gone from it.

## Where this batch came from

Two sources, and it is worth keeping them apart because they found different kinds of thing.

**Four items are the Commander's**, reported one at a time against a running build — three of them
with a picture. All four are the interface: one is a defect that removed a whole capability from
the panel, and three are the panel not being readable.

**Two are from reading [EliteIntel](https://github.com/SudoKrondor/EliteIntel)**, a Java companion
for the same game solving the same problems, on a research pass with no code change intended.
Neither of those two was reported by anybody, and that is the point: both are faults that produce
**silence** rather than a symptom — a headset panel that is quietly one state behind, and a
keybinding that quietly stops matching the game. A Commander cannot report either, because neither
looks like anything. They were found by reading a codebase that had already hit them and written
down what it cost.

## The original asks

Recorded in the Commander's own words, as remediation 15 established. **Where an item and an ask
disagree, the ask wins.**

| # | The ask, as made | Item |
|---|---|---|
| 1 | Add a dividing line between "Forget introductions" and "Ship". | 3 |
| 2 | In settings, put Language Model, Speech, and Listening at the top of the settings. | 4 |
| 3 | I have lost the ability to modify the loadout. Don't see how to modify "Reaper". | 5 |
| 4 | This should not be laid out on a single line. Hard to get what I need from that. At least a bullet list of the different types: * Beam Laser (G5) * Burst Laser (G5) etc. | 6 |

## What runs through this batch

**A. Joins that miss and fall back silently — again, and this time it cost a capability.**
Remediation 15 named this and fixed four instances of it. Item 5 is a fifth, and the worst so far:
`EliteSpecifications.Slots` keyed straight off the hull string it was handed while
`EliteSpecifications.Ship` resolved the same string properly, so one lookup answered
`Cobra MkV` and the other only answered `cobramkv`. A ship the Commander could open, read every
figure of, and then find had **no slots to plan at all**. The rule stated in 15 — *no join should
fail quietly* — has now been paid for twice, and the fix here is the shape it should always take:
not a second spelling remembered in a second place, but one resolver both callers go through, so
they agree by construction.

**B. Faults that report as silence.** Items 1 and 2 are both this. A refused overlay frame was
dropped and the surface's dirty flag was already down, so the panel simply stopped being current
with nothing in the log. The bindings file was read once at startup, so a Commander who rebound a
control in Elite's own options menu spent the rest of the session injecting a stale scancode.
Neither has a symptom a Commander could name — the panel is *there*, the key press just *does
nothing* — and both were sitting in code with a comment explaining why they were fine.

## The items

- [x] **1. A refused overlay frame was dropped and never drawn again.** *Built.* Found by reading
  EliteIntel's `platform_openvr.c`, which sets a dirty flag on every refusal with the reason in one
  line: *the model has already moved on, and a dropped frame is a card that is quietly one reply
  behind for good.*

  d47 had the same fault and had it worse. `SteamVrRuntime.Serve` submitted only when the surface
  said `IsDirty`; `Draw` cleared that flag **before** the submit; and `VrOverlay.Submit` discarded
  the `EVROverlayError` it got back. So a refusal lost the frame permanently — the headset held
  whatever it last showed until something unrelated happened to mark the surface dirty again,
  which for a panel nobody is touching can be minutes.

  **Redrawing is the wrong retry**, and this is the half that is easy to get wrong. What the
  compositor refused is the upload, not the picture: the pixels are already in the buffer and are
  still the ones wanted. So a refusal **holds** the frame — the ring does not rotate, nothing is
  rasterised again, and the next pass re-sends the same bytes. Re-rasterising an Avalonia tree ten
  times a second to retry a call that costs microseconds would pay the whole price of the frame
  for its cheapest part.

  **And only while SteamVR is drawing the quad.** A refusal with the dashboard up, or the headset
  in standby or off the Commander's head, is correct behaviour rather than a fault. EliteIntel's
  second bug on this exact path was counting those: their recovery fired every five seconds for as
  long as the dashboard was open, and every one of those is a card that vanishes and comes back.
  d47 does not rebuild overlays so it cannot reproduce *that*, but it would have re-sent a
  panel-sized buffer — about nine megabytes, copied inside OpenVR — at every tick for as long as
  the dashboard was open. `IsOverlayVisible` gates the retry, and it is asked only when something
  is actually waiting, so an ordinary frame costs no call at all.

  **Visibility throttles the retry, never the draw.** A surface that changed while the dashboard
  was up has to be current the moment the dashboard closes, and the only way to be sure of that is
  to have drawn it.

  **The recovery is reported, and that is a second defect fixed with it.** `_complaints` was
  add-only for the life of the process, so a refusal was said once and never again: a log could say
  frames were refused and never say whether that lasted one second or the whole session, and a
  second run an hour later left no trace at all. A run is now reported once, its recovery is
  reported once, and the complaint is forgotten so the next run is its own event.

  The decision lives in `D47.Core.Vr.FrameDelivery`, for the reason `RuntimeReadback` does: inside
  `SteamVrRuntime` it is three booleans threaded between an overlay call and a compositor call, in
  the one part of d47 no test can reach (architecture.md §8). **Stated plainly: the decision is
  covered by twelve tests and the wiring is not.** The seam that calls it needs a headset, so what
  is proven here is the policy, and the integration is proven by using it.

- [x] **2. The bindings file was read once at startup, and the reason given was wrong.** *Built.*
  The comment in the composition root said the file changes only when the Commander edits their
  controls, *"which they cannot do while d47 is the foreground window, so re-reading it ten times a
  second would be polling for an event that cannot happen."*

  Controls are edited in **Elite's** options menu, where Elite is the foreground window and d47 is
  not. The event is not only possible, it is routine — and the moment a Commander is most likely to
  go and do it is straight after d47 has told them an action is not bound. From then until a
  restart, every injection sent the old scancode, and every answer about what is reachable
  described a preset no longer in use.

  EliteIntel watches the file on a `WatchService` and reloads. d47 does it in d47's own shape:
  `BindsWatch.Poll()` on the tick loop, exactly as `MacroStore` already does, comparing write
  stamps before parsing anything — so the ordinary tick costs two or three calls asking a file when
  it was last written, and no XML.

  **Two files, not one.** A rebind rewrites the `.binds` file; switching preset rewrites
  `StartPreset` and changes which `.binds` file is even the answer. Watching only the resolved file
  would miss the larger of the two changes — the one that can move every binding at once. The stamp
  is taken again after a reload, because a preset switch lands on a different file and the stamp
  recorded before the read describes the old one.

  **Cache-safe, which is the obvious objection and worth answering.** Bindings never reach a tool
  schema — capability descriptors are registered once and never mutated. What they reach is the
  reachability sentence in the game-state block, which sits *below* the cache breakpoint and is
  expected to change every turn. So a reload changes what d47 says is possible and costs nothing in
  cached tokens.

- [x] **3. A dividing line between "Forget introductions" and "Ship".** *Built.* Reported with a
  picture: the persona rows and the ship-core pair running together as one undifferentiated column,
  with a button in the middle of it.

  The panel already had the mechanism — `SettingRow.Group` draws a rule and a heading — and the
  ship-core rows simply were not in a group. So the fix is the two rows naming one, which gives the
  line that was asked for **plus the one thing a bare rule cannot carry**: a name saying what the
  rows beneath it are for. Both rows name the same group, so it is drawn once rather than between
  two rows that are one thought.

- [x] **4. Language model, Speech and Listening at the top of Settings.** *Built.* They were at
  ordinal 30, 30 and 32, behind Help, Persona, Memory, Habits, Commander's log, Goals and Location.
  Those are the three a Commander has to set before d47 does anything at all, and they were seventh,
  eighth and ninth.

  Now 1, 2 and 3. **That also fixes a tie nobody had noticed**: Language model and Speech were both
  30, so which came first was whatever order the registry happened to enumerate in.

  `Display.Order` has exactly one reader — `SettingsService.Bind` — so this moves the settings
  sections and nothing else.

- [x] **5. A ship you are not flying had no slots to plan.** *Built.* Reported with a picture:
  *"I have lost the ability to modify the loadout. Don't see how to modify Reaper."*

  The page opened. It named the hull, gave speed, boost, armour and unfitted price, said where the
  ship was parked and what it was worth — and then offered no slots at all, only *"Nothing is
  planned, and I cannot see this ship's modules. Plan a slot and it will appear here"*, which is the
  one thing the page had no way to do.

  **Thread A, and the most expensive instance of it yet.** `StoredShips` carries the localised hull
  name — `element.Named("ShipType")` prefers `ShipType_Localised` — which is deliberate and is what
  the fleet page prints as *Reaper (Cobra MkV)*. That name is what a build started from a parked
  ship holds. `EliteSpecifications.Ship` resolves symbol **or** name, so every figure on the page
  came out right. `EliteSpecifications.Slots` did a raw dictionary lookup on the lowercased string,
  so it answered `cobramkv` and not `Cobra MkV`, returned nothing, and the fallback for a hull with
  no known layout — list what the journal mentioned — had nothing to list either, because Elite
  reports the loadout of one ship at a time and this was not that ship.

  Two lookups of one hull, disagreeing, and the disagreement rendered as a feature that had gone
  missing.

  **Fixed at the resolver, not at the caller.** `Slots` now goes through `Ship` and keys off the
  symbol it returns. Adding the second spelling to the dictionary would have worked today and been
  a third place to remember; this makes the two agree by construction. Verified against the reported
  ship: a Cobra Mk V parked at BNH-T2F, no build, twenty-six slots where there were none.

- [x] **6. An engineer's grades read as a paragraph.** *Built.* Reported with a picture: *"This
  should not be laid out on a single line. Hard to get what I need from that."*

  Nine specialities set as running prose, wrapping into a paragraph a Commander has to read through
  to find out whether the one they came for is in it — and the commas between entries looking
  exactly like the commas inside them.

  **The list is a second projection, not a formatting of the sentence.** Splitting the joined string
  on its commas would be a second parser of d47's own prose, wrong the day a speciality name has a
  comma in it. Both come off the same table. That is also why the grade reads `(G5)` in the list and
  `to 5` in the sentence: **a list is scanned and a sentence is heard**, and they are allowed to
  differ. The spoken form is what `get_engineer` says out loud and is untouched — there is a test
  saying so, because changing what d47 sounds like as a side effect of a panel fix is exactly the
  kind of thing that ships unnoticed.

## Checked against EliteIntel and already right

Recorded so it is not investigated twice. Each of these is something they hit, wrote down, and
fixed, where d47 already had it — and in two cases had it for a better reason.

- **Draining the overlay event queue.** Their headline VR fault: an overlay's own event queue that
  nobody read grew until the HUD stopped updating altogether, worst when the headset came off.
  `VrOverlay.PumpEvents` already drains it and already explains why — *a queue nobody drains is one
  that grows*.
- **Oversized journal lines.** A fleet carrier's `StoredModules` can exceed 64 KB on one line; their
  fixed-buffer reader met one, never advanced past it, and silently stopped reading for the rest of
  the session. `JournalReader.Poll` copies to end of file and advances by `GetByteCount`, so it is
  immune to that *and* to the char-index-versus-byte-offset trap next to it.
- **Scancodes rather than VK codes**, because DirectInput identifies keys by hardware scan code.
  Both projects independently; d47's is in `ScancodeInjector` and is an architecture invariant.
- **Serialised injection**, so two capabilities cannot interleave chords and leave a key held. Their
  single worker thread, d47's `SemaphoreSlim`.
- **A persistent audio output device**, rather than opening and closing one per utterance. They note
  it causes audible pops; d47 opens one `WasapiOut` over a mixing graph and never closes it.
- **A minimum utterance length.** They pad short audio and penalise blanks because a model handed
  200 ms of room tone returns a confident wrong word. `ListenGate.MinimumLength` is 250 ms and
  applies to every close, hands-free included.
- **Whisper's annotations.** `SpeechNoise` already discards a transcription that is nothing but
  `[BLANK_AUDIO]` or `(mouse clicking)`, and does it by shape rather than by a list of phrases.
- **The Edge Read Aloud 4096-byte request cap.** Both projects use the same undocumented endpoint.
  They cap explicitly; d47 never reaches it, because `SentenceSplitter` breaks any run-on at 320
  characters before a request is built. Reached by a different road, and worth knowing the road
  exists.
