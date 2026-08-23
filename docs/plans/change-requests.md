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
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. Everything through 33 has
shipped and been pruned, so **the next number is 35** — the count is not the length of this file.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## Open

### 34. The window's tab and view carry to the mini panel, where the mini panel has them

Asked for 2026-08-23.

> Switching to a tab (and view of the tab) in the main window should ALWAYS affect the mini-panel —
> IFF that tab/view is present on the mini panel.

**This reverses a decision Phase 45 made explicitly, and the reasoning being overturned is written
down in the source.** `src/D47.Core/Interface/TranscriptMirror.cs:15-19`:

> **Only the transcript.** Mirroring tabs and trails as well would acquire an *except*: Settings is
> desktop-only and Loadout is withdrawn from VR, so that rule would hold only sometimes, which is
> the kind people misremember. Every surface furnishes all three transcript roots, so this one has
> no except.

The objection was never that mirroring tabs is hard — it was that the rule would need an exception
and an exception gets misremembered. **The Commander's ruling supplies the exception in the same
breath as the request**, which is what disposes of the objection: *IFF that tab/view is present on
the mini panel*. That is a stated rule with its own condition attached, not a rule that happens to
fail sometimes.

**The mechanism already exists and no new state is needed.** `PanelView.Tab` already declines to
select a tab nobody furnished, and `PanelNavigation.Register`
(`src/D47.Core/Interface/PanelNavigation.cs:201`) already records every root every surface
furnished, with `Roots` (`:228`) reading them back in bar order. So *is this tab present on that
surface* is a question the code can already answer — the same *not calling `Furnish`* that withdrew
Loadout from the headset. A destination the mini panel does not furnish is simply one the mirror
does not carry there, and the surface that has it is unaffected.

**Where it goes.** Beside `TranscriptMirror` rather than inside it, or by widening it — but **not as
a second mechanism**, which is the trap Phase 45 named and solved once: two mechanisms holding one
invariant eventually disagree about it. The existing re-entrancy guard, the last-seen-root direction
rule and the *decline a root you are already on* behaviour are all still exactly what is wanted and
should be reused rather than re-derived. `TranscriptMirror`'s own doc comment must be rewritten
where it now says the opposite, in the Commander's words, the way `TranscriptPage`'s was when Phase
45 reversed *it*.

**`PanelMode` and zoom are untouched.** Phase 45's principle survives this intact — what you are
reading is shared, how a surface draws it is not — and this request extends the first half without
touching the second. Mini stays mini.

**Open question, and it needs an answer before code: is this one-way?** The request names one
direction — window → mini panel. `TranscriptMirror` is deliberately symmetrical with *no preferred
surface*, and `list.md` Phase 48 states the opposite for tabs: *"What must not follow is the
overlay's tab dragging the window's."* So there are two coherent designs — a symmetrical mirror
with a furnished-only filter, or a **follower** relationship where the window leads and the mini
panel may be moved independently until the window next moves. The request as written is the second.
Confirm which, because it decides whether a Commander in a headset can move their own panel and keep
it there.

**Related, and probably the same fix.** `bugs.md` carries a report from the same day that the
Checklist *filter* does not agree across surfaces, because `_chosen` and `_query` are instance
fields on `ChecklistPage` rather than shared state. A filter is arguably "view of the tab" in the
sense meant here. Decide whether this request covers it or whether the filter is a narrower thing
that travels by the same road; do not fix them twice.
