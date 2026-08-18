---
title: Ships
group: Knowledge
nav_order: 111
---

Your fleet, the hulls you intend to buy, and one build per ship.

## Ask for it

> "what have I planned"
> "plan grade 5 dirty drives on the thrusters"
> "plan an overcharged multi-cannon on the third hardpoint of my Corsair"
> "put that on my checklist"

The first and the last need no AI configured at all.

## The plan owns what. The checklist owns when.

These are two different questions and Directive 47 keeps them apart:

- **A build** is what a ship should be. It lives in `data/ships.json`, it has one entry per slot,
  and changing it disturbs nothing else.
- **Your checklist** is what you are working on next, in the order you put it in.

**Nothing crosses between them unasked.** Planning a slot writes the build and stops. It reaches
your checklist when you promote it — and even then it arrives as a proposal you accept, the same
way every other suggestion does.

That separation is what lets you rearrange a build without your checklist reordering itself under
you, and reorder your checklist without the build forgetting what you decided.

## One build per ship

Comparing a combat fit against an exploration fit for the same hull is a planner feature this
deliberately does not have. A slot holds one plan, because a slot holds one module.

**Changing your mind about a slot is an edit, not a delete and an add.** Swapping a long-range
pulse laser for an overcharged multi-cannon leaves you with the same third hardpoint on the same
hull — with whatever history it had. Before this, the first time you changed your mind about a
slot, everything that slot had been through was tombstoned and an identical-looking new item
opened beside the corpse.

## The fleet, and the fleet you intend

The Loadout tab opens on your fleet and answers where each ship is before you drill into anything.

**A hull you do not own is not in the fleet.** It is its own thing, with no ship id, because
Elite's id is what a ship list is keyed by and a Corsair nobody has bought has none. So
**acquiring the hull is the plan's first step** rather than a precondition sitting outside it:

```text
Corsair, intended — not bought yet
```

**Buying one adopts the plan rather than making you re-point it.** When the journal reports a new
hull of a type you had planned for, Directive 47 binds the plan to it and says so:

```text
That Corsair is yours now, and the plan you had for one is pointed at it.
```

Only when exactly one intended build matches the hull. Two Corsairs planned and one bought is a
question rather than a guess.

## Owned is derived. Intended is authored.

The same rule your checklist already draws between a line the journal settles and a line a person
does — so it looks like the same rule, because it is one.

## Dropping a build keeps what it already produced

Delete a plan and whatever it already put on your checklist **stays there**. You ordered your list
around those lines, and quietly removing them is a history that lies.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

**This capability advertises nothing, and the reason is cost as much as safety.** The advertised
tool surface is re-billed on every turn, and the largest profile — the SRV's, which carries that
vehicle's controls on top of everything else — measured **39,840 bytes against a 40,000 byte
ceiling** before this capability existed. `ToolProfiles.ComfortableBytes` says in as many words
that raising the number a third time is the wrong answer.

So the one route that genuinely needs a model to understand free English is
[`plan_ship_build`](checklists.md), which already existed and now writes to the build rather than
proposing straight to the checklist. Everything below is `Protected`: reachable from the panel and
from a phrase, and never from the model.

### `get_ship_plans`

Every ship the Commander owns and every hull they intend to buy, with where each one is and how
many slots its build has an opinion about.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Not `get_fleet`, which `JournalCapability` already has and which answers a different question:
that one reports what the journal saw in the racks, and this one reports what the Commander means
to do about it.

### `promote_ship_plan`

Offer a ship's build to the checklist. It is a proposal: the Commander accepts, and one planned
change produces the modification plus whatever unlocking and ranking it needs.

```json
{"type":"object","properties":{"ship":{"type":"string","description":"Which ship, by name or hull. Omit for the one the Commander is flying."}},"required":[],"additionalProperties":false}
```

**Promotion is one-to-many.** `EngineeringPlan` already emits an `EngineerAccess` step beside a
modification, so promoting one planned change produces several lines — and each carries the slot
that caused it in its intent, which is what lets a later revision find them again.

### `drop_ship_plan`

Drop a ship's build. The Commander's own act: not offered to the model, and refused if it asks.
What the plan already put on the checklist is kept.

```json
{"type":"object","properties":{"ship":{"type":"string","description":"Which ship, by name or hull. Omit for the one the Commander is flying."}},"required":[],"additionalProperties":false}
```

### The file

```json
{
  "ships": [
    {
      "id": "ship-1",
      "hull": "python",
      "shipId": 12,
      "name": "Bad Idea",
      "slots": [
        {
          "slot": "MainEngines",
          "blueprint": "Dirty Drive Tuning",
          "grade": 5,
          "engineer": "Felicity Farseer",
          "experimental": "Drag Drives"
        }
      ]
    }
  ]
}
```

`id` is the build's own identity and is **independent of `shipId` from the moment it is created** —
that independence is what there is to rebind when the hull is bought. A build with no `shipId` is
an intended one.

Hand-edited, it takes effect without a restart, and a line the file gets wrong is reported rather
than silently dropped.

</details>
