---
title: Adventures
group: Knowledge
nav_order: 115
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a story that runs while you fly.</p>
<section>
<h2><span class="num">1</span> Ask for one, in the Stories tab or out loud.</h2>
<p class="say">Say "ask for an adventure" and say what it should be about, or pick one in the Stories tab.</p>
<p class="expect">D47 writes a draft and waits for your yes. Accept it and you hear the first objective.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a request for an adventure typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">ask for an adventure</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Or press Stories in the tab strip and pick one there.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Either way D47 writes a draft and waits for your yes.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Fly. The next objective arrives when your journal earns it.</h2>
<p class="say">Fly as you normally would, and listen for the next objective.</p>
<p class="expect">When your journal shows a jump, dock or scan that earns it, your ship AI speaks the next objective.</p>
<svg viewBox="0 0 880 168" role="img" aria-label="A jump or a docking in the journal moves the story to its next objective">
 <rect x="20" y="24" width="250" height="72" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOU JUMP</text>
 <text x="145" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">or dock, or scan</text>
 <line x1="282" y1="60" x2="306" y2="60" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="320,60 304,52 304,68" fill="var(--accent-muted)"/>
 <rect x="334" y="24" width="526" height="72" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="597" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE NEXT OBJECTIVE IS SPOKEN</text>
 <text x="597" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">in your ship AI's own voice</text>
 <text x="20" y="146" font-size="16" fill="var(--text-muted)">Nothing is on a timer. Say "where am I up to" to hear the story so far.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Say "next" if the story has not moved and you want it moved on.</p>
<p class="expect">The story advances; with the game in a menu it does nothing until the game moves.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="A story only moves when Elite writes something to the journal">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">A story moves when the game does.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Sitting in the menu, nothing happens. Say "next" if you want it moved on anyway.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A story you fly, told by the ship's AI, moved along by your own journal.</p>
<section>
<h2><span class="num">1</span> A story, not a list of stops.</h2>
<svg viewBox="0 0 880 300" role="img" aria-label="A spine of premise, want, stake, turn and ending, with objectives hung on real places">
 <rect x="20" y="20" width="360" height="216" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="52" y="58" font-size="19" font-weight="800" fill="var(--accent)">THE SPINE</text>
 <text x="52" y="96" font-size="16" fill="var(--text)">what it is about</text>
 <text x="52" y="126" font-size="16" fill="var(--text)">what you want in it</text>
 <text x="52" y="156" font-size="16" fill="var(--text)">what is really at stake</text>
 <text x="52" y="186" font-size="16" fill="var(--text)">where it turns</text>
 <text x="52" y="216" font-size="16" fill="var(--text)">what the end means</text>
 <line x1="398" y1="128" x2="428" y2="128" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="442,128 426,120 426,136" fill="var(--accent-muted)"/>
 <rect x="458" y="20" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="56" font-size="16" fill="var(--text)">an objective, standing on a real place</text>
 <rect x="458" y="92" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="128" font-size="16" fill="var(--text)">an objective, standing on a real place</text>
 <rect x="458" y="164" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="200" font-size="16" fill="var(--text)">an objective, standing on a real place</text>
 <text x="440" y="268" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">The shape is written first. The places are where that shape can stand.</text>
 <text x="440" y="294" text-anchor="middle" font-size="16" fill="var(--text-muted)">Which is why D47 is never asked for five stops — it is asked for a story.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Your journal moves it. There is nothing to tick.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="An objective fires when you reach its place, and nothing before you began counts">
 <rect x="20" y="30" width="250" height="86" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="145" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">YOU BEGIN IT</text>
 <text x="145" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">the clock starts here</text>
 <line x1="282" y1="73" x2="308" y2="73" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="322,73 306,65 306,81" fill="var(--accent-muted)"/>
 <rect x="334" y="30" width="250" height="86" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="459" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">YOU FLY THERE</text>
 <text x="459" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">arrive, dock, land or scan</text>
 <line x1="596" y1="73" x2="622" y2="73" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="636,73 620,65 620,81" fill="var(--accent-muted)"/>
 <rect x="648" y="30" width="212" height="86" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="754" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">IT SPEAKS</text>
 <text x="754" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">and says where next</text>
 <rect x="20" y="148" width="840" height="60" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="185" text-anchor="middle" font-size="16" fill="var(--text)">Nothing you did before you began counts, and only the current objective can fire.</text>
 <text x="440" y="242" text-anchor="middle" font-size="16" fill="var(--text-muted)">Fly with D47 closed and it catches up when you start it. Wander off and the story waits —</text>
 <text x="440" y="264" text-anchor="middle" font-size="16" fill="var(--text-muted)">going somewhere else is what a sandbox is for.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> It cannot spoil itself.</h2>
<svg viewBox="0 0 880 274" role="img" aria-label="What the ship's AI is told, and when">
 <rect x="20" y="24" width="840" height="62" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="56" y="62" font-size="18" font-weight="700" fill="var(--accent)">always</text>
 <text x="824" y="62" text-anchor="end" font-size="16" fill="var(--text)">the premise, what you want, what is at stake</text>
 <rect x="20" y="98" width="840" height="62" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="56" y="136" font-size="18" font-weight="700" fill="var(--text)">once it has happened</text>
 <text x="824" y="136" text-anchor="end" font-size="16" fill="var(--text)">the turn, and what the ending meant</text>
 <rect x="20" y="172" width="840" height="62" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="56" y="210" font-size="18" font-weight="700" fill="var(--danger)">never</text>
 <text x="824" y="210" text-anchor="end" font-size="16" fill="var(--text)">the objectives ahead of you</text>
 <text x="440" y="268" text-anchor="middle" font-size="16" fill="var(--text-muted)">A storyteller who knows the ending leaks it. So the AI is simply never told what is coming.</text>
