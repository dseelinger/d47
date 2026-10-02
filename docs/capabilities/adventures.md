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
<h2><span class="num">1</span> Ask for one, in the Adventures tab or out loud.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a request for an adventure typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">tell me a story about this system</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Or press Adventures in the tab strip and pick one there.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Either way you get a first beat, and the story waits for you.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Fly. The next beat arrives when your journal earns it.</h2>
<svg viewBox="0 0 880 168" role="img" aria-label="A jump or a docking in the journal moves the story to its next beat">
 <rect x="20" y="24" width="250" height="72" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOU JUMP</text>
 <text x="145" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">or dock, or scan</text>
 <line x1="282" y1="60" x2="306" y2="60" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="320,60 304,52 304,68" fill="var(--accent-muted)"/>
 <rect x="334" y="24" width="526" height="72" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="597" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE NEXT BEAT IS SPOKEN</text>
 <text x="597" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">in your ship AI's own voice</text>
 <text x="20" y="146" font-size="16" fill="var(--text-muted)">Nothing is on a timer. Say "where am I up to" to hear the story so far.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
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
<svg viewBox="0 0 880 300" role="img" aria-label="A spine of premise, want, stake, turn and ending, with beats hung on real places">
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
 <text x="484" y="56" font-size="16" fill="var(--text)">a beat, standing on a real place</text>
 <rect x="458" y="92" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="128" font-size="16" fill="var(--text)">a beat, standing on a real place</text>
 <rect x="458" y="164" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="200" font-size="16" fill="var(--text)">a beat, standing on a real place</text>
 <text x="440" y="268" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">The shape is written first. The places are where that shape can stand.</text>
 <text x="440" y="294" text-anchor="middle" font-size="16" fill="var(--text-muted)">Which is why D47 is never asked for five stops — it is asked for a story.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Your journal moves it. There is nothing to tick.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="A beat fires when you reach its place, and nothing before you began counts">
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
 <text x="440" y="185" text-anchor="middle" font-size="16" fill="var(--text)">Nothing you did before you began counts, and only the current beat can fire.</text>
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
 <text x="824" y="210" text-anchor="end" font-size="16" fill="var(--text)">the beats ahead of you</text>
 <text x="440" y="268" text-anchor="middle" font-size="16" fill="var(--text-muted)">A storyteller who knows the ending leaks it. So the AI is simply never told what is coming.</text>
</svg>
<p class="body">Foreshadowing still happens — it is written <em>into</em> the earlier beats, by the turn that did know the ending. Between beats the ship's AI wonders aloud in character and never states a new fact about the story, so nothing it says on a quiet stretch can contradict a beat you have not reached.</p>
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
the ship's AI, anchored to the galaxy by beats, accepted by you, and advanced by your own journal.

The drive behind it is to add story to a sandbox, which sandboxes deeply lack. It is deliberately
**not** a checklist of things to complete.

### Two ways to have one

**Write an adventure** — the editor is a level of the Adventures tab. A name, an opening, then the
five spine questions in order, each skippable. Then the beats: what happens, where, and the line.
Every field is a chooser except the prose, so the form cannot compose something the file would
refuse.

**Ask for one** — a short form and D47 writes it, in the voice of whichever core is aboard. Three
choosers, each with a default, so pressing *Go* on an untouched form is a complete ask:

- **Reach** — how far the story may go: *near here*, *a session's flying*, *anywhere*. Turned into
  light years by what you can actually move — your ship's jump range, or your carrier's if you have
  one.
- **Length** — which structure. *Short* is three beats, setup and turn and resolution. *An evening*
  is five. *Long* is eight or more. The count follows from the structure rather than the other way
  round.
- **Using** — *this ship only*, or *anything I own*. Shown only when you have a choice.

And one optional thing said: a brief — a theme, a mood, a place it must include. Empty is fine.

