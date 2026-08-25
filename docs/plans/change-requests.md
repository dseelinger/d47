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
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. Everything through 38 has
shipped and been pruned, so **the next number is 43** — the count is not the length of this file.
41 is taken by an entry on a branch that has not merged yet, which is exactly why the number is
written down rather than counted: two branches counting entries would both have arrived at 41.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## Open

## 39 — The engineer filter should cover the fleet, not the ship you are in

Asked for 2026-08-23, in the Commander's own words:

> All checklist items fulfillable by the Engineer should appear for that filter, not just for the
> ship I'm in.
>
> If that's undoable, then at least notice when I switch ships and re-filter for the new ship — but
> I'd rather the previous bullet be implemented instead.

**The fallback is named as the lesser option and should be read that way.** Re-filtering on a ship
switch is a consolation prize for a filter that cannot see past the cockpit; it is not the ask, and
building it instead would close the entry without answering it.

`ChecklistService.FilterAxes` offers the row — *"What {engineer} can do here"* — and
`EngineersHere.For(live, State)` decides what is under it, per engineer, in `EngineerAtHand`.

### The ground moved after this was reported, and that is the first thing to check

Two changes landed between the ask and this entry, both of which touch exactly this code, so
**reproduce it before designing anything**:

- **Other ships' modules became readable.** `EngineerAtHand.LoadoutFor` already answers with the
  remembered loadout for a ship the Commander is not in — *"the live one for the ship being flown …
  the remembered one for any other ship"* — which is the per-ship loadout memory shipped in v0.41.1.
  Whatever was scoping the filter to one cockpit in August, it was not a lack of data about the
  others.
- **A fitted gate was added on 2026-08-25** (GitHub issue 41), because every line offered under
  *"What Selene Jean can do here"* was about a slot with nothing in it. `IsFitted` passes an item
  whose ship has **never been seen** — `LoadoutFor(…) is null` returns true — so it is permissive
  rather than restrictive about the rest of the fleet.

So the entry may already be half-answered, or the restriction may live somewhere neither of those
touched. **Open question, and it wants measuring rather than arguing**: with a plan on a stored ship
and its engineer in the current system, does that line appear under the filter today?

### What it must not become

**Not a filter that offers work the Commander cannot do.** The three bands `EngineerAtHand` keeps
apart — ready, out of rank, and partial — exist because folding them together was reported as noise,
and widening the filter across the fleet must not quietly widen it across those bands too. A line
about a ship in another system is still work; it is *"fly there first"* work, and if the filter grows
to include it, the row has to say which ship it is about or it becomes a list of errands with no
addresses.

---

## 40 — Voices that suit the names they are speaking

Asked for 2026-08-23, queued behind Phase 54 for discussion: **multilingual and ethnic voices that
match NPC names where appropriate.** An engineer called Hera Tani, Marco Qwent or Etienne Dorn read
in the same flat English as everyone else is a small thing that adds up over a hundred hours.

`VoicePairing` is where a voice is chosen, and it already asks a model to do the choosing, so the
question is what it is allowed to choose *from* and what it is allowed to say about language.

### This contradicts two stated rulings, and both are in the source

The file's own rule is that a request overturning a comment says so, because leaving the comment
standing beside code that no longer obeys it turns the file into a liar. Two comments are in the way,
and **neither is wrong** — they are answers to a question this request asks differently.

**Turbo 2.5 rather than Multilingual 2, and the reason is language.** `ElevenLabsTtsProvider`
records it: Multilingual 2 *"infers the language of every line from the line, which is the behaviour
it is built for and which produced a material milestone read half in German."* The 2.5 generation
accepts `language_code` and holds it; Multilingual 2 rejects the parameter outright, so there was no
version of pinning English that kept that model. It is also half the price. **A request for voices
that sound like their names is not a request for lines that switch language mid-sentence**, and the
difference between those two is the whole of this entry's design problem.

**`Language = "en"`, fixed, and the argument behind it is about untrusted input.** The comment: *"the
one thing an in-game message can do to this path is arrive in another language — which is not a
reason to let it choose the voice's."* Anything here that lets the spoken language follow the text
has to answer that, because in-game comms are untrusted input by invariant. An accent chosen from a
**name d47 already knows** — an engineer from the shipped directory — is a different proposition from
a language inferred from a line, and that distinction is probably where the answer lives.

### Open questions — these want answers before the code

1. **Accent or language?** A voice with a French accent reading English is not the same feature as a
   line spoken in French, and only the first is obviously wanted. If it is accent only, the model
   pin may not need to move at all — it becomes a casting question rather than a synthesis one.
2. **Which names?** The engineers are a shipped table with known origins. Station and system names
   are not, and a Commander's own invented ship names certainly are not. Naming the source list is
   most of the scope.
3. **What happens on the other providers?** Edge Neural is the free path and most Commanders' path,
   and its voice list is its own. A feature that only exists on the paid provider needs to say so on
   the row rather than being discovered.

### What this is not

**Not accents for the Commander, and not for d47 itself.** The personas are written in English and
cast deliberately; this is about the voices d47 *quotes*, not the voice it *has*. And **not
translation** — nothing here proposes that d47 speak anything but English to the Commander.

---

## 42 — The headset's mini panel should carry no buttons either

Asked for 2026-08-25, in the Commander's own words:

> VR mini-panel should likewise not have any buttons on it, similar to the 2d panel.

**The mechanism already exists and is one style rule.** `PanelView.axaml` declares
`panel|PanelView.output-only Button { IsVisible: False }`, and `OverlayPanel` adds the
`output-only` class to the flat 2D overlay. Its comment states the case this request extends —
*"nothing can be clicked, and it makes more room for the data"* — and explains why the selector is
an exact `Button` rather than `:is(Button)`: a `CheckBox` shows whether a checklist line is done
and a `RepeatButton` is half a scrollbar, so removing those would take away the data rather than
make room for it. **Nothing about that reasoning needs revisiting**; the request is to point it at
one more surface.

### The one thing that makes it more than a line

`VrPanelSurface` holds **a single `PanelView`** and flips `Mode` between `PanelMode.Mini` and
`PanelMode.Full`. So the class cannot simply be added at construction: that would strip the buttons
from the **big** headset panel as well, which is the one surface where they are genuinely pressable
— the headset drives `PanelView` through a geometric hit test and the ray can hit them.

So it is a class toggled with the mode rather than set once, and the negative half is what the test
should assert: **mini loses its buttons and full keeps them.**

### Why room matters more here than on the flat overlay

Mini is **512 pixels wide**, chosen because that is the lever on apparent text size rather than
because it is a comfortable amount of room. Every button on it is space taken from the transcript
tail and the provenance line, which is what mini exists to show. The flat overlay had the same
argument at a more forgiving size.

### Where the code is

- `PanelView.axaml` — the `output-only` style. Untouched.
- `OverlayPanel.cs` — `_view.Classes.Add("output-only")`, the precedent to follow.
- `VrPanelSurface.cs` — one `PanelView` at construction, `Mode`, `Surface` and `Slot` all keyed on
  `PanelMode.Mini`. The class belongs wherever `Mode` settles.

### What it is not

**Not the big headset panel.** Its buttons are reachable by the ray and stay.

**Not a change to what mini shows.** The tabs, the strip and the chrome are already gone in mini
by other means; this is about the controls a furnished page brings with it — the checklist's
filter and Add, the engineer pages, the clocks — which is exactly the set the flat overlay's
comment enumerates.
