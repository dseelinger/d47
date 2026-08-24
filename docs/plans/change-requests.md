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
shipped and been pruned, so **the next number is 37** — the count is not the length of this file.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## Open

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

### 35. A checkbox for work the engineer cannot finish

Asked for 2026-08-23, immediately after the grade fix in that filter landed, and **simplified the
same evening** from a "Minimum Grade" stepper to this:

> Checkbox beside "Showing what Lei Cheung can do here" labeled "Include items that cannot be fully
> engineered" or something like that. It's a lot of words especially with all the other buttons, too.

**This does not add a rule; it re-opens a door the grade fix closed.** That fix made a line appear
under an engineer only where they offer the blueprint **at the grade the line asks for**, so a Grade
5 Heavy Duty on a Shield Booster no longer shows for Lei Cheung, who tops out at grade 3. But he
genuinely can take that booster from 0 to 3 — real work, at a workshop the Commander is standing in.
Excluding it outright was a judgement call recorded at the time as overrulable, and this overrules
it. Unchecked stays the default and stays exactly what shipped.

**So the clause is about the engineer's ceiling, not the item's grade** — which is the distinction
that makes it more than a second filter. Checked means: also show lines this engineer can *advance*
but not complete. `BlueprintCatalogue.Named(...)` already returns every grade of the matched
blueprint, so the engineer's ceiling is `rows.Where(has this engineer).Max(r => r.Grade)` and needs
no new lookup.

**And it wants a third band rather than a mixed list.** `EngineerAtHand` already separates `Ready`
from `OutOfRank` because *"one is work, the other is a reason to go and do some of their other work
first"*. This is a third kind again — work that can be started here and finished elsewhere — and
folding it into `Ready` would undo the very report that produced the grade fix, because those lines
would once more read as work Lei Cheung can do. Say how far they go on the line itself: *"Lei Cheung
takes this to 3 of 5."*

**Where it goes.** `ChecklistPage`, beside the scope chooser, **shown only while the engineer filter
is the chosen one** — the phrase means nothing against a list filtered by ship or by source, and the
row is already crowded.

**The label is settled: "Include Partial Grades".** Ruled 2026-08-23. *"Include partial rolls"* was
proposed and **rejected by the Commander**, and the reason is worth recording because it is a fact
about the game rather than a preference: *"'Rolls' does not speak to the commander post-
non-deterministic engineering rolls."* A roll was a throw of the dice, and engineering stopped
working that way — a Commander flying today progresses a **grade**, so that is the noun the control
must use. Anything shorter that keeps *partial* and *grade* is acceptable; nothing that reintroduces
*roll* is.

**And the Help must explain the difference** — required by the same ruling, not an extra. The two
readings of this page are a sentence apart and a Commander cannot be expected to infer which is
showing:

- **unchecked** — what this engineer can take all the way to the grade the line asks for;
- **checked** — *and* lines they can only take part of the way, which somebody else must finish.

`docs/capabilities/checklist.md` is where that lands, since help is about the page rather than the
subject and the row hangs off this capability. The line itself should carry the same fact where it
applies — *"Lei Cheung takes this to 3 of 5"* — so the answer is on screen as well as in the help.

**One open question left.** Does the setting persist? `bugs.md` carries a report that the chosen
filter itself does not survive a restart and does not agree across surfaces. This is the same kind
of state and should travel the same road rather than growing a second one — decide both together,
and see that entry for why `ViewState` rather than `settings.json` is the likely home.

**Experimentals stop being a question here**, which they were not under the stepper. They carry no
grade, are bought outright, and are applied at any standing — so an engineer either offers one or
does not, and there is no partial case to include or exclude.

**Noticed while settling the label, and deliberately not folded in: d47 says "roll" to the Commander
in about fourteen places.** If the word no longer speaks to a Commander flying today, this control
is not the only place that matters — `EngineeringCapability` alone says *"a full grade 5 is N
rolls"*, *"What the roll did"*, *"The grade is part rolled"* and *", rolled by {engineer}"*. Two
things keep it out of this entry rather than in it. It is a sweep across a capability nobody
reported, so it wants its own decision. And **the split matters more than the sweep**: the phrases a
Commander *says* — `"how good is my frame shift drive roll"`, `"how good is my roll"` — are keyword
vocabulary, and taking a word out of what d47 *listens* for breaks input that works today. Widen
what it hears; only narrow what it says.

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