**Things it reads rather than asks**, because asking would be asking you to describe your own
ships: your fleet and what each hull can do, whether you have a carrier and where it is, where you
are, who is aboard, and your ranks.

**Write the next chapter** — on a finished adventure's page, the same form for the story that
follows it. The AI reads the finished one in full — its spine, its beats and what was said as you
flew it — and each chapter before that by name and premise only. The new story's want follows from
how the last one turned and ended, and its stake is the belief the last one left open. The draft
card says which adventure it follows, and it is accepted, flown and checked like any other.

### What a beat can be

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

Only a stock story's chapter one uses the beacon trigger, as its last beat, and you cannot add one on the
form.

Every trigger after the seventh is counted except the engineer stage, which fires once. A counted beat fires when its total reaches its count, and only what happens
after the beat before it has fired counts. It has no place of its own; a chapter that needs it done
somewhere puts an arrive or dock beat there first. The card shows the running total, such as "Kill bonds
for LTT 7786 Labour: 3 of 8", and a catch-up after d47 was closed rebuilds it from the journal. These
mission families are set aside, and no beat uses or counts them: `Mission_Massacre_Skimmer`,
`Mission_Disable`, `Mission_Hack`, `Mission_OnFoot_Hack`, `Mission_Scan`, `Mission_RS_` and `Mission_DS_`.
A story steers illegal missions to Anarchy space but does not enforce it. Illegal families are any whose name contains `Illegal`, `Mission_OnFoot_Heist` and `Mission_OnFoot_Sabotage`. The chapter writer is given up to five Anarchy systems within reach and must put an illegal mission beat directly after an arrive or dock beat in one of them, or use no illegal family when none is in reach. Any completion of the family still counts, whatever its target. While an illegal mission beat is current, approaching the target settlement of a live mission of that family, when a faction other than Anarchy runs it, makes d47 say once per mission that the job is a crime there.
A suit mod or livery change is seen when the next `SuitLoadout` or `Loadout` is written, so the beat may fire some minutes after the change. A suit, weapon or ship with no earlier loadout is only remembered: its first loadout counts nothing. A livery beat names no paint job, kit or decal. The first `CarrierLocation` seen for a carrier sets the baseline and counts nothing; a squadron carrier's is ignored. No beat may need an ARX purchase, because the journal cannot show one; the chapter writer is told so.

**Conflict and faction beats.** A conflict beat counts taking part, not winning: a conflict lasts days and other players move it. The side is the faction the first counted contribution went to, and contributions to the other side do not count. A bond in a system with no active or pending conflict does not count. d47 reads the conflicts from the `Conflicts` of `FSDJump`, `Location` and `CarrierJump`, as they stood when the Commander last arrived, so a war that began after that arrival is not seen until the next one. When a later arrival shows the conflict ended (an empty `Status`), the standing records whether the side with more `WonDays` was the Commander's, and the next chapter's brief says so. For a faction beat, the next chapter's brief says whether the faction's influence rose or fell between two visits a day apart. The writer is told both kinds and up to eight active or pending conflicts in systems the Commander has visited; a conflict beat may name no faction and leave the Commander to find one. A faction beat counts mission marks only; trade, data sales and bounties do not count as faction work.

**Carrier, squadron and team beats.** The chapter writer is told about a kind only where the Commander can do it, and a chapter that contains one it was not told about is refused:

| Kind | Allowed when |
|---|---|
| `carrierbuy` (once) | 7,000,000,000 credits at the last load (the carrier costs 5,000,000,000, so its reserve rule needs only 5,500,000,000), and no fleet carrier owned |
| `carrierjump` (counted) | a fleet carrier owned |
| `wing` and `multicrew` (counted) | always |
| `squadron` (once) | not in a squadron |
| `squadronfound` (once) | 20,000,000 credits at the last load: the 10,000,000 it costs and a reserve of the same; and not in a squadron |

