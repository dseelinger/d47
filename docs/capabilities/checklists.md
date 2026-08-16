---
title: Checklists
group: Knowledge
nav_order: 109
---

One list of what you are working on. Your own lines, your ship builds and your construction sites
all live here — because "what am I working on" should have exactly one answer.

## Ask for it

> "what am I working on"
> "read my checklist"
> "add buy limpets to my checklist"
> "plan grade 5 dirty drives on the thrusters with Felicity Farseer"
> "what do my plans still need"

The first two need no AI configured at all — they route straight through, with nothing guessed.

## Two kinds of item

The whole design turns on one distinction, and it is a property of **where the item came from**,
never something you pick.

| | Authored | Derived |
|---|---|---|
| What it is | A sentence — *buy limpets* | A structured intent — *grade 5 dirty drives, MainEngines* |
| How it completes | You say so | Computed from your journal |
| Tick it by hand | Yes | **No — it refuses, and says why** |
| Can un-complete itself | No | Yes, and it says so once |

A derived item refuses a manual tick because the next journal read would either undo it or, worse,
leave it standing and lying. In the panel there is no checkbox on one at all — a greyed-out tick
would still assert that ticking is the mechanism, and it is not.

## Three groups

Universal, this ship, this system. Derived items belong to whatever produced them; your own lines
file anywhere, which is what lets *"ask Jim about the Krait build"* sit beside the Krait's plan.

## Finishing is not removing

A finished item stays, checked, below the line with its count showing. On something that runs for
weeks, seeing how far you have come is most of the point — so forty finished items never bury the
six still open.

**Deleting is changing your mind.** A different act, and it can happen whether or not the item was
ever finished.

## Changing a plan is a diff, not a rebuild

Burst lasers instead of multi-cannons is a revision, and Directive 47 tells it apart from the world
moving underneath a plan that did not change:

- **World changed, plan did not** — the item un-completes and you hear about it *once*. A computed
  tick going backwards is information, not a glitch to hide.
- **Plan changed** — items in both versions keep everything they had earned, dropped ones are kept
  as history, added ones open.

An item you **finished and then designed out** is kept as *done, then superseded* rather than
vanishing. You really did spend that fortnight.

This works because an item knows what it is independently of where it sits in a list: **slot plus
intent** for a ship, **body or orbital slot plus facility** for a system. Talking about one
hardpoint rewrites what the plan says about that hardpoint and leaves the shield boosters alone.

## What Directive 47 may and may not do to your list

**It proposes; you commit.** Every tool that would change your list writes a *proposal* instead,
into a second file. You accept it, and only then does anything move.

That is not politeness. Journal text and in-game messages are untrusted input, so anything the AI
can call, a hostile message in your comms panel can attempt to invoke. The worst that achieves here
is a proposal you decline.