</svg>
<p class="body">Foreshadowing still happens — it is written <em>into</em> the earlier objectives, by the turn that did know the ending. Between objectives the ship's AI wonders aloud in character and never states a new fact about the story, so nothing it says on a quiet stretch can contradict an objective you have not reached.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="persona.html"><span class="ct">Persona →</span><span class="cd">Whose story it is — each core writes the one it cares about.</span></a>
<a class="card" href="galaxy.html"><span class="ct">Galaxy search →</span><span class="cd">The catalogue a generated story picks its places from.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">The other thing that remembers what you are in the middle of.</span></a>
</div>
</div>
</div></div>

## The details

An adventure is a story: someone wants something, there is a belief the events exist to test, a
turn where it stops being what it looked like, and an ending that means something. It is told by
the ship's AI, anchored to the galaxy by objectives, accepted by you, and advanced by your own journal.

The drive behind it is to add story to a sandbox, which sandboxes deeply lack. It is deliberately
**not** a checklist of things to complete.

### How to have one

**Ask for one** — a short form and D47 writes it, in the voice of whichever core is aboard. Three
choosers, each with a default, so pressing *Go* on an untouched form is a complete ask:

- **Reach** — how far the story may go: *near here*, *a session's flying*, *anywhere*. Turned into
  light years by what you can actually move — your ship's jump range, or your carrier's if you have
  one.
- **Length** — which structure. *Short* is three objectives, setup and turn and resolution. *An evening*
  is five. *Long* is eight or more. The count follows from the structure rather than the other way
  round.
- **Using** — *this ship only*, or *anything I own*. Shown only when you have a choice.

And one optional thing said: a brief — a theme, a mood, a place it must include. Empty is fine.

Say "ask for an adventure" and D47 opens this form and the entry for the brief. Say the brief, or
press **Done** with it empty, and it asks with the form as it stands, as *Go* does. When a language model or galaxy
search is missing, the form opens and D47 says which instead.

**Things it reads rather than asks**, because asking would be asking you to describe your own
ships: your fleet and what each hull can do, whether you have a carrier and where it is, where you
are, who is aboard, and your ranks.

**Write the next chapter** — on a finished adventure's page, the same form for the story that
follows it. The AI reads the finished one in full — its spine, its objectives and what was said as you
flew it — and each chapter before that by name and premise only. The new story's want follows from
how the last one turned and ended, and its stake is the belief the last one left open. The draft
card says which adventure it follows, and it is accepted, flown and checked like any other.

#### `ask_for_adventure`

Open the Ask page and the entry for a brief; committing the brief asks for the adventure, as Go does. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### What an objective can be

Thirty-four triggers, and every one is a comparison on a structured field rather than on a name:

| Trigger | Matched on | Never on |
|---|---|---|
| Arrive at a system | the system's id | its name |
| Dock at a station | the station's market id | its name — a carrier's name is player-chosen |
| Land on a body | system and body ids | the body's name |
| Scan a body | system and body ids | the body's name |
| Reach a rank | career and a number | any rank word |
| Board a ship | the hull's type | the ship's name |
| Scan a Guardian beacon | a data-link scan while in a Guardian beacon system's id | the scan's type, or any name |
| Collect bounties | `Bounty` events | anything else |
| Earn kill bonds | `FactionKillBond` events, optionally for one `AwardingFaction` | the faction fought |
| Complete missions | `MissionCompleted` events, optionally for one `Faction` and a mission family that starts the internal `Name`, such as `Mission_Courier` | the mission's title |
| Sell tons | `Count` in `MarketSell`, optionally of one `Type` or at one `MarketID` | the commodity's display name |
| Refine tons | one ton per `MiningRefined`, optionally of one `Type` | the commodity's display name |
| Step out on foot | `Disembark` onto a planet, not a station, optionally on one body | anything else |
| Collect items | `Count` in `CollectItems`, optionally of one `Type` or `Name` | the item's display name |
| Analyse organics | `ScanOrganic` with `ScanType` `Analyse`, optionally of one `Genus` | `Log` and `Sample` scans |
| Map bodies | `SAAScanComplete`, optionally of one body | anything else |
| Survey signals | `SAASignalsFound` with a `Signals` entry of one type, such as `Thargoid` or `Platinum` | the body's name |
| Land at a wreck | `Touchdown` at a `$Settlement_Unflattened_Wrecked…` destination, of one type; unfiltered, only `Unknown`, a crashed Thargoid ship | the destination's display name |
| Log codex entries | `CodexEntry`, optionally of one `Category` | the entry's name |
| Sell data | credits from `SellExplorationData`, `MultiSellExplorationData` and `SellOrganicData`, optionally one of the two kinds | anything else |
| Salvage cargo | `CollectCargo`, optionally of one `Type` | the cargo's display name |
| Drop into a signal source | `USSDrop`, optionally of one `USSType` | the source's display name |
| Rescue | `Count` in `SearchAndRescue`, optionally of one `Name` | anything else |
| Reach an engineer stage | `EngineerProgress` for one `Engineer` at `Invited` or `Unlocked`, either form; an on-foot engineer appears only in the startup list | the engineer's display name |
| Launch the SRV | `LaunchSRV` events | anything else |
| Hire crew | `CrewHire` events | anything else |
| Apply suit mods | each mod in `SuitMods` for a `SuitID`, or in `WeaponMods` for a `SuitModuleID`, that the previous `SuitLoadout` for it lacked, optionally one mod name such as `suit_nightvision` | anything else |
| Change livery | a `Loadout` whose cosmetic slots (`PaintJob`, `Decal1` to `Decal3`, `ShipName0`, `ShipName1`, `ShipID0`, `ShipID1`, `EngineColour`, `WeaponColour`, `StringLights`, `VesselVoice`, `Bobble01` to `Bobble10`, `ShipKit*`) differ from the previous `Loadout` for the same `ShipID` | anything else |
| Buy a fleet carrier | a `CarrierBuy` event, not for a squadron carrier | anything else |
| Jump the fleet carrier | a `CarrierLocation` whose `SystemAddress` differs from the last one seen for the same `CarrierID`, for a carrier whose `CarrierType` is not `SquadronCarrier` | the carrier's name |
| Join a wing | `WingJoin` or `WingAdd` events | the other player's name |
| Join another Commander's crew | `JoinACrew` events | the captain's name |
| Join a squadron | a `JoinedSquadron` event | the squadron's name |
| Found a squadron | a `SquadronCreated` event | the squadron's name |
| Take part in a conflict | a `FactionKillBond` whose `AwardingFaction` is one side of an active or pending war or civil war in the system the Commander is in; or a `MissionCompleted` for one side of an active election whose `FactionEffects` mark influence in the election's system. Optionally one `WarType` (`war`, `civilwar` or `election`) and one side | anything else |
| Work for a faction | `+` marks in the `FactionEffects` of a `MissionCompleted` for one faction, optionally in one `SystemAddress`; `++++` counts as four | anything else |