A Commander is in a squadron from a `SquadronStartup`, `JoinedSquadron` or `SquadronCreated` until a `LeftSquadron`, `KickedFromSquadron` or `DisbandedSquadron`, or until the next `LoadGame` without a `SquadronStartup`. A wing or multicrew beat needs another player, so the writer is told the Commander can refuse it, and neither is ever the comfort-zone activity.
The form offers the first six kinds and shows any other beat without changing it. A text filter ignores case and any `$…;` wrapping, so `Tritium` and `tritium` are one type, and `Thargoid` matches `$SAA_SignalType_Thargoid;`. An engineer beat at `Invited` is also met by `Unlocked`, and by the startup list that names every engineer. An on-foot engineer is written only in that startup list, so an engineer beat naming one fires at the first login after the stage is reached, not at the moment.

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
thing you can actually do in a story is fly to the next beat. Ask the core whether one of them was
real and it says they are someone in the story.

### Where you are in it, and what it says

A beat speaks when it fires, and hands over to the next one in the same breath:

```text
The log's last entry is dated the day the beacon went quiet.
Next: dock at Maren Anchorage in Dyson's Hollow.
```

The hand-off names the place and the act and nothing else — never the next beat's title or its
line, which is the spoiler rule holding. A scan's hand-off says *how*, because "scan X" sent a
Commander looking for a detailed surface scanner when the ship's own scanner was what the story
meant. It also says that going there counts: a body already scanned writes no second `Scan`, so a
scan beat is satisfied by the approach as well, and a story cannot strand a Commander on somewhere
they had already been.

**No counts where you read.** The card says the story's name and where it is — *not yet begun*, the
current beat's title, or *finished*. *Beat 3 of 7* is checklist language and stays off the card.

### Stopping, and starting again

**Abandon** a begun adventure and it stops telling you: no beat fires, the AI drops it from what it
knows, and a beat waiting out its settle window is discarded. The record stays, folded away at the
foot of the list with what it reached. **Begin again** on an abandoned one starts from the opening
with a fresh stamp — nothing that happened in the gap counts, because a start is a start.

Abandoning is also how a begun adventure gets edited: abandon it, change it, begin again.

**Remove** deletes the record. For a begun one it asks first, because an adventure three beats in
is work you did.

Both are yours alone, reachable from the panel and nowhere else.

### Nothing here is callable by the model

Generation, beginning, abandoning and removing are all your acts, on the panel. Pausing and resuming
a stock story are yours too, by voice or by the **Story on** checkbox. The ship's AI can
*read* the story — that is what it is for, so it can play off it — and can change nothing about
it. A hostile message arriving in your comms panel cannot propose a story, end one, or delete one.

It also costs nothing: none of this is on the advertised tool surface.

### Messages

Every beat the ship's AI says is also kept as a written message, so a line said during a fight is not
lost. Open **Messages** on the Adventures tab: newest first, unread in bold, and the button carries the
unread count. Opening a message marks it read.

The messages live in `data/messages.json`. The file holds the most recent 200; past that the oldest read
message goes first.

### A nudge when a story stalls

When the next beat has waited through three play sessions and seven days since the last beat or nudge,
the Narrator's next narration leans toward it. It may hint at where the beat waits; it never quotes the
beat's line. It is posted to Messages from the narrator, and each adventure is nudged at most once each
time D47 runs.

### Stock stories

Open **Stories** on the Adventures tab for the stock stories, each written to run for the length on its
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
chapter ends at the beacon. When it cannot, the chapter has no beacon beat, keeps to a session's
flying, and works toward a ship that can make the trip; the chapter writer is told the beacon, its
distance, and whether the jump range, the missing fuel scoop or both stand in the way. The chapter's last line is said before
the core wakes, in the voice that was aboard. Each chapter is an adventure on this tab. When one finishes, the next is written from it
and begins. **Switch** abandons the running story, keeps its chapters on file, and picks another.

