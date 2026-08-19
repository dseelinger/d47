# Remediation 14

Reported from 2026-08-19 against v0.36.1, one item at a time. Each is checked off as it ships,
and **checked only once it has been seen to work** — a change that compiles is not a fixed item.

Remediation 13 shipped whole in [v0.36.1](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

- [x] **1. The first hardpoint row is a column of single letters.** Reported with a picture: under
  Hardpoints, the row carrying a long plan is three hundred pixels tall, with "Large Hardpoint 1"
  wrapped one character per line down the left edge and the plan taking the rest of the width.

- [x] **2. "Broo Tarquin is still three steps out."** An opening fact of that kind is not useful.

- [x] **3. A stale fact spoken in the present tense.** *"Sacred Fire is mid-manoeuvre — a jump
  inside Laksak"*, said of a jump that completed the day before.

- [ ] **4. Warden talked about another core.** *"Cora used to count like that when she was
  checking a relay."* A core does not know or care that the others exist.

  **Held: this line is prescribed, and the reading may be the other way round.**
  `guardian-personas.md` line 52 says of Warden, in as many words: *"Mentions Cora the way you
  mention a colleague you respected and were slightly afraid of — 'she'd have called that sloppy'
  — always past tense, always fond."* It is in his shipped body text for that reason, and the
  isolation model at the top of that file is not that a core never names another core, but that
  **no core knows another is present**: each believes it is the only survivor. Warden speaks of
  Cora as dead. Cora, running, believes the same of him.

  So the line is the design working, and what breaks it is something else — **Cora is also a core
  in the picker.** A Commander who has seen her in the list hears one AI talking about another AI
  in the same application, which is exactly the report. The spec's own note (line 269) says only
  the player sees the seams, and this is that seam being uncomfortable rather than a defect.

  Three ways out, and they are yours to pick rather than mine: strike the named dead from every
  core's body and keep the unnamed ones like *the quiet one*; rename the dead so no shipped core
  shares a name; or leave it. The first two are edits to eleven pieces of writing and an amendment
  to `guardian-personas.md`, which is why nothing was changed on a guess.

- [ ] **5. Copy a plan to another slot by dragging.** Ctrl and left button held, dragged from one
  slot to another — by mouse, or by motion controller on the headset — copies the module, the
  engineering and the experimental effect, matching the new slot's largest size where it can. Only
  within one kind of slot: Hardpoints to Hardpoints, Utility Mounts to Utility Mounts, Optional
  Internal to Optional Internal. **Core Internal is not draggable at all** and is not a target.

  **Held for its own session, on the headset half.** The desktop half is ordinary: pointer
  pressed with the modifier down, capture, the row under the pointer on move, commit on release —
  plus a Core rule for picking the copy's size, which is the largest variant of the same module
  and mount that fits the target, and is the easiest part to get right and test.

  The headset half is not ordinary. `VrPanelSurface` has no press-move-release at all: a ray
  gives `Press(u, v)`, which synthesises a *click*. The one drag that exists there — the
  scrollbar — is built as its own trio of geometric hit tests (`GrabsScroll`, `Scroll`) rather
  than out of pointer events, so a carry between rows needs a third gesture of the same shape,
  a controller binding for a modifier a motion controller does not have, and something drawn to
  say what is being carried. None of that can be checked without the headset on, and this repo
  has a memory file of VR traps that fail silently and look like working code.

  Shipping the desktop half alone would leave a gesture that does nothing on the other surface,
  which is worse than not having it yet.

- [ ] **6. A core per ship.** Each ship remembers the core that flew it — Sentinel on the combat
  ships, Quartermaster on the haulers — set at the Commander's command rather than by watching.

  **Written up as [Phase 35](list.md) rather than here**, because it is a capability rather than
  a defect and that is where capabilities live. This line is a pointer so the next session finds
  it; the detail, the invariant it has to respect and the one open question that changes the shape
  of the work are all in list.md.

- [ ] **7. Trade routes d47 works out itself.** A hold that is not always emptied, round trips, and
  the profit a station pays less for being flooded.

  **Written up as [Phase 36](list.md)**, and asked for as its own session. The note there records
  what already exists — today's `plot_trade_route` is Spansh's answer rather than d47's — and that
  the saturation figure is to be measured from the Commander's own `MarketSell` events rather than
  guessed at.

- [x] **8. Nothing heard should be nothing said.** Mouse clicking with no speech behind it drew
  *"Nothing spoken, Commander. Only hands at work. I'll hold the channel open."* — and a cue after
  it. Neither should happen.

- [x] **9. The transcript's context menu should copy the selection.**