Only a stock story's chapter one uses the beacon trigger, as its last objective, and you cannot add one on the
form.

Every trigger after the seventh is counted except the engineer stage, which fires once. A counted objective fires when its total reaches its count, and only what happens
after the objective before it has fired counts. It has no place of its own; a chapter that needs it done
somewhere puts an arrive or dock objective there first. The card shows the running total, such as "Kill bonds
for LTT 7786 Labour: 3 of 8", and a catch-up after d47 was closed rebuilds it from the journal. These
mission families are set aside, and no objective uses or counts them: `Mission_Massacre_Skimmer`,
`Mission_Disable`, `Mission_Hack`, `Mission_OnFoot_Hack`, `Mission_Scan`, `Mission_RS_` and `Mission_DS_`.
A story steers illegal missions to Anarchy space but does not enforce it. Illegal families are any whose name contains `Illegal`, `Mission_OnFoot_Heist` and `Mission_OnFoot_Sabotage`. The chapter writer is given up to five Anarchy systems within reach and must put an illegal mission objective directly after an arrive or dock objective in one of them, or use no illegal family when none is in reach. Any completion of the family still counts, whatever its target. While an illegal mission objective is current, approaching the target settlement of a live mission of that family, when a faction other than Anarchy runs it, makes d47 say once per mission that the job is a crime there.
A suit mod or livery change is seen when the next `SuitLoadout` or `Loadout` is written, so the objective may fire some minutes after the change. A suit, weapon or ship with no earlier loadout is only remembered: its first loadout counts nothing. A livery objective names no paint job, kit or decal. The first `CarrierLocation` seen for a carrier sets the baseline and counts nothing; a squadron carrier's is ignored. No objective may need an ARX purchase, because the journal cannot show one; the chapter writer is told so.

**Conflict and faction objectives.** A conflict objective counts taking part, not winning: a conflict lasts days and other players move it. The side is the faction the first counted contribution went to, and contributions to the other side do not count. A bond in a system with no active or pending conflict does not count. d47 reads the conflicts from the `Conflicts` of `FSDJump`, `Location` and `CarrierJump`, as they stood when the Commander last arrived, so a war that began after that arrival is not seen until the next one. When a later arrival shows the conflict ended (an empty `Status`), the standing records whether the side with more `WonDays` was the Commander's, and the next chapter's brief says so. For a faction objective, the next chapter's brief says whether the faction's influence rose or fell between two visits a day apart. The writer is told both kinds and up to eight active or pending conflicts in systems the Commander has visited; a conflict objective may name no faction and leave the Commander to find one. A faction objective counts mission marks only; trade, data sales and bounties do not count as faction work.

**Carrier, squadron and team objectives.** The chapter writer is told about a kind only where the Commander can do it, and a chapter that contains one it was not told about is refused:

| Kind | Allowed when |
|---|---|
| `carrierbuy` (once) | 7,000,000,000 credits at the last load (the carrier costs 5,000,000,000, so its reserve rule needs only 5,500,000,000), and no fleet carrier owned |
| `carrierjump` (counted) | a fleet carrier owned |
| `wing` and `multicrew` (counted) | always |
| `squadron` (once) | not in a squadron |
| `squadronfound` (once) | 20,000,000 credits at the last load: the 10,000,000 it costs and a reserve of the same; and not in a squadron |

A Commander is in a squadron from a `SquadronStartup`, `JoinedSquadron` or `SquadronCreated` until a `LeftSquadron`, `KickedFromSquadron` or `DisbandedSquadron`, or until the next `LoadGame` without a `SquadronStartup`. A wing or multicrew objective needs another player, so the writer is told the Commander can refuse it, and neither is ever the comfort-zone activity.
The form offers the first six kinds and shows any other objective without changing it. A text filter ignores case and any `$…;` wrapping, so `Tritium` and `tritium` are one type, and `Thargoid` matches `$SAA_SignalType_Thargoid;`. An engineer objective at `Invited` is also met by `Unlocked`, and by the startup list that names every engineer. An on-foot engineer is written only in that startup list, so an engineer objective naming one fires at the first login after the stage is reached, not at the moment.

Nothing a stranger can choose — a ship name, an in-game message, a mission title — can be a
trigger. That is the safety property stated as a type rather than as a promise.

### People may be invented. Places may not.

A story needs people and the galaxy's named ones are few, so the AI may invent a contact, a rival,
a voice on a wreck's log. It may not invent a system, a station, a body, a faction, a Power or a
game mechanic — every place in a generated story is resolved against the galaxy before you are
offered it, and a miss refuses the whole draft by name.