A story of 3 days, 1 week or 2 weeks has no trip to a beacon: it begins after you have scanned one. At
**Pick**, the scan is narrated in the words the story was written with, posted to Messages from its
speaker and said aloud. Then the cores wake and the story's core comes aboard, as for a real scan, and
chapter one begins in act one with no beacon beat. The story's days count from the pick. A story of 1
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

A story chapter's page never shows its premise, turn or ending, and has no **Edit** button, because
the chapter was written from the hidden layer.

### Download stock stories {#download-stock-stories}

Stock stories are files on the `stories-1` release of the repository. The first time the Stories page
opens in a session, D47 fetches `index.json` into `data\stories`, shows the stories already on disk at
once and redraws when the new list lands. **Pick** on a story whose hidden file is not on disk fetches
it and every cast picture it names, reads **Downloading** while it does, and starts the story when
every file is in place. On a failure the page says "The story could not be downloaded. Check your
connection and pick it again." and nothing starts. At startup, a story that is running or paused and
has no hidden file on disk is fetched.

**Download stock stories**, on by default, is in the Adventures settings. Off, nothing is fetched and
the Stories page lists only stories already on disk. The hidden layer is fetched only when you pick
the story, so a story's secret is not downloaded until you choose it.

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

**Activity, not only travel.** From chapter two on, no more than two of a chapter's beats may be arrive,
dock, land or scan. A chapter with more is refused.

**The long haul.** A chapter keeps to a session's flying unless you had 250,000,000 credits or more at
the last load, or own a Caspian Explorer, and the story is three months long or more. Then the chapter
may go anywhere, as far as Colonia or Sagittarius A*. Below that, the writer is not told it exists.

**A credit reserve.** A chapter never spends the Commander to nothing. A story beat that buys something,
which is a `board` into a hull they do not own, `carrierbuy` or `squadronfound`, is allowed only when the
credits at the last load cover the price plus a reserve: the price again or 500,000,000, whichever is less.
Where a fixed threshold above is higher, it applies. A hull already owned is allowed at any balance, and a
hull with no price in d47's table is refused unless owned. The writer is told the most the chapter may ask
the Commander to spend, and a chapter that asks for more is refused and written again. Saving up and buying therefore fall in separate
chapters.

**The reach limits one hop.** A chapter's reach is the longest one hop may be, not how far the chapter
goes: a farther place is reached over several hops.

**The finale's destination.** Finale chapter 1 names where the story ends: a landable body in a real
system, resolved as a land beat's place is, with no permit needed. It may be at most five hops at the
chapter's reach from you for each finale chapter after the first, and at least five: with a reach of 360
light years, 1,800 light years in a story with two finale chapters and 5,400 in one with four. A finale
chapter 1 with no destination, or one that does not stand, is refused. The destination is kept on the
story. Each later finale chapter's writer is told the destination, how far it is from you and how many
finale chapters are left. A finale chapter between the first and the last that starts more than one
reach from the destination must end closer to it than it started. The last finale chapter's last beat
is a land beat on the destination, and that one beat may be farther than the reach. A 3-day story's one
finale chapter both names the destination and lands on it.

**The comfort zone.** Every third chapter after the beacon scan, d47 picks the activity your
`Statistics` show you have done least, and the chapter must contain one beat of it:

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

### Refusing a beat

A story chapter's current beat has a **Not for me** button, on the chapter's page and on the Stories
page, and a voice command: "this beat is not for me", "not for me" or "give me a different beat". The
button asks "Write a different beat?" and, for a beat that asks for an activity, "This story won't ask
you to collect bounties again." (or whichever activity it is). On yes the ship's AI writes new beats from
the current one to the end of the chapter. Beats already done stay as they were, the chapter count and the
clues do not change, and the new beats count only what happens after you refused. A replacement arrive
beat for a system you had already visited waits for your next arrival there. If the write fails, the beat
stays as it was and the Stories page says why.

