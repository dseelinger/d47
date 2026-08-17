# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## 1. A dangling `tool_use` poisons the session until restart

**Seen** — 2026-08-16, build 0.15.0. Asking "Place the VR panel here." returned:

> I couldn't reach the model after 3 tries. Status Code: BadRequest
> `messages.2: tool_use ids were found without tool_result blocks immediately after:`
> `toolu_01Snvy5SCL91n35grjQbG6uV`

The **next**, unrelated message failed with the same error and the same id.

**Severity — high.** This is not a failed turn, it is a poisoned session. The id recurring
on a later message means the malformed exchange is in `_history`, so every subsequent turn
assembles the same invalid request. Retrying cannot help; all three attempts send the
identical payload. Only a restart clears it. The capability fallback still answers because
it never reaches the model.

**Verified** — `TurnLoop.cs:342` already anticipates exactly this: `pending` is held out of
`_history` "so a turn that fails commits nothing — a half-written exchange ending in a tool
call nobody answered is worse than no memory of it at all." That guard exists and something
got past it. The request is assembled at `TurnLoop.cs:375` as `[.. _history, .. pending]`,
so a dangling call in either list poisons every later turn.

**Not diagnosed.** Candidates: the `MaxToolRounds` ceiling committing a round whose result
was never appended; a tool that throws or returns nothing; cancellation mid-round; a commit
path that writes `pending` on partial success.

**How to diagnose** — point `llm.endpoint` at a local endpoint with a dummy key and read the
exact messages array. This bug is entirely about the shape of that array, so that is the
instrument; reading harder is not.

---

## 2. Clearing the Settings search does not restore the other sections

**Seen** — Settings tab, type `speech`, clear the box. The nav still lists only Speech,
Listening, Privacy and egress, Audio mixer. There are 18 sections.

**Verified** — `ApplyFilterToCards` (`SettingsView.axaml.cs:697`) sets
`Card.IsVisible = !filtering || holds`, so if `Filter("")` reaches `Refresh()` everything
returns unconditionally. The fault is upstream of that method.

**Not diagnosed.** Either `Filter("")` never runs — the dispatch at `PanelView.axaml.cs:441`
is null-conditional, so a missed cast is silently dropped — or `Filter()`'s early return sees
`_query` already empty and skips `Refresh()`, meaning PanelView's `_query` and SettingsView's
have drifted apart.

**Related** — the missing match highlighting is why this failed quietly enough to become a
bug report. With matches marked, "the filter removed nothing" would have been obvious.

---

## 3. The Settings nav column is blank until you scroll

**Seen** — open Settings. The nav is empty but for a lone orange active-marker bar. Scroll
the cards and all 18 sections appear.

**Strong lead, unverified.** Nav brushes are fetched once, not bound: `Res(key)` is
`this.FindResource(key) as IBrush` (`SettingsView.axaml.cs:136`). Off the visual tree that
resolves nothing and yields **null**, and `UpdateNavVisuals` assigns it straight into
`NavText.Foreground` for every section. Null foreground is text that does not draw — layout,
item count and hit-testing all intact, nothing visible. The lone orange bar corroborates it:
`NavBar` is driven by `Opacity`, a double that cannot come back null. The first scroll re-runs
`UpdateNavVisuals` once attached, so the resources resolve and everything appears.

**The fix is already in the file.** `Themed()` (`SettingsView.axaml.cs:142`) binds a brush
property to a theme resource "so a theme switch repaints controls built in code the same way
DynamicResource repaints the ones built in markup." The nav does not use it. Moving those
assignments onto bindings also retires the comment above `UpdateNavVisuals`, which exists
only to explain why the fetched brushes need manual repainting — the bug describing itself.

**Note** — probably *not* the same fault as bug 2. Invisible text does not explain four items
rendering normally while the rest are absent. Confirm separately.

---

## 4. The VR panel cannot be grabbed with a motion controller

**Seen** — the panel cannot be picked up or moved in the headset.

**Verified — the feature is implemented, and carefully.** `VrHost.Carry()`
(`VrHost.cs:272`) handles press, hand recovery, carry and release. It already respects the
two measured traps: the hand is recovered by ray-casting each controller at the quad rather
than trusting `trackedDeviceIndex`, and the anchor is written once on release rather than per
pose. `VrOverlay.cs:157` sets both required flags — `SetOverlayInputMethod(Mouse)` and
`MakeOverlaysInteractiveIfVisible`. So something in the chain returns empty.

**Not diagnosed.** Four silent failure points, cheapest first:

1. `Carry()` is never called — confirm it is on the tick loop.
2. `overlay.Pointer.Held` never goes true — the press is not arriving as a button.
3. `Pointing(overlay)` returns null — the ray/quad intersection misses, so the hand is never
   identified and the grab is abandoned one line before it starts.
4. `OverlayFor(_panel.Surface)` returns null — this is keyed on the **current** surface, so
   mini and full are different overlays and a grab wired for one is not wired for the other.

There is a debug log on put-down but none on pick-up, so the log can show a drop with no
matching grab. Adding the mirror line is likely the fastest way to find which link is dead.

**Read first** — the six OpenVR/Avalonia traps recorded from Phase 9. Every one of them
fails silently: no exception, no warning. This bug is in the code they are about.