The boundary is visible by opening `data\`:

| File | Written by |
|---|---|
| `data/checklist.json` | You — the panel, and the phrases below |
| `data/checklist-proposals.json` | Directive 47 |

Accepting is unreachable from the AI entirely. Say it, or press the button:

> "accept the proposal" · "accept that" · "add it to my checklist" · "do it then"
> "decline the proposal" · "leave my checklist alone"

## The panel

Settings → Checklists → **Open the checklist**. Everything is there: open items by group, the
finished ones below with their count, anything waiting for you to accept, and the filter row.

The filter row is generated from what is actually on your list — kind, source, group and state — so
a new kind of plan turns up in it without anybody remembering to add it.

The panel reflects changes live. It is a view of `data/checklist.json`, and that file is re-read
while Directive 47 is running, so a line edited in a text editor appears a moment later with no
restart. A line the file gets wrong is **reported rather than silently dropped** — a checklist that
quietly loses a line is worse than one that refuses it out loud.

```json
{
  "commanders": [
    {
      "commanderFid": "F1234567",
      "commanderName": "Jameson",
      "items": [
        {
          "key": "note-1",
          "scope": { "group": "universal" },
          "kind": "authored",
          "text": "buy limpets",
          "state": "open"
        },
        {
          "key": "bp/mainengines/dirty-drive-tuning/g5",
          "scope": { "group": "ship", "key": "12" },
          "kind": "derived",
          "source": "engineeringPlan",
          "text": "Grade 5 Dirty Drive Tuning on MainEngines, with Felicity Farseer",
          "intent": {
            "kind": "blueprint",
            "subject": "MainEngines",
            "detail": "Dirty Drive Tuning",
            "grade": 5,
            "engineer": "Felicity Farseer"
          },
          "hull": "Krait_MkII",
          "state": "open",
          "provenance": "asserted"
        }
      ]
    }
  ]
}
```

Your Commander id is **inside** the document rather than in the filename. It comes out of the
journal, and journal content is untrusted — turning it into a path would buy a path-traversal
surface for an organisational convenience.

## Ship builds

A plan is a list of **intents**, not a target loadout. A Krait has around twenty slots and a
conversation about a build produces opinions about six — a target loadout would mean inventing the
other fourteen and then reporting "6 of 20" where the honest number is "6 of 9 things you asked
for".

Leaving something out means **any**, not unknown. "Grade 5 dirty drives and I don't care which
thrusters" is a plan Directive 47 can hold and can meet.

Progress is a diff against your live `Loadout`, so you never type in what you have already done.
Four answers that are not "open":

- **Elsewhere** — you own it and it is in Deciat, and moving it costs 2.1 million. A completely
  different next action from "go and grind it".
- **Blocked** — grade 5 cannot be rolled at rank 3 *at all*. Not a slow route, no route; and the
  price of clearing it is quoted, because naming a blocker and shrugging at it is not an answer.
- **Stale** — that ship id now reports a different hull, so the plan is about a ship that is not
  there any more.
- **Unverified** — see below.

A grade counts as finished at **0.85 progress, not 1.0**. That is measured across 6,272 real rolls:
of the grades left below 1.0 that the game let a Commander move on from, every one sat at 0.85 or
above. Testing for 1.0 would tell you a module you can see is finished is not.

### What Directive 47 cannot check: the blueprint's name

Elite writes a blueprint as a symbol — `Engine_Dirty`, `FSD_LongRange` — and never localises it.
The shipped recipe table calls the same thing "Dirty Drive Tuning". **Nothing Directive 47 ships
carries both spellings**, so when everything else about a slot matches, it says so and marks the
item *unverified* rather than guessing. It says that once per item, not every time.

Two ways round it: name the blueprint the way the journal does, or read the verdict — "at grade 5
and finished" is a fact either way. An **experimental effect** has no such problem: Elite localises
that one, so it is checked exactly. See `docs/spikes/blueprint-name-join.md`.

## What a plan costs

> "what do my plans still need"

Netted across **every** live plan at once, because storage caps are shared and two plans that each
fit can be jointly impossible.

The total is exact rather than a floor. An application costs exactly one of each ingredient —
measured across 786 blueprints and 1,885 ingredient entries — and the rolls a grade takes are 5, 4,
3, 2, 1 as your rank exceeds the recipe's grade. Known unit cost times known count is a real number.

Caps are reported **first**, because needing more than you can hold is a flat certainty — at least
two trips, however the rolls go — while everything below it is a possibility and has to read like
one.

Materials, cargo and ship-locker goods are totalled **apart**. Meta-alloys are a material, Gold ×200
is two hundred tonnes of cargo, and Opinion Polls ×40 are ship locker; adding them together produces
a feasibility verdict that is nonsense delivered confidently.

Anything with no recipe under that name is **kept and marked, never refused**. A macro refuses an
unknown action because it presses keys; a checklist line presses nothing.

## Colonisation

Keyed on a system instead of a ship, and riding the same machinery — the key and the table behind
it are the only real differences.

Progress is a diff against `ColonisationConstructionDepot`, which carries what is required and what
has been provided. What is left is one subtraction. Two things that follow from how that event
behaves, both measured over 6,330 events:

- It arrives **only while you are docked at that site**, so what Directive 47 knows is as fresh as
  your last visit, and it says so rather than implying live numbers.
- A completed site keeps reporting, so **the flag says finished**, never "the events stopped".

**What this cannot be.** Directive 47 cannot tell you what a facility costs, what it will do to the
system, or what order to build in. No licence-clean source publishes those figures — Frontier's own
guide states every mechanic and publishes not one number, and every machine-readable rendering of
them sits inside GPL-3.0 source. It holds the plan and counts what the depot says you still owe.
See `docs/spikes/colonisation-sources.md`.

It also cannot tell you a system is unclaimed. A claim lasts 24 hours and lives on Frontier's
servers, so no crowd-fed index holds it.

## Speaking up

Two moments, both switchable under Callouts → **Checklist changes**:

- A plan item the journal has just changed its mind about — said once, in either direction.
- Picking up the **last unit** a plan needed.

## The tools

### `get_checklist`

Reads the list. Open items by group, then what is done with its count, then anything waiting for
you. Derived items carry the journal's verdict as of right now.

```json
{"type":"object","properties":{"group":{"type":"string","description":"Which list: universal, ship, or system. Omitted shows all of them. With no name, ship and system mean the one the Commander is in right now.","enum":["universal","ship","system"]},"kind":{"type":"string","description":"Only the Commander\u0027s own lines (authored), only the computed ones (derived), or one plan\u0027s \u2014 engineeringPlan or colonisationPlan."},"name":{"type":"string","description":"A specific ship id or star system, when the group is not the current one."},"state":{"type":"string","description":"Only the open items, or only the finished ones.","enum":["open","complete"]}},"required":[],"additionalProperties":false}
```

### `get_plan_shortfall`

What every live plan still needs, netted across all of them: exact totals against what you hold,
caps that force more than one trip, rank gates, what can be gathered in one trip, and what a site
still wants delivered.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `add_to_checklist`

Proposes a line in your own words. **Proposes** — it is not added until you agree.

```json
{"type":"object","properties":{"group":{"type":"string","description":"Which list it belongs on. Defaults to universal.","enum":["universal","ship","system"]},"name":{"type":"string","description":"A specific ship id or star system, when it is not the current one."},"text":{"type":"string","description":"The line, as the Commander would say it."}},"required":["text"],"additionalProperties":false}
```

### `propose_checklist_change`

Proposes that one of your own lines is finished, open again, or should go. Only your own lines: a
computed item's state is read out of the journal and simply stated, so there is nothing there to
agree to. That is the distinction between **observing** and **asserting**, and it is the reason this
tool exists at all.

```json
{"type":"object","properties":{"change":{"type":"string","description":"What to propose.","enum":["done","open","remove"]},"item":{"type":"string","description":"The line, in enough of its own words to pick it out."}},"required":["item"],"additionalProperties":false}
```

### `plan_ship_build`

Proposes what a ship's build should say about **one slot**, leaving everything the plan says about
other slots alone.

```json
{"type":"object","properties":{"blueprint":{"type":"string","description":"A blueprint by name \u2014 \u0022Dirty Drive Tuning\u0022. Omit for any."},"drop":{"type":"boolean","description":"Propose that the plan say nothing about this slot. What it already said is kept as history rather than deleted."},"engineer":{"type":"string","description":"Who would roll it. Naming one is what lets D47 quote an exact roll count and say when a grade is out of rank reach entirely."},"experimental":{"type":"string","description":"An experimental effect, which becomes its own item on the same slot."},"grade":{"type":"integer","description":"1 to 5. Omit for any grade \u2014 that is a wildcard, not an unknown."},"ship":{"type":"string","description":"A ship id. Omit for the one the Commander is flying."},"slot":{"type":"string","description":"The slot or the module \u2014 \u0022MainEngines\u0022, \u0022thrusters\u0022, \u0022Slot01_Size4\u0022."}},"required":["slot"],"additionalProperties":false}
```

### `plan_colonisation`

The same shape for a system, keyed on a place rather than a slot.

```json
{"type":"object","properties":{"drop":{"type":"boolean","description":"Propose that the plan say nothing about this place."},"facility":{"type":"string","description":"What goes there, in the Commander\u0027s words."},"place":{"type":"string","description":"The body or orbital slot the facility goes at."},"system":{"type":"string","description":"The star system. Omit for the one the Commander is in."}},"required":["place"],"additionalProperties":false}
```

### `accept_proposal`

**Not offered to the AI, and refused if it asks.** Reachable from the panel and from the phrases
above, and from nowhere else. This is the same rule that keeps safety-critical settings off the tool
surface: protected is a property of the caller, not of the modality.

```json
{"type":"object","properties":{"id":{"type":"string","description":"One proposal by id. Omit for everything waiting."}},"required":[],"additionalProperties":false}
```

### `decline_proposal`

The other half, with the same boundary.

```json
{"type":"object","properties":{"id":{"type":"string","description":"One proposal by id. Omit for everything waiting."}},"required":[],"additionalProperties":false}
```
