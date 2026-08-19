# Remediation 14

**Seven of its nine shipped in [v0.36.2](CHANGELOG.md)**, which is their permanent record from
here. Items 4 and 5 are held for a decision and stay open below.

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

- [x] **4. Warden talked about another core.** *"Cora used to count like that when she was
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

  **Settled by the pack.** The rewrite struck Warden's *On the dead* entry outright, and porting
  it carried that into the shipped text — so the reported line cannot be produced. Every other
  core's reference to another was already unnamed, which is the pack working as designed and
  stays: *my secondary core*, *a preservation core from a rival clan*, *an archival core*.

  **Analyst Prime keeps Cora by name**, because he is about her and the rewrite strengthened that
  rather than removing it. So the rule shipped is "no core names another core, except that one",
  and it is a test rather than a promise — the property drifted in silently once and was found by
  a Commander hearing it. Reintroducing Warden's line fails two assertions by name.

  One line the pack had and the code never did is now in: the Archivist on the most complete
  records any of them kept belonging to the one they cast out. Unnamed, like every other.

- [ ] **5. Copy a plan to another slot by dragging.** Ctrl and left button held, dragged from one
  slot to another — by mouse, or by motion controller on the headset — copies the module, the
  engineering and the experimental effect, matching the new slot's largest size where it can. Only
  within one kind of slot: Hardpoints to Hardpoints, Utility Mounts to Utility Mounts, Optional
  Internal to Optional Internal. **Core Internal is not draggable at all** and is not a target.

  **Settled: it is a desktop-only item, because the headset no longer has the page.** Checklist
  and Loadout were withdrawn from the big VR panel on 2026-08-19 at the Commander's instruction,
  and the slot rows this item drags between live at Loadout › Ships › Slot. So there is no
  headset half left to design. What ships is the Core operation and the mouse: pointer
  pressed with the modifier down, capture, the row under the pointer on move, commit on release —
  plus a Core rule for picking the copy's size, which is the largest variant of the same module
  and mount that fits the target, and is the easiest part to get right and test.

  **The headset analysis is kept below rather than struck**, because the tab could come back and
  the collision would come back with it. Read as a record of what was found, not as work to do.

  **The headset half is not ordinary, and the reason is sharper than "there is no drag".** Read
  properly, `VrHost` already gives the trigger three meanings and disambiguates them after the
  fact: a **press** is down and up having neither dwelt nor travelled; a **scroll** is decided at
  the moment of the press by whether the ray landed on a scrollbar; and a **carry of the whole
  panel** begins at 400 ms of dwell *or* five percent of the panel travelled
  (`VrPress.BecomesACarry`).

  Dragging from one row to another is that third condition exactly. So this is not a missing
  capability, it is a collision: on the headset, pressing a row and then moving already means
  *pick the panel up*. Dwell is carry, travel is carry, and the grip is Back (list.md Phase 25).

  **"Every other candidate is taken" was too strong.** d47 binds two actions, not two buttons:
  trigger to carry and grip to back, on both hands. A controller has more — face buttons, a stick
  that clicks — and a third action is a declaration in `VrActionManifest` plus a binding, which is
  ordinary work. What it is not is free: bindings are written per profile, and of the four d47
  supports, **the Vive wand has no face buttons at all** and carries a trackpad where Touch and
  Index have a stick, while Touch names its face buttons A and B on the right hand and X and Y on
  the left. Trigger and grip are the only two inputs that exist identically everywhere, which is
  why they are the two that are bound.

  So there are two honest shapes for a headset modifier: **grip held with the trigger**, which
  needs no new binding and works on all four — Back is only acted on when it moves the panel
  somewhere, so a grip squeezed with the trigger already down is unambiguous — or **a third
  action**, bound to A on Touch and Index and to the trackpad click on the wand, which is a better
  gesture bought with per-profile binding work.

  **One measured correction to the three paragraphs above, recorded because it reverses their
  conclusion.** "There is no pointer motion on the headset" is true only of Avalonia's pointer
  events, and the implication drawn from it was wrong. `VrRay.PointingAt` computes a `Hit.U`,
  `Hit.V` on the panel **every frame**, and the panel already consumes that stream twice —
  `_panel.Aim` lights the row under the ray continuously, and `_panel.Scroll` is a drag that
  already works. A row-to-row drag is that same stream a third time, not a new one. The carry
  itself never touches the widget tree at all: it is `VrPlacementMath` moving the overlay quad,
  which is why the panel's content is unaware of it.

  So the headset half was **ordinary work after all**, and its shape was settled before the tab
  was withdrawn: sample the grip **at the moment the trigger goes down**, exactly as
  `_scrolling = _panel.GrabsScroll(u, v)` already decides scrolling and never revisits it. That
  ordering — grip before trigger — is forced, because a grip arriving later races the 400 ms
  dwell and the panel is already in hand. Its one price is that Back would move from grip
  **press** to grip **release** (`VrActionInput.BackPressed` is a rising edge today), suppressed
  if a trigger press happened while the grip was down. On its own that is imperceptible.

  None of which is built, because there is nothing in the headset to drag. **It is written down
  rather than struck** so that if Loadout ever returns to the big panel, the collision returns
  with it and this is the answer, already argued.

  The arm-and-press alternative — a *Copy this plan to…* control that marks legal targets — is
  **not** what ships. It was only ever the price of making one gesture work on both surfaces,
  and it costs a step the mouse does not need.

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

