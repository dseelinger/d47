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

## 38 — The slot list becomes a table: what is there, and what you wanted

Asked for 2026-08-24, after an evening of the Loadout tab being read wrongly in three different
ways. The request: **Current and Plan side by side, per line** — one row per slot with two columns
on it, not two groups for the same ship.

**This overturns a stated Phase 26 ruling rather than filling a gap**, and the ruling is in the
item's own words. `list.md` Phase 26, *"What is fitted and what you want"*:

> the slot list an **index rather than a table**: one line each, a mark where a plan exists, and
> everything else in the pane

So the product description already promises both facts. What is being overturned is the *shape*:
an index with a mark, rather than a table with two columns. **That shape is what produced the
confusion.** "A mark where a plan exists" is the orange dot, and one line carrying two facts is what
let a planned Shield Booster in an empty mount draw exactly like the five fitted ones beside it —
reported as *"something IS fitted on oxen utility mount 8"* when nothing was.

`ShipsMode.Parted` is where it lives, and its own comment is the reasoning being overturned:

> **The plan first, and what is fitted where there is no plan.** This was the other way round for a
> day — fitted first, on the argument that what is in the slot is a fact and a plan is only a want —
> and the argument is sound about the *slot* and wrong about *this row*.

Both readings of that argument are right and neither survives one column. `Planned(plan) ?? fitted`
has to pick, and either choice is a row describing something that is not there. Two columns is the
answer that was unavailable while the row was an index.

### The shape

```text
SLOT          CURRENT                              PLAN
Utility 2     SB                                   Heavy Duty G5 · Super Capacitor
Utility 8     —                                    SB · Heavy Duty G5 · Super Capacitor
Military 1    HRP · Heavy Duty G5 · Deep Plating   ✓
Comp 4 (5)    —                                    HRP · Heavy Duty G5 · Deep Plating
PowerDist     7A · Priority Systems G5             Weapon Focused G5                     ⚠
Comp 6 (5)    HRP · Heavy Duty G5 · Deep Plating   (no plan)
```

Three rules carry it:

- **Current is the journal and Plan is `ships.json`, and neither ever borrows from the other.** That
  one rule closes four of the five defects this evening produced, including both of the ones that
  made a Commander believe modules were fitted that were not.
- **Agreement collapses.** Where the plan is met the second column says so and stops; repeating
  identical words in two columns is noise. This is also the answer to *"these have been engineered,
  the orange circles should be gone, right?"* — the marker becomes **disagreement** rather than
  *a plan exists*, which is the thing worth an eye-catching colour.
- **Four states where the row has two.** Nothing planned · planned and met · planned and not rolled ·
  **planned and the slot is empty**. The fourth has no representation at all today, and it is the
  one that misled.

### Shorter names, and a sharper version of the same idea

Two columns will not fit today's vocabulary, so the request comes with abbreviation: `HRP` for Hull
Reinforcement Package, `SB` for Shield Booster.

**And the blueprint usually repeats the module.** *Heavy Duty Hull Reinforcement* sits on a row
already saying *Hull Reinforcement Package*; the module is said twice and neither saying is short.
Strip the module out of the blueprint and it reads *Heavy Duty* — shorter, and **comparable down the
column**, so "this whole ship is Heavy Duty" becomes something an eye can see rather than something
to be worked out line by line.

The short names are d47's own words rather than Frontier's, so they are a table this repo authors —
which is a different licence question from the specification data and a lighter one.

### Open questions — these want answers before the code

1. **`PD` collides.** Point Defence and Power Distributor. Either a rule that breaks the tie, or core
   internals stay unabbreviated — there are only eight of them and each is unique on a hull, so the
   pressure for short names is really about the optional and utility lists.
2. **Is a short name ever the only name?** The glyph work of the same evening kept every word it
   removed on the tooltip and the accessible name, on the grounds that a picture is only an
   improvement while the word is still reachable. The same test applies here, and the natural home
   for the long form is the slot drill.
3. **Does the mini panel get this?** Two columns do not fit 512 pixels. Current-only is one answer;
   **only the rows that disagree** is another, and is arguably the more useful mini view of a ship
   in any case — it is the question a Commander at a workshop is actually asking.

### What this is not

**Not the defects.** Five were found the same evening and are GitHub Issues, because they are bugs:
a plan drawn as though fitted, a marker that never clears, a raw blueprint symbol on a fitted-only
row, an engineer offered another module's blueprint, and an engineer offered work on an empty slot.
This entry is the shape those defects made visible, not the defects — and **several of them are
worth fixing before this lands**, because a table drawn from the same conflated source would inherit
them.

**Not the checklist.** The checklist's wording was correct throughout the evening that produced this
and is not in scope; what changed is that it was being read against a page that disagreed with it.