**No stop needs a permit.** Your journal does not say which permits you hold, so a generated story
never sends you to a permit-locked system such as Shinrarta Dezhra or Sol, unless you are already in it.

**Invented people are told about, never met.** The game has no act for meeting anyone, and the only
thing you can actually do in a story is fly to the next objective. Ask the core whether one of them was
real and it says they are someone in the story.

### Where you are in it, and what it says

An objective speaks when it fires, and hands over to the next one in the same breath:

```text
The log's last entry is dated the day the beacon went quiet.
Next: dock at Maren Anchorage in Dyson's Hollow.
```

The hand-off names the place and the act and nothing else — never the next objective's title or its
line, which is the spoiler rule holding. A scan's hand-off says *how*, because "scan X" sent a
Commander looking for a detailed surface scanner when the ship's own scanner was what the story
meant. It also says that going there counts: a body already scanned writes no second `Scan`, so a
scan objective is satisfied by the approach as well, and a story cannot strand a Commander on somewhere
they had already been.

**No counts where you read.** The card says the story's name and where it is — *not yet begun*, the
current objective's title, or *finished*. *Objective 3 of 7* is checklist language and stays off the card.

### Stopping, and starting again

**Abandon** a begun adventure and it stops telling you: no objective fires, the AI drops it from what it
knows, and an objective waiting out its settle window is discarded. The record stays, folded away at the
foot of the list with what it reached. **Begin again** on an abandoned one starts from the opening
with a fresh stamp — nothing that happened in the gap counts, because a start is a start.

**Remove** deletes the record. For a begun one it asks first, because an adventure three objectives in
is work you did.

Both are yours alone, reachable from the panel and nowhere else.

### Nothing here is callable by the model

Generation, beginning, abandoning and removing are all your acts, on the panel. Asking for an adventure,
and accepting, changing and rejecting a draft, are yours too, by voice or by their buttons, and so are pausing and resuming
a stock story, by voice or by the **Story on** checkbox. The ship's AI can
*read* the story — that is what it is for, so it can play off it — and can change nothing about
it. A hostile message arriving in your comms panel cannot propose a story, end one, or delete one.

It also costs nothing: none of this is on the advertised tool surface.

### Accepting, changing or rejecting a draft by voice

While a generated adventure waits for your yes, three phrases act on it as its buttons do:

| Say | Does |
| --- | --- |
| "accept the adventure" | The same as **Accept**. |
| "change the adventure" | Opens the voice entry for what should change, as **Change something** does. |
| "reject the adventure" | Removes the draft, as **Decline** does. |

They act on the draft whose page is open on the Stories tab. With no draft page open and exactly one draft,
they act on that one. With two or more drafts and none open, d47 says there is more than one draft and to open the one
you mean, and changes nothing. With no draft the phrases are not heard. The page stays in view and nothing reads the
draft aloud. The model is refused all three.

#### `change_adventure`

Open the entry for a remark that changes the Commander's draft adventure. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `accept_adventure`

Accept the Commander's draft adventure, as the Accept button does. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `reject_adventure`

Reject the Commander's draft adventure and remove it, as the Decline button does. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Messages

