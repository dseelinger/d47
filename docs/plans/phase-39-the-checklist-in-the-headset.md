# Phase 39 — The checklist in the headset

The plan of record for a phase that is **not built**. Written 2026-08-20 on the Commander's
instruction: *"Add the Checklist tab back into VR."*

`list.md` carries the Phase 39 entry as of 2026-08-20, unticked. This document is the reasoning
behind those lines and the measurements they rest on; the list is the product description.

---

## The number

**Phase 39.** CLAUDE.md freezes 1–21 and 23–37, retires 22, and appends new phases. Phase 38
(*A build you can watch*) is written and unbuilt; this is the next number after it. Neither
depends on the other, so they can be built in either order.

## The phase in one sentence

Furnish the **Checklist** tab on the VR panel again, so what the Commander is working on is
readable in a headset, where it cannot be read any other way.

## What this reverses, and what it does not

Phase 25 made the checklist reachable in a headset — which a `Window` cannot be — and the tab was
**withdrawn on the Commander's instruction** during the panel redesign, along with Loadout. That
withdrawal is recorded in `VrPanelSurface` and in CLAUDE.md's parity amendment, and it was a
deliberate call rather than a discovery that the tab did not work.

**This phase reverses half of it.** Checklist comes back; **Loadout stays withdrawn** unless asked
for separately. Feature parity between the two surfaces is explicitly a someday-maybe, so bringing
one tab back is not a step toward bringing them all back and should not be read as one.

## Why it is nearly free, and why it still needs a phase

The withdrawal was done **by not calling `EnableChecklist`** rather than by hiding anything. A tab
nobody furnishes has no builder, registers no root, and `PanelView.Tab` already declines to select
it — so the spoken route and the drawn one agree with no special case in either. `VrPanelSurface`
still takes `checklists`, `goals` and `backfillGoals` from `AppHost` and leaves them unused,
against exactly this day.

So the code change is **one call**. What is not free is everything a headset does differently from
a monitor, and that is what the phase is for:

- **A ray is not a mouse.** Rows sized for a pointer at a desk are a different target at arm's
  length through a lens. The checklist is a long list of short rows, which is the worst case.
- **Text size at overlay resolution.** The panel renders once and is sampled by the compositor; a
  secondary line that reads at 100% on a monitor can be unreadable through a headset.
- **It is a list that scrolls.** Scrolling by ray is the interaction most likely to be wrong, and
  the checklist is the first tab in VR that needs much of it.
- **Filters and the state ladder.** The tab's filters are how a Commander narrows 124 items to the
  handful that matter, and a filter row that cannot be hit is worse than no filter.

## The items

1. **The tab is furnished.** `VrPanelSurface` calls `EnableChecklist`, and the comment recording
   the withdrawal is updated rather than deleted — it explains a decision that was real.
   *Accepted when:* the tab appears in the headset, the spoken route reaches it, and Loadout still
   does not appear.

2. **It reads at overlay resolution.** Row height, type scale and the muted second line checked
   against a real capture rather than against the window.
   *Accepted when:* a headless capture at the overlay's own size is legible, and the Commander
   confirms on the headset — this is the class of thing only an eye catches, which Phase 37
   already recorded twice.

3. **A ray can hit every row it needs to.** Items, the filter row, and the scroll.
   *Accepted when:* every interactive target meets the size the VR pages already settled on, and
   a long list can be scrolled to its end without the ray leaving the panel.

4. **Ticking works from the headset.** Including the refusal: a derived item cannot be ticked by
   hand, and the reason has to be readable there too.
   *Accepted when:* an authored item ticks, a derived one refuses with its sentence, and neither
   needs the desktop window.

## What this phase does not do

- **It does not bring Loadout back.** That is a separate decision and a bigger surface — the drill
  is three levels deep and the slot chooser is a search field.
- **It does not add a VR-only checklist feature.** One view definition renders to both surfaces;
  a tab that grew its own behaviour in the headset would be the second UI codebase the invariant
  exists to prevent.
- **It does not restore parity as a principle.** Parity stays a nice-to-have.