The activity is remembered for the rest of the story. A refusal is kept under the beat's kind, and for a
mission under the kind and the mission family, so refusing a `Mission_Massacre` beat still allows a
`Mission_Courier` one. Every later chapter's writer is told which activities are refused, and a chapter
with a beat for one is refused and rewritten. The Stories page lists them. A beat that names a place
(arrive, dock, land, scan, board, rank) is replaced and not remembered. The Guardian beacon scan that
ends act one cannot be refused. A refusal cannot be taken back.

The model is refused this tool.

#### `refuse_story_beat`

Refuse the beat the Commander's story chapter is waiting on and write a different one in its place. The story remembers the activity and does not ask for it again. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Pausing the story

Clear **Story on**, on the Stories page or on the mini panel, or say "pause the story", and the
running stock story goes quiet until you switch it on again. While it is off no beat is said, no
nudge or clue is owed, nothing from the story is posted to Messages, and the hidden layer is left out of
every prompt, so the Narrator, the core aboard and chatter do not hint at it. The clue clock stops.
Narration and chatter work as they always do, and your Backstory, cores and persona are unchanged.

A place you visit while the story is off does not count. Its beat waits for the next visit. The
switch is kept in `data/story.json`, so it lasts across restarts. Say "resume the story" or tick
**Story on** to bring it back.

Both phrases are the model-free router's, and the model is refused them.

#### `pause_story`

Pause the Commander's running story: no beats, nudges, clues or story chatter until it is resumed.

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
Messages, said in the voice that speaks the clues, with the story's options listed under it. Open the
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

| Length | Clues come due on day | Finale from day | Finale chapters | Clues in all | Beat sheet |
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

Each chapter's writer is told the stage and given that stage's lines from the beat sheet, and nothing from
later stages. The full sheet has all fifteen beats. The short sheet has only the beats of the stages its
length reaches: Opening Image and Catalyst in act one, Break into Two once the beacon is in reach, then
Fun and Games, Midpoint and All Is Lost where the length has them, the Finale, and the Final Image in the
last finale chapter. A 3-day story's beats are Opening Image, Catalyst, Break into Two, Midpoint, Finale
and Final Image.

The finale begins with the first chapter written once every clue before it is given and the finale's day
has passed. Each finale chapter's clue comes due as the chapter begins, with no wait for a day or a
session. When the last finale chapter is done and its clue given, no further chapter is written: the
story is finished, the Stories page shows it as **Finished**, and it no longer holds back any Guardian
core. A story keeps the length it was picked with.

While the core aboard is stock COVAS, every clue is spoken by the
Narrator, with narration on or off. With a Guardian core or a core you wrote aboard, a clue is spoken by
the Narrator when narration is on, and otherwise by the core, in its own words. A clue is also posted to
Messages, from whoever spoke it. A clue needs a language model
and personality; without them it waits.

The Narrator, invented chatter, scene chatter and the chapter writer all read the same hidden layer:
the story's secret, its end, and the clues you have had so far. A Guardian core or a core you wrote
reads it too, in conversation and in its own lines. Stock COVAS never reads it: it is standard
equipment with no history, so it has nothing to hint at. They are told to
hint at it and never state it, and to mislead you only about the story, never about fuel, cargo,
credits, routes, rank or danger. **Privacy and egress** says a hidden story is sent to the language
model, without quoting it.

### Where it lives

`data/adventures.json`, beside the executable, per Commander, and hand-editable like everything
else D47 writes. Only two things are stored — the definition, and the moment you began. Everything
else is worked out from your journal each time, which is what lets a story you flew with D47 closed
be up to date the moment you open it.

### What it does not do yet

- **Branching.** One current beat, and only it can match; a beat that would match out of order is
  ignored rather than banked.
- **Importing** somebody else's adventure. The store file is already the format, so this is a copy
  and a validate when it comes.
- ***Somewhere I've been*** in the editor — D47 keeps no visited-places list, so the two ways in
  are *Here*, which reads your live position, and typing a name.