Every objective said is also kept as a written message, from whoever said it, so a line said during a fight
is not lost. Open **Messages** on the Stories tab: newest first, unread in bold, and the button carries the
unread count. Opening a message marks it read. A message that was spoken has a **Play** button that
plays the clip it was spoken in; see [Spoken messages keep their clip](speech.md#kept-clips).

The messages live in `data/messages.json`. The file holds the most recent 200; past that the oldest read
message goes first.

Abandoning a story, or switching to another, removes the messages it posted: its objectives, nudges, clues,
beacon scan and ending. A finished story keeps them. The messages of an adventure you wrote yourself stay
when you abandon it.

### A nudge when a story stalls

When the next objective has waited through three play sessions and seven days since the last objective or nudge,
the Narrator's next narration leans toward it. It may hint at where the objective waits; it never quotes the
objective's line. It is posted to Messages from the narrator, and each adventure is nudged at most once each
time D47 runs.

### Stock stories

Open **Stories** on the Stories tab for the stock stories, each written to run for the length on its
card, a chapter at a time. Until stories are added to the catalog the page says "No stories yet." The
list shows each story's title, its length, the level of Commander it was written for, a blurb saying why you might pick it, and the
Guardian core the story is written for. The level is `new` (no engineering done yet), `midrange` (some
ship engineering done) or `endgame` (most ship and on-foot engineers unlocked, and at least one ship, suit
and weapon fully engineered). It is a guide to choosing, not a limit: every story can be picked at any
level, D47 does not work out your level, and where the story's backstory disagrees with your real ships,
credits or ranks, the chapter writer goes by what is true now. A story's page adds the length
beside the level's guideline, the tone, the story in your words and why it sends you to a Guardian beacon.

Each story also has a hidden layer: its secret, a beat sheet of Save the Cat beats, a clue for each of its
length's clue days, a line for each finale chapter, one to four ways it can end, and up to four
speakers who exist only in the story. Each of those speakers has a local voice, Kokoro or Chatterbox,
so a story never needs a paid key or sends its lines off your PC to be spoken. Every clue and finale
line names who says it: one of those speakers, the ship's AI or the narrator. D47 keeps that layer
sealed, never shows it, and sends it to the language model so the chapters can hint at it.

A speaker may be written in two versions: `forMan`, the version a Commander who is a man meets, and
`forWoman`, the version a Commander who is a woman meets. Each version has its own name, voice and
picture. The story's card never names that speaker and calls them by role ("the engineer"); the hidden
layer names them as `{name:<cast-id>}`, and D47 puts in the version's name before any line is shown,
spoken or sent to the chapter writer.

For such a story, the story's page asks **Your Commander is**: **A man** or **A woman**, above **Pick**.
**Pick** and **Switch** stay disabled until you choose, and the page says why. The choice is kept in
`settings.json` as `commanderGender` and is asked only by a story that needs it. The same control is on
the page of your running story; changing it there takes effect from the story's next line.

**Pick** makes the card's words your Backstory and has the ship's AI write chapter one, which then
begins. Act one ends when you scan the Guardian beacon nearest to you with the ship's data-link
scanner; arriving in the beacon's system is not enough. Before each act-one chapter D47 checks whether
the ship you are in can reach that beacon: it can when you own a fleet carrier, or when the beacon is at
most 20 jumps away at the ship's maximum jump range and a fuel scoop is fitted. When it can, the
chapter ends at the beacon. When it cannot, the chapter has no beacon objective, keeps to a session's
flying, and works toward a ship that can make the trip; the chapter writer is told the beacon, its
distance, and whether the jump range, the missing fuel scoop or both stand in the way. The chapter's last line is said before
the core wakes, in the voice that was aboard. Each chapter is an adventure on this tab. When one finishes, the next is written from it
and begins. **Switch** abandons the running story and picks another.

A story of 3 days, 1 week or 2 weeks has no trip to a beacon: it begins after you have scanned one. At
**Pick**, the scan is narrated in the words the story was written with, posted to Messages from its
speaker and said aloud. Then the cores wake and the story's core comes aboard, as for a real scan, and
chapter one begins in act one with no beacon objective. The story's days count from the pick. A story of 1
month or longer keeps the real scan.
**Abandon** ends the story. Neither changes your Guardian cores. Abandoning a chapter on its own page
pauses the story, and **Resume** on the Stories page begins that chapter again.

Each story is written for one Guardian core, shown on the list and on the story's page as "Core". When
you scan the story's first Guardian beacon, or at the pick of a story shorter than a month, that core comes aboard in place of the core you had, says
the waking line, and Messages says it came aboard and that you can choose another core in Settings. A
core you choose afterwards stays; the story does not bring its own back. Pausing, abandoning or finishing
the story leaves the core aboard as it is.

One story runs at a time, kept in `data/story.json`. A story needs a language model and galaxy search,
the same as asking for an adventure.

Stories need Elite Dangerous: Odyssey, because most chapters use it: on-foot missions, settlements,
exobiology and suits. D47 reads the `Odyssey` flag on each `LoadGame`. When the last one says
`"Odyssey":false`, the Stories page says why stories are off, **Pick** and **Switch** are disabled, a
running story writes no chapter and gives no clue, and the time does not count toward a clue's day. The
next `LoadGame` with Odyssey brings the story back where it was, and a story you switched off stays off.
Before D47 has seen a `LoadGame`, stories run.

A story chapter's page never shows its premise, turn or ending, because the chapter was written
from the hidden layer.

### Download stock stories {#download-stock-stories}

Stock stories are files on the `stories-1` release of the repository. The first time the Stories page
opens in a session, D47 fetches `index.json` into `data\stories`, shows the stories already on disk at
once and redraws when the new list lands. Opening a story's page fetches its hidden file and every
cast picture it names when the hidden file is not on disk, and the page redraws with its **Cast**
section when they land. **Pick** on a story whose hidden file is still not on disk fetches it, reads
**Downloading** while it does, and starts the story when every file is in place. On a failure the page says "The story could not be downloaded. Check your
connection and pick it again." and nothing starts. At startup, a story that is running or paused and
has no hidden file on disk is fetched, and one that has fetches any cast picture the release has and
`data\stories` does not.

**Download stock stories**, on by default, is in the Adventures settings. Off, nothing is fetched and
the Stories page lists only stories already on disk.

### Story ratings {#story-ratings}

D47 can show every stock story's average rating, from 1 to 5 stars, and send your own. Ratings live on
a Worker at `https://d47-ratings.dseelinger.workers.dev`, not on GitHub. The first time the Stories page
opens in a session, D47 fetches every story's average and vote count. A story's stars are the average
rounded to the nearest half, so 3.74 is 3.5 and 3.75 is 4. The Stories page can keep only stories of a
least star rating, 4, 3, 2 or 1 stars and up, and can sort by highest rated: more stars first, then more
votes, then catalogue order. A story with no votes fails every star filter and sorts last.

Only a story you have picked can be rated, in any state. Your vote is kept on the story in
`data\story.json`, with a flag while it, or taking it back, has not reached the Worker. A vote carries
the story, your stars and a random number made on this PC the first time you rate, one per Commander and
kept in `data\story.json` with that Commander's stories. It is not your donation identifier, and your
Frontier ID is never sent. Reinstalling without `data\` makes a new number, so one person can then vote
twice. No voice command or model tool rates a story.

Each card on the Stories page shows its stars and vote count, or five empty stars and "(No ratings yet)". The filter bar's
**Rating** and **Sort** choices set the least star rating and the order, and **Clear filters** resets
them. A story's page shows its average under the title. For a story you have picked it also shows
**Your rating**: click a star to rate, click your current rating again to take it back, or use Left,
Right and Delete once the stars have focus. A vote that could not be sent says so, and is sent again
the next time the Stories page opens. When the averages cannot be fetched, every story shows as unrated,
with no error, and they are asked for again the next time the page opens.

**Story ratings**, on by default, is in the Adventures settings. Off, nothing is fetched or sent and the
Stories page shows no ratings.

### Cast pictures

A speaker may have a picture, downloaded with the story as `<story-id>.<cast-id>.jpg` into
`data\stories`; a speaker in two versions has one for each, ending `.for-man.jpg` and `.for-woman.jpg`. A
speaker marked `primary` is a recurring character named on the story's card, and always has one. A line
that speaker says is posted to Messages with the picture, which the message page shows above the text, at
most 240 px wide. A line from the ship's AI or the narrator has none.

Under the picture, **Change picture** opens a file picker for a PNG, JPEG, BMP or WebP file under 10 MB.
D47 reads it, scales it to at most 1024 px on the longer side and keeps it as
`data\pictures\<story-id>.<cast-id>.png` (with the version suffix for a speaker in two versions). A file
that cannot be read as a picture is refused with the reason, and nothing is written. From then on every
message with that picture, earlier ones included, shows your file. **Use the default** deletes it, and the
story's own picture shows again. Your pictures stay on this PC; nothing is sent anywhere, and the model
has no tool for them.

```csharp
public string? For(StorySpeakerShown? speaker) =>
    speaker is { } shown && IsName(shown.Picture) && (shown.Primary || Find(shown.Picture) is not null)
        ? shown.Picture
        : null;
```

### Who speaks a story line {#story-speakers}

Every line of a stock story names its speaker: each clue and finale line, the narrated beacon scan, and in
each chapter the opening and every objective's line. The chapter writer is told the speakers, with each cast
member's name and who they are, and returns a speaker for every line it writes. A chapter that gives a
line to anyone else is sent back once with the reason, and refused if it comes back the same way.

- **`ship`** is the core aboard, in the ship's voice as you set it under **Its voice**. While the core
  aboard is stock COVAS, a `ship` line is spoken in the narrator voice and posted from the Narrator, and
  the chapter writer is told so, so it writes those lines as narration. With a Guardian core or a core you
  wrote aboard, that core speaks them. This is decided when the line is spoken.
- **`narrator`** is the narrator voice set under **Narrator Voice**.
- **A cast member** speaks in the provider and voice the story pinned to it, Kokoro or Chatterbox, whatever
  **Where each voice comes from** says, unless you chose another for it (see [Cast voices](#cast-voices)). A speaker in two versions speaks in your version's voice, and
  changing **Your Commander is** takes effect from the next line. The line goes through that speaker's
  own sound, a comms link at the story's strength and any Guardian effects the story lists, and nothing
  else: no radio for its role and none of the **Guardian Voice Effects** you set for the ship. The model
  writes a cast member's clue as that member, from their name and who they are.

Each line is posted to Messages from its speaker's name, with the speaker's picture and the clip it was
spoken in. When a chapter's objective belongs to the narrator or a cast member, that speaker says the objective's line
as written and the ship then says where you go next. A line may open with a sound such as
`[static crackle]`: a voice that performs sounds performs it, and every other voice has it taken out.

```csharp
public static StoryLineVoice Of(
    string? speaker, StorySecret secret, string? gender, Persona.Persona? core, IReadOnlyDictionary<string, StoryVoiceChoice>? choices = null)
```

#### A story needs its voices first

A story cannot be picked until every voice its cast speaks in is ready, the voices you chose included.
**Pick** and **Switch** are disabled, and the story's page lists what is missing:

- the Chatterbox download and its size, when a member speaks through Chatterbox and it is not downloaded;
- the Kokoro download and its size, when a member speaks through Kokoro and it is not downloaded;
- a recording under **Settings**, **Your voice**, when a member speaks in your own voice;
- the provider's API key under **Settings**, **Its voice**, when you chose a provider that needs a key and
  none is stored.

The **Local voice** and **Chatterbox voice** download rows under **Its voice** appear while a downloaded
story's cast needs them, even when no slot uses that provider. A pick tried anyway is refused, and the ship
posts one message to Messages listing the same things.

If a voice a running story needs stops being ready, for example because the recording was deleted, the
story is paused within a few seconds, its chapter stops, and the message is posted again. **Resume** is
refused, with the same list, until the voice is back.

### Cast voices {#cast-voices}

A story's card page has a **Cast** section listing its primary characters, and no one else, before you
pick the story and while it runs. A character in two versions is listed once **Your Commander is** is set,
as your version. Each row shows the character's picture, name, and the provider and voice it speaks in,
with "(the story's voice)" while it is the one the story pinned. **Play sample** speaks the character's name
and one fixed sentence in that voice. **Change picture** and **Use the default** work as they do on a
message.

**Change voice**, on the row and on every message from a story character, primary or not, opens a picker
of every provider that speaks, then of every voice that provider lists, of either gender. The story's own
voice is marked in the list. The story's text is not rewritten for the voice you choose. Each voice in the
list can be played first; on a paid provider each sample is billed like any line. **Use the default**
returns the character to the story's voice. A character that is not primary is listed nowhere until its
first message arrives.

The choice is kept in `settings.json` under `storyVoices`, keyed `<story-id>.<cast-id>`, with
`.for-man` or `.for-woman` for a character in two versions. Lines spoken through a paid provider count in
the speech spend with every other line. If the chosen provider fails a sentence, that sentence is spoken
in the story's own voice, and the character's row on the card page says why.

A character on a hosted provider sends its lines, which are the story's sealed text, to that provider.
**Privacy and egress**, under text to speech, names each such character, its story and its provider, with
that provider's own disclosure, and `get_data_egress` answers from the same entry. A character on Kokoro or
Chatterbox adds nothing. Only the panel changes these voices; the model has no tool for them.

```csharp
public static PinnedVoice Voice(StorySpeaker member, StorySpeakerShown shown, IReadOnlyDictionary<string, StoryVoiceChoice>? choices)
```

### How a chapter is fitted to you

**Genre.** Each card has a Save the Cat genre, and the chapter writer is given that genre's three
elements, from Blake Snyder: a Buddy Love story keeps an incomplete hero, a counterpart and a
complication in play. In a Buddy Love story the partner exists only in the fiction; a chapter may offer
hiring a crew member but never requires it.

**Your ship and credits.** The writer is told your position, your ships, the jump range of the one you
are in, your credits at the last load, your fleet carrier and your ranks, and sizes counts and
destinations to them. There is no fixed limit on ships: a chapter may have you save up for one, and a
later chapter have you buy and board it. A chapter is sized to finish in one to three play sessions, or
in one session in a story shorter than three months. A longer undertaking, such as engineering, saving
for a ship or a run of ranks, carries on across chapters.

**Activity, not only travel.** From chapter two on, no more than two of a chapter's objectives may be arrive,
dock, land or scan. A chapter with more is refused.

**The long haul.** A chapter keeps to a session's flying unless you had 250,000,000 credits or more at
the last load, or own a Caspian Explorer, and the story is three months long or more. Then the chapter
may go anywhere, as far as Colonia or Sagittarius A*. Below that, the writer is not told it exists.

**A credit reserve.** A chapter never spends the Commander to nothing. A story objective that buys something,
which is a `board` into a hull they do not own, `carrierbuy` or `squadronfound`, is allowed only when the
credits at the last load cover the price plus a reserve: the price again or 500,000,000, whichever is less.
Where a fixed threshold above is higher, it applies. A hull already owned is allowed at any balance, and a
hull with no price in d47's table is refused unless owned. The writer is told the most the chapter may ask
the Commander to spend, and a chapter that asks for more is refused and written again. Saving up and buying therefore fall in separate
chapters.

**The reach limits one hop.** A chapter's reach is the longest one hop may be, not how far the chapter
goes: a farther place is reached over several hops.

**The finale's destination.** Finale chapter 1 names where the story ends: a landable body in a real
system, resolved as a land objective's place is, with no permit needed. It may be at most five hops at the
chapter's reach from you for each finale chapter after the first, and at least five: with a reach of 360
light years, 1,800 light years in a story with two finale chapters and 5,400 in one with four. A finale
chapter 1 with no destination, or one that does not stand, is refused. The destination is kept on the
story. Each later finale chapter's writer is told the destination, how far it is from you and how many
finale chapters are left. A finale chapter between the first and the last that starts more than one
reach from the destination must end closer to it than it started. The last finale chapter's last objective
is a land objective on the destination, and that one objective may be farther than the reach. A 3-day story's one
finale chapter both names the destination and lands on it.

**The comfort zone.** Every third chapter after the beacon scan, d47 picks the activity your
`Statistics` show you have done least, and the chapter must contain one objective of it:

| Activity | Figure |
| --- | --- |
| bounty | `Combat.Bounties_Claimed` |
| bond | `Combat.Combat_Bonds` |
| mine | `Mining.Quantity_Mined` |
| organic | `Exobiology.Organic_Data` |
| rescue | `Search_And_Rescue.SearchRescue_Count` |
| passenger mission (a `Mission_Passenger` mission) | `Passengers.Passengers_Missions_Delivered` |

A tie goes to the earlier row. Before d47 has seen a `Statistics` event, no activity is picked. An
activity you have refused in the story is never picked.

### Refusing an objective

A story chapter's current objective has a **Not for me** button, on the chapter's page and on the Stories
page, and a voice command: "this objective is not for me", "not for me" or "give me a different objective". The
button asks "Write a different objective?" and, for an objective that asks for an activity, "This story won't ask
you to collect bounties again." (or whichever activity it is). On yes the ship's AI writes new objectives from
the current one to the end of the chapter. Objectives already done stay as they were, the chapter count and the
clues do not change, and the new objectives count only what happens after you refused. A replacement arrive
objective for a system you had already visited waits for your next arrival there. If the write fails, the objective
stays as it was and the Stories page says why.

The activity is remembered for the rest of the story. A refusal is kept under the objective's kind, and for a
mission under the kind and the mission family, so refusing a `Mission_Massacre` objective still allows a
`Mission_Courier` one. Every later chapter's writer is told which activities are refused, and a chapter
with an objective for one is refused and rewritten. The Stories page lists them. An objective that names a place
(arrive, dock, land, scan, board, rank) is replaced and not remembered. The Guardian beacon scan that
ends act one cannot be refused. A refusal cannot be taken back.

The model is refused this tool.

#### `refuse_story_beat`

Refuse the objective the Commander's story chapter is waiting on and write a different one in its place. The story remembers the activity and does not ask for it again. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Pausing the story

Clear **Story on**, on the Stories page or on the mini panel, or say "pause the story", and the
running stock story goes quiet until you switch it on again. While it is off no objective is said, no
nudge or clue is owed, nothing from the story is posted to Messages, missions get no story aside, and the
hidden layer is left out of every prompt, so the Narrator, the core aboard and chatter do not hint at it. The clue clock stops.
Narration and chatter work as they always do, and your Backstory, cores and persona are unchanged.

A place you visit while the story is off does not count. Its objective waits for the next visit. The
switch is kept in `data/story.json`, so it lasts across restarts. Say "resume the story" or tick
**Story on** to bring it back.

Both phrases are the model-free router's, and the model is refused them.

#### `pause_story`

Pause the Commander's running story: no objectives, nudges, clues or story chatter until it is resumed.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `resume_story`

Resume the Commander's running story after a pause.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Ending a story

When a stock story's last finale chapter is done, the story is finished and d47 posts its ending to
Messages, said by the Narrator while stock COVAS is aboard or narration is on and otherwise by the core
aboard, with the story's options listed under it. Open the
message and press an option, or say "choose ending two". A story with one option takes "accept the ending".
The answer is kept on the story, and the option's closing line is said and posted. An option can also bring
cores aboard: each one says its waking line once. Nothing is taken away by any option, and the ending stays
waiting across restarts until you answer it.

The model is refused this tool.

#### `answer_story_ending`

Answer the ending of a finished story with one of its options, numbered from one. The Commander's choice alone.

```json
{"type":"object","properties":{"option":{"type":"integer","description":"The option\u0027s position in the ending message, from one. Leave out when the ending has one option."}},"required":[],"additionalProperties":false}
```

### Clues

Each story card has one of seven lengths, and the story is paced to it. Every story in the catalog is a
year long. The days count from the beacon scan, or from the pick where the scan is narrated; days while the story is paused, switched off or without
Odyssey do not count.

| Length | Clues come due on day | Finale from day | Finale chapters | Clues in all | Objective sheet |
| --- | --- | --- | --- | --- | --- |
| 3 days | 1 | 2 | 1 | 2 | short |
| 1 week | 1, 3 | 5 | 2 | 4 | short |
| 2 weeks | 2, 4, 7, 9 | 11 | 2 | 6 | short |
| 1 month | 3, 7, 11, 15, 19 | 23 | 3 | 8 | short |
| 3 months | 7, 14, 21, 28, 42, 56, 70 | 84 | 3 | 10 | full |
| 6 months | 7, 14, 21, 28, 60, 90, 120, 150 | 180 | 4 | 12 | full |
| 1 year | 7, 14, 21, 28, then every 30 days to 330 | 360 | 4 | 18 | full |

Each clue before the finale also waits for a play session since the last clue and for a chapter finished
since the last clue, so no two come in one session, and a Commander back after months away gets the next
clue in order and no more.

The story's stage follows the clues you have had, not the calendar. It is act one before the beacon scan,
and for chapter one after a narrated scan, and Break into Two with no clue yet; after that, by the number of clues you have had:

| Length | Fun and Games | Midpoint | Bad Guys Close In | All Is Lost | Dark Night of the Soul |
| --- | --- | --- | --- | --- | --- |
| 3 days | – | 1 | – | – | – |
| 1 week | – | 1 | – | 2 | – |
| 2 weeks | 1 | 2 | – | 3–4 | – |
| 1 month | 1–2 | 3 | – | 4–5 | – |
| 3 months | 1–3 | 4 | 5 | 6 | 7 |
| 6 months | 1–4 | 5 | 6 | 7 | 8 |
| 1 year | 1–8 | 9 | 10–12 | 13 | 14 |

Each chapter's writer is told the stage and given that stage's lines from the objective sheet, and nothing from
later stages. The full sheet has all fifteen objectives. The short sheet has only the objectives of the stages its
length reaches: Opening Image and Catalyst in act one, Break into Two once the beacon is in reach, then
Fun and Games, Midpoint and All Is Lost where the length has them, the Finale, and the Final Image in the
last finale chapter. A 3-day story's objectives are Opening Image, Catalyst, Break into Two, Midpoint, Finale
and Final Image.

The finale begins with the first chapter written once every clue before it is given and the finale's day
has passed. Each finale chapter's clue comes due as the chapter begins, with no wait for a day or a
session. When the last finale chapter is done and its clue given, no further chapter is written: the
story is finished, the Stories page shows it as **Finished**, and it no longer holds back any Guardian
core. A story keeps the length it was picked with.

A clue is spoken by the speaker the story names for it, as [Who speaks a story line](#story-speakers)
describes: a `ship` clue by the core aboard in its own words, or by the Narrator while stock COVAS is
aboard; a `narrator` clue by the Narrator; a cast member's clue by that member. The narration setting
does not change who speaks a clue. A clue is also posted to Messages, from whoever spoke it. A clue needs
a language model and personality; without them it waits.

The Narrator, invented chatter, scene chatter and the chapter writer all read the same hidden layer:
the story's secret, its end, and the clues you have had so far. A Guardian core or a core you wrote
reads it too, in conversation and in its own lines. Stock COVAS never reads it: it is standard
equipment with no history, so it has nothing to hint at. They are told to
hint at it and never state it, and to mislead you only about the story, never about fuel, cargo,
credits, routes, rank or danger. **Privacy and egress** says a hidden story is sent to the language
model, without quoting it.

### Missions in the story

While a stock story is running and switched on, speech about a mission you take carries a short excerpt
of the story's public layer: its title and tone, and the start of the backstory in your words. Each
mission gets this once, from the first of three places to speak about it:

- **The accept line.** Taking a mission gets a line from the core aboard that ties it to the story. Where
  d47 already had something to say, such as a cargo that needs several trips, that line keeps every fact
  and adds the aside. Without a language model and personality, a mission with nothing else to say gets
  no line.
- **Mission scene chatter.** With a scenario set, the people of the faction that gave the mission are
  told the excerpt too, and one line may touch on it.
- **Narration.** A narration during a lull ties in the newest mission on your board that has not had its
  aside.

The aside never changes a mission: its name, giver, destination, cargo, reward and deadline are said as
the game gives them. The chapter writer is told the missions you hold, and may make one's destination
system an arrive objective or its station a dock objective, so the mission's trip is also the story's. No objective asks
you to complete, fail or abandon a mission. While the story is paused or switched off, missions get no
aside, and a mission taken meanwhile gets its aside once the story is back.

```csharp
public MissionAside? Take(IReadOnlyList<Mission> missions)
```

### Where it lives

`data/adventures.json`, beside the executable, per Commander, and hand-editable like everything
else D47 writes. Only two things are stored — the definition, and the moment you began. Everything
else is worked out from your journal each time, which is what lets a story you flew with D47 closed
be up to date the moment you open it.

A stock story keeps only its current chapter and the one before it in `data/adventures.json`. When a
chapter begins, every earlier chapter of the story moves to `data/story-chapters.jsonl`, one chapter
per line, with when each of its objectives fired; when a story is abandoned, switched or finished, all of
its chapters move there. Archived chapters leave the Adventures page, the chapter writer still reads
the last ten of them by name and premise, and your log still lists their objectives. Story chapters do not
count toward the 40 adventures of your own that the file holds.

### What it does not do yet

- **Branching.** One current objective, and only it can match; an objective that would match out of order is
  ignored rather than banked.
- **Importing** somebody else's adventure. The store file is already the format, so this is a copy
  and a validate when it comes.
