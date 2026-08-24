# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are
[GitHub Issues](https://github.com/dseelinger/d47/issues). Everything here behaves as built; the
request is that it be built differently.

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
shipped and been pruned, so **the next number is 38** — the count is not the length of this file.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## Open

### 37. A shortfall says which ships want a material, but never which blueprint

Asked for indirectly on 2026-08-20 and carried here from `bugs.md` when the entry it sat inside was
fixed and pruned. The Commander asked what a shortfall of Conductive Polymers was for, and d47
answered:

> I can't tell you from here which single blueprint eats them — the shortfall is netted across every
> live plan at once, and there are a great many.

**That was honest about the tool and slightly harder on itself than it needed to be.** The gap report
does carry attribution — `GapDemand.What` is `"{ship} · {slot}"`, folded in at
`src/D47.Core/Loadout/PlanGap.cs` and printed by `GapCapability.cs:172` as *"— for Bad Idea (Python)
· MainEngines"*. So d47 knew which **ships and slots** wanted them and said only that it did not know
which blueprint. What is genuinely missing is one field: the blueprint name.

**It is available where the demand is built.** `PlanGap.Of` walks `build.Slots`, and a `SlotPlan`
carries its blueprint and grade — the costing is done one slot at a time precisely so the answer
knows who asked (that comment is at the fold site). Adding the blueprint to `GapDemand` is a change
to what is recorded there, not a new join.

**The open question is the wording, not the data.** *"for Bad Idea (Python) · MainEngines"* is
already long, and a fleet-wide shortfall can name a dozen demands; *"· Dirty Drive Tuning 3"* on each
would double a line that is read aloud as often as it is drawn. So decide whether the blueprint rides
every demand, or only the answer to a *"what is this for"* question — the spoken route and the panel
are not under the same pressure.

### 36. "Roll" is a word from a version of engineering that no longer exists

Asked for 2026-08-23, as the general form of the label ruling in item 35.

> "Rolls" does not speak to the commander post-non-deterministic engineering "rolls".

**The word describes a mechanic Frontier removed.** A roll was a throw of the dice — you applied
materials and got a result somewhere in a band. Engineering has not worked that way for years, and a
Commander flying today progresses a **grade** by applying materials a known number of times. d47
still says "roll" in about **twenty-five Commander-facing strings**, which makes it fluent in a
dialect its user does not speak.

**Frontier's own replacement is in the journal, and d47 already reads it.** The event is
`EngineerCraft` — *craft*, not *roll* — parsed at `src/D47.Core/Journal/ShipLoadout.cs:313`. So the
sweep does not need a word invented for it: *"three crafts to go"*, *"crafted by Selene Jean"*,
*"the grade is part crafted"*. Using the game's own noun is the same argument that settled the slot
headings and the blueprint names.

**The split that matters more than the sweep.** Three audiences, and only one of them wants
narrowing:

1. **What d47 says** — narrow. `EngineeringCapability.cs:418-419` (*"a full grade 5 is N rolls"*),
   `:1014` (*", rolled by {engineer}"*), `:1034` (*"What the roll did:"*), `:1087-1093` (*"the grade
   is part rolled"*, *"N rolls to go"*); `ChecklistEvaluator.cs:180, 229, 247, 629` and its
   `Rolls(int)` helper at `:632`; `EngineeringPlan.cs:285, 320`; `EngineerAtHand.cs:54`;
   `ShipsMode.cs:853, 1558`. **`ChecklistEvaluator.Rolls` is the one to change first** — it is the
   single place that turns a count into words, and the others read as they do because of it.
2. **What d47 listens for** — **widen, never narrow.** `EngineeringCapability.cs:49` and `:59` carry
   `"how good is my frame shift drive roll"` and `"how good is my roll"` as keyword phrases. Taking a
   word out of what d47 *hears* breaks input that works today, and a Commander who has said it for
   years will say it again. Add "craft" beside "roll"; remove neither.
3. **What the model reads** — a third audience, decided separately. `ChecklistCapability.cs:133` and
   `:260`, `EngineeringCapability.cs:45` and `:98` are tool descriptions. The model does not care,
   but its answers echo the vocabulary it is handed, so leaving these alone quietly reintroduces the
   word in spoken replies — which is most of what a Commander hears.

**Do not sweep blindly; five of the matches are correct and must survive.** `VrPose.cs:147` is roll
as an **axis of rotation**. `PushToTalkKey.cs:35` and `ListeningCapability.cs:506` are audio
**pre-roll**, and that `DocsAnchor` is a published URL fragment. `SessionSummary.cs:7` is Elite
**rolling a log file**. `OnFootCapability.cs:82` and `OnFootMode.cs:271` say *"nothing on foot is
rolled"* and *"bought outright, not rolled"* — those draw the contrast deliberately and are the
place the old word is still doing work, though they may want rewording once the rest moves.

**Internal names are out of scope**, and saying so is the point rather than laziness:
`EngineeringRules.RollsFor`, `RollModel`, `ShipLoadout.Rolled`. Renaming them touches far more code
than the strings do, changes nothing a Commander sees, and would bury the actual change in a
diff nobody can review. The arithmetic is unaffected either way — how many times materials must be
applied for a full grade is the same number whatever it is called.

**Also in scope:** `docs/capabilities/engineering.md`, `engineers.md` and `checklists.md` say it too,
and the docs gate will not catch a stale word.

**One open question.** Is *craft* right, or does the Commander have a different word for it? Frontier
uses *craft* in the journal and *Craft* on the button in the engineer's workshop, which is the
strongest evidence available — but this is a vocabulary ruling and item 35 was settled by the
Commander overruling a proposal, so the same should happen here before twenty-five strings move.

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

**Related, and it was the same fix.** A report from the same day said the Checklist *filter* did
not agree across surfaces, because `_chosen` and `_query` were instance fields on `ChecklistPage`
rather than shared state; that shipped in 0.60.8, the filter is shared and remembered and the
search text is neither. A filter is arguably "view of the tab" in the sense meant here. Decide
whether this request covers the rest of it or whether the filter was a narrower thing
that travels by the same road; do not fix them twice.
