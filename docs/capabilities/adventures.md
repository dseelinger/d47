---
title: Stories
group: Knowledge
nav_order: 115
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app draws this band first and the How it works band after it.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a story that runs while you fly.</p>
<section>
<h2><span class="num">1</span> Ask for a short story, in the Stories tab or out loud.</h2>
<p class="say">Say "ask for a short story" and say what it should be about, or pick one in the Stories tab.</p>
<p class="expect">D47 writes a draft and waits for your yes. Accept it and you hear the first objective.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a request for a short story typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">ask for a short story</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Or press Stories in the tab strip and pick one there.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Either way D47 writes a draft and waits for your yes.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Fly. The next objective arrives when your journal earns it.</h2>
<p class="say">Fly as you normally would, and listen for the next objective.</p>
<p class="expect">When your journal shows a jump, dock or scan that earns it, your core speaks the next objective.</p>
<svg viewBox="0 0 880 168" role="img" aria-label="A jump or a docking in the journal moves the story to its next objective">
 <rect x="20" y="24" width="250" height="72" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOU JUMP</text>
 <text x="145" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">or dock, or scan</text>
 <line x1="282" y1="60" x2="306" y2="60" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="320,60 304,52 304,68" fill="var(--accent-muted)"/>
 <rect x="334" y="24" width="526" height="72" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="597" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE NEXT OBJECTIVE IS SPOKEN</text>
 <text x="597" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">in your core's own voice</text>
 <text x="20" y="146" font-size="16" fill="var(--text-muted)">Nothing is on a timer. Say "where am I up to" to hear the story so far.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Wait for the game to move. If you did the objective and the story has not moved, say "mark this objective done". On a stock story chapter you can also say "never ask this" to be given a different objective, or "change this objective" to be sent on a detour, one objective of your own, before the chapter goes on. Say "write this chapter again" to have the chapter written again from where you are.</p>
<p class="expect">After three play sessions and seven days at the same objective, your core nudges you toward it. The nudge does not apply to a short story.</p>
<p class="expect">A short story you have begun and not finished shows Change objective and Never ask this under NEXT on its page. They work as on a story chapter and change only that short story. Neither has a voice phrase for a short story.</p>
<p class="expect">Mark done, under NEXT on a story chapter's page or a short story's page, moves the story on as if the journal had shown the objective. Marking the Guardian beacon objective done records the scan and brings the story's Guardian cores aboard.</p>
<p class="expect">An objective to board a ship has a button beside Change objective and Never ask this that plans that ship, or shows it when you own it or have planned it. An objective to reach a stage with an engineer, sell data or apply suit mods has one that opens the engineer's page, the unsold data page or the suits page, when that page is on.</p>
<svg viewBox="0 0 880 192" role="img" aria-label="A story only moves when Elite writes something to the journal">
 <rect x="20" y="20" width="840" height="152" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">A story moves when the game does.</text>
 <text x="440" y="98" text-anchor="middle" font-size="16" fill="var(--text)">Sitting in the menu, nothing happens. After three sessions and seven days, the Narrator nudges.</text>
 <text x="440" y="134" text-anchor="middle" font-size="16" fill="var(--text)">On a stock story chapter, say "never ask this" or "change this objective".</text>
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
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A story you fly, told by the core, moved along by your own journal.</p>
<section>
<h2><span class="num">1</span> A story, not a list of stops.</h2>
<svg viewBox="0 0 880 300" role="img" aria-label="Objectives built by D47 on real places, with the spine and the lines written against them">
 <rect x="20" y="20" width="360" height="216" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="52" y="58" font-size="19" font-weight="800" fill="var(--accent)">THE OBJECTIVES</text>
 <text x="52" y="96" font-size="16" fill="var(--text)">built by D47</text>
 <text x="52" y="126" font-size="16" fill="var(--text)">what you do</text>
 <text x="52" y="156" font-size="16" fill="var(--text)">where you do it</text>
 <text x="52" y="186" font-size="16" fill="var(--text)">how many times</text>
 <text x="52" y="216" font-size="16" fill="var(--text)">in what order</text>
 <line x1="398" y1="128" x2="428" y2="128" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="442,128 426,120 426,136" fill="var(--accent-muted)"/>
 <rect x="458" y="20" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="56" font-size="16" fill="var(--text)">the spine, written against them</text>
 <rect x="458" y="92" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="128" font-size="16" fill="var(--text)">each objective's reason and lines</text>
 <rect x="458" y="164" width="402" height="60" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="200" font-size="16" fill="var(--text)">the opening, before the first</text>
 <text x="440" y="268" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">D47 builds the objectives on places that exist. The AI writes the story they tell.</text>
 <text x="440" y="294" text-anchor="middle" font-size="16" fill="var(--text-muted)">From chapter one on, the AI writes only the words, never a place or a count.</text>
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
<svg viewBox="0 0 880 274" role="img" aria-label="What the core is told, and when">
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
<p class="body">Foreshadowing is written into the earlier objectives by the turn that knew the ending. Between objectives the core wonders aloud in character and states no new fact. Say <code>where am I up to</code> to hear the story so far.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="persona.html"><span class="ct">Core →</span><span class="cd">Whose story it is — each core writes the one it cares about.</span></a>
<a class="card" href="galaxy.html"><span class="ct">Galaxy search →</span><span class="cd">The catalogue a generated story picks its places from.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">The other thing that remembers what you are in the middle of.</span></a>
</div>
</div>
</div></div>

## The details

A short story is a story: someone wants something, there is a belief the events exist to test, a
turn where it stops being what it looked like, and an ending that means something. It is told by
the core, anchored to the galaxy by objectives, accepted by you, and advanced by your own journal.

The drive behind it is to add story to a sandbox, which sandboxes deeply lack. It is deliberately
**not** a checklist of things to complete.

### How to have one

**Short Story** — a form and D47 writes it, in the voice of whichever core is aboard. Three
labelled rows, each with a default, so pressing *Go* on an untouched form is a complete ask:

- **Reach** — how far the story may go: *near here*, *a session's flying*, *anywhere*. Turned into
  light years by what you can actually move — your ship's jump range, or your carrier's if you have
  one.
- **Length** — how many objectives. *Short* is three or four: setup, turn and resolution, and a complication
  when there are four. *An evening* is four to six. *Long* is six to eight, and adds an opening first, a
  hesitation just before the turn and a closing last. The count within the range is drawn when the story is
  planned (see [A short story planned in code](#short-stories-planned)).
- **Ship** — *this ship only*, or *anything I own*. Shown only when you have a choice.

Each row has a grey hint under it that changes with the choice, such as how many light years a leg of the story may run. And one optional thing said: a brief — a theme, a mood, a place to end in, things to do, a ship to fly. Empty is fine. D47 builds the objectives from any place, activity or ship the brief names, and the AI writes the story's words in its theme and mood. The brief row is a tile showing the brief; press it to say or type one, and press *Clear* beside it to empty it.

Say "ask for a short story" and D47 opens this form and the entry for the brief. Say the brief, or
press **Done** with it empty, and it asks with the form as it stands, as *Go* does. When a language model or galaxy
search is missing, a warning at the top of the form says which, with *Open settings*, and *Go* is disabled.

While the story is written, a bar under *Go* shows what D47 is doing and about how long it usually takes. **Stop** ends
the write, and D47 says nothing. Leaving the page does not end it: the bar is there when you come back, on the desktop or in the
headset. When the story is finished D47 saves it and says so, whatever is showing, and opening Short Story afterwards
takes you to the draft.

**Things it reads rather than asks**, because asking would be asking you to describe your own
ships: your fleet and what each hull can do, whether you have a carrier and where it is, where you
are, who is aboard, and your ranks.

**Write the next chapter** — on a finished story's page, the same form for the story that
follows it. The AI reads what the finished one asked you to do, how it ended and what you asked during it,
and how many chapters came before. The new chapter follows from what you did and from the story, not from how the last
one was worded. The draft card says which story it follows, and it is accepted, flown and checked like any other.

Every chapter's wording is checked before it is shown, in a new draft and in a draft you ask to have changed. The title, the story's premise and ending, the objectives' titles, reasons and lines, a choice's options and endings, and the opening are read against the plain-language rule. A metaphor is rewritten in plain words where the check can do so, and the chapter is checked again. A feature the game does not have, or a rewrite that does not pass, is sent back to be written again. The check's own rewrite of a part is put in place once between the writer's rounds; a part it refuses again goes back to the writer, which is told every finding raised on that part. A chapter that still fails the check after its rounds is thrown away and written again. In a chapter built on drawn activities (see [A chapter planned in code](#drawn-chapters)) the check reads the title, spine, opening and lines together. A repair writes only the lines of the objectives it names, and a chapter written again has only its words written again, against the same objectives.

#### `ask_for_adventure`

Open the Short Story page and the entry for a brief; committing the brief asks for the short story, as Go does. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### What an objective can be

Every trigger is a comparison on a structured field rather than on a name:

| Trigger | Matched on | Never on |
|---|---|---|
| Arrive at a system | the system's id | its name |
| Dock at a station | the station's market id | its name — a carrier's name is player-chosen |
| Land on a body | system and body ids | the body's name |
| Scan a body | system and body ids | the body's name |
| Reach a rank | career and a number | any rank word |
| Board a ship | the hull's type | the ship's name |
| Collect the Ancient Key at a Guardian beacon | a `CollectCargo` of `ancientkey` while in a Guardian beacon system's id (the beacon's three pylons are charged with laser or railgun fire and its core scanned first) | a `DataScanned` alone, or the key collected in any other system |
| Fit out a ship that can reach the beacon | a `Loadout` whose modules include a fuel scoop and a laser or railgun (`hpt_pulselaser`, `hpt_beamlaser` or `hpt_railgun`) and whose `CargoCapacity` is 1 or more (a cargo rack), or a `CarrierBuy`; `MaxJumpRange` is never read, and a range stored in the objective is only the figure in its "easier with" sentence. For a fleet carrier owner a `Loadout` with a laser or railgun meets it, and the cargo rack is not read. An `FSDJump`, `Location` or `CarrierJump` into the beacon's system, or a `CarrierBuy`, also meets it, in any ship, unless the ship had no laser or railgun when the chapter was written. Only a chapter whose Guardian beacon is out of reach has one | the ship's hull or name |
| Collect bounties | `Bounty` events | anything else |
| Earn kill bonds | `FactionKillBond` events, optionally for one `AwardingFaction` | the faction fought |
| Earn kill bonds on foot | `FactionKillBond` events while the Commander is on foot: after a `Disembark` or a `DropshipDeploy`, or a `Location` or `CarrierJump` with `OnFoot` true, and before the next `Embark` | a kill bond earned in a ship or SRV |
| Complete missions | `MissionCompleted` events, optionally for one `Faction` and a mission family that starts the internal `Name`, such as `Mission_Courier`, `Mission_Scan` for a planetary scan job, `Mission_Hack` or `Mission_OnFoot_Hack` for a Hack mission from a ship or on foot, or `Mission_Disable` for a settlement Disable mission | the mission's title |
| Sell tons | `Count` in `MarketSell`, optionally of one `Type` or at one `MarketID` | the commodity's display name |
| Refine tons | one ton per `MiningRefined`, optionally of one `Type` | the commodity's display name |
| Step out on foot | `Disembark` onto a planet, not a station, optionally on one body | anything else |
| Collect items | `Count` in `CollectItems`, optionally of one `Type` or `Name` | the item's display name |
| Analyse organics | `ScanOrganic` with `ScanType` `Analyse`, optionally of one `Genus` | `Log` and `Sample` scans |
| Map bodies | `SAAScanComplete`, optionally of one body | anything else |
| Survey signals | `SAASignalsFound` with a `Signals` entry of one type, such as `Thargoid` or `Platinum` | the body's name |
| Land at a wreck | `Touchdown` at a `$Settlement_Unflattened_Wrecked…` destination, of one type; unfiltered, only `Unknown`, a crashed Thargoid ship | the destination's display name |
| Log codex entries | `CodexEntry`, optionally of one `Category` or `SubCategory` (Biology, StellarBodies, Civilisations or Guardian), and with `new` true only an entry whose `IsNewEntry` is true, the first time it is logged in a region | the entry's name |
| Picture a new codex entry | the Commander saves their own picture (from a new capture, on the entry's page in the main window) for an entry whose `CodexEntry` with `IsNewEntry` true was logged in that region after the objective appeared, and the entry is on record | a picture for another entry, one saved before the entry was logged, one set from the Discoveries page for a Missing entry, or a **Re-crop** |
| Picture a known codex entry | the Commander saves their own picture, after the objective appeared, for an entry on record in that region | a picture for a Missing entry, a **Re-crop**, or a picture the entry already had when the objective appeared |
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
| Boost off a jet cone | `JetConeBoost` events, optionally one star: `neutron` for a `BoostValue` of 4 or more, `whitedwarf` for less | anything else |
| First footfall | a `Disembark` onto a body whose `Scan` said `WasFootfalled` false, each body once | a body already walked on, or a second disembark there |
| Discover a body | `Scan` events with `WasDiscovered` false, each body once, optionally one class: `Earthlike body`, `Water world`, `Ammonia world`, `Gas giant with water based life`, `Gas giant with ammonia based life`, or `N` for a neutron star | a scan of a body already discovered, or any other class |
| Deliver to construction sites | the `Amount` of each `Contributions` entry in a `ColonisationContribution`, optionally of one commodity `Name` | a `MarketSell` at a construction site's market |
| Fly to a Guardian site | an `ApproachSettlement` whose `Name` starts with `$Ancient`, in the objective's system and body ids | a settlement of another name, or on another body |
| Scan a tourist beacon | a `DataScanned` of type `$Datascan_TouristBeacon;` while in the objective's system id | any other data scan type, or a scan in another system |
| Drop out at a generation ship | a `SupercruiseDestinationDrop` whose `Type` equals the ship's name, such as `Generation Ship Lycaon`, in any system | a drop at another megaship, a `DataScanned` of `$Datascan_ShipUplink;`, or a tourist beacon scan |
| Claim a system | a `ColonisationSystemClaim` event | anything else |
| Unlock at a tech broker | a `TechnologyBroker` event whose `ItemsUnlocked` holds a module symbol of one tech broker item, such as `Guardian FSD Booster` | an unlock of another item, or an item the Commander is known to have unlocked already |
| Pledge to a Power | a `PowerplayJoin` event, for the named Power or for any Power when none is named | a join to another Power, or a `PowerplayDefect` |
| Reach a PowerPlay rank | a `PowerplayRank` event whose `Rank` is at least the asked rank | a lower rank |
| Work for a faction | `+` marks in the `FactionEffects` of a `MissionCompleted` for one faction, optionally in one `SystemAddress`; `++++` counts as four | anything else |

Only a stock story's chapter one uses the beacon trigger, as its last objective, and you cannot add one on the
form. The three site objectives are not authorable either.

Every trigger after the seventh is counted except the engineer stage, the beacon-reach objective, the tech broker unlock, the PowerPlay pledge and rank, the site objectives, and the once kinds (carrier buy, squadron, squadron founding and system claim), which each fire once. A counted objective fires when its total reaches its count, and only what happens
after the objective before it has fired counts. It has no place of its own; a chapter that needs it done
somewhere puts an arrive or dock objective there first. The card shows the running total, such as "Kill bonds
for LTT 7786 Labour: 3 of 8", and a catch-up after d47 was closed rebuilds it from the journal. The ship's core also says
the total as each counted objective moves forward, such as "3 of 5 bounties collected.", as written and with no model call; an
objective counted in tons or credits says it at each quarter of the count, and the increase that completes the objective
is left to the beat's own line. These
mission families are set aside, and no objective uses or counts them: `Mission_Massacre_Skimmer`,
`Mission_DisableMegaship`, `Mission_RS_` and `Mission_DS_`.
A story steers illegal missions to Anarchy space but does not enforce it. With Crime at 0 no crime objective is drawn. Illegal families are any whose name contains `Illegal`, `Mission_OnFoot_Heist` and `Mission_OnFoot_Sabotage`. The planner puts an illegal mission objective directly after an arrive or dock objective in an Anarchy system within reach, and refuses the chapter's plan when none is in reach. Any completion of the family still counts, whatever its target. While an illegal mission objective is current, approaching the target settlement of a live mission of that family, when a faction other than Anarchy runs it, makes d47 say once per mission that the job is a crime there.

The planner is given the sites within reach: up to three Guardian sites, landable bodies with a Guardian surface signal found by the galaxy search, up to three tourist beacons and up to three generation ships (megaships named `Generation Ship …`) from the signal index, nearest first. A site in a system the Commander has already visited is left out. The tourist beacons and generation ships are found only while the **Signal index** setting is on, which fetches the index when a chapter is written as it does for a signal search; with it off only Guardian sites are found. In an act-one travel chapter the planner may make one of these sites a stop, which counts toward the chapter's stops; with none found it plans no `site` objective. The writer writes only the site objective's words.
A suit mod or livery change is seen when the next `SuitLoadout` or `Loadout` is written, so the objective may fire some minutes after the change. A suit, weapon or ship with no earlier loadout is only remembered: its first loadout counts nothing. A livery objective names no paint job, kit or decal. New drafts never ask for one, because the journal cannot show which paint the Commander owns, and the resolve step refuses one; a stored adventure with a livery objective still loads and counts. The first `CarrierLocation` seen for a carrier sets the baseline and counts nothing; a squadron carrier's is ignored. No objective may need an ARX purchase, because the journal cannot show one; the chapter writer is told so.

**Conflict and faction objectives.** A conflict objective counts taking part, not winning: a conflict lasts days and other players move it. The side is the faction the first counted contribution went to, and contributions to the other side do not count. A bond in a system with no active or pending conflict does not count. d47 reads the conflicts from the `Conflicts` of `FSDJump`, `Location` and `CarrierJump`, as they stood when the Commander last arrived, so a war that began after that arrival is not seen until the next one. When a later arrival shows the conflict ended (an empty `Status`), the standing records whether the side with more `WonDays` was the Commander's, and the next chapter's brief says so. For a faction objective, the next chapter's brief says whether the faction's influence rose or fell between two visits a day apart. The writer is told both kinds and up to eight active or pending conflicts in systems the Commander has visited; a conflict objective may name no faction and leave the Commander to find one. A faction objective counts mission marks only; trade, data sales and bounties do not count as faction work.

**Carrier, squadron and team objectives.** The planner builds a kind only where the Commander can do it, and refuses a Goals step of a kind the Commander cannot do:

| Kind | Allowed when |
|---|---|
| `carrierbuy` (once) | 7,000,000,000 credits at the last load (the carrier costs 5,000,000,000, so its reserve rule needs only 5,500,000,000), and no fleet carrier owned |
| `carrierjump` (counted) | a fleet carrier owned |
| `wing` and `multicrew` (counted) | always |
| `squadron` (once) | not in a squadron |
| `squadronfound` (once) | 20,000,000 credits at the last load: the 10,000,000 it costs and a reserve of the same; and not in a squadron |
| `pledge` (once) | the journal has said whether the Commander is pledged, and they are not; the Power is one of the twelve spelled as the galaxy search's `power` filter, or none for any |
| `powerrank` | pledged, and the asked rank is above the rank held |

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

**A stock story's cast can be answered.** Every cast member the running stock story has introduced is
on the Story [comms channel](comms.md#choose-who-your-comms-key-reaches). While one is selected, what you
say on the comms push-to-talk goes to that member, with no time limit and no limit on replies. They
answer in the first person, in their own voice, knowing their last line, what has been said since, and the story's current objective with its reason.
Push-to-talk still goes to the ship. The member reads
the hidden story only as far as that member would know it at this point, so they may hint at the secret but never state it, and never
mislead you about fuel, cargo, credits, routes, rank or danger. A reply that contradicts your ship, such
as one counting on a fuel scoop you have not fitted, is not said. The member is
offered no tools, so nothing they say can act on the ship. Each reply is one model call, counted in your
spend like conversation, and is kept on the story's page on the Story bench from the member, with their picture
and the clip it was spoken in, below what you said. No one is on the Story channel while the story is paused or
switched off, or while the game runs without Odyssey, and a member the story marks as heard only in
recordings or messages is never on it. Ask the core whether one
of them was real and it says they are someone in the story. The notes you keep on a cast member on the
**Characters** page are added after the story's own description of them, so their replies follow both.

### Where you are in it, and what it says

An objective speaks when it fires, and hands over to the next one in the same breath:

```text
The log's last entry is dated the day the beacon went quiet.
Next: dock at Maren Anchorage in Dyson's Hollow.
```

The hand-off names the place, the act and why the Commander goes — never the next objective's title or its
line, which is the spoiler rule holding. A scan's hand-off says *how*, because "scan X" sent a
Commander looking for a detailed surface scanner when the ship's own scanner was what the story
meant. It also says that going there counts: a body already scanned writes no second `Scan`, so a
scan objective is satisfied by the approach as well, and a story cannot strand a Commander on somewhere
they had already been.

**No counts where you read.** The card says the story's name and where it is — *not yet begun*, the
current objective's title, or *finished*. *Objective 3 of 7* is checklist language and stays off the card.

### The NEXT strip

On the full panel, under the title rule of every tab, a one-line NEXT strip shows the story's next objective
with its system and a copy glyph. When you are also working a campaign front, a Story and Campaign toggle
appears at the right end of the strip. Campaign shows the first sentence of the front's next action, the
front's kind and the faction it concerns, and the glyph copies the system. With only one of the two, the
strip shows that one and no toggle. The choice is kept across restarts and is the same on the desktop and in
the headset. The mini panel has no strip.

### Stopping, and starting again

**Remove** a begun short story and it stops telling you: no objective fires, the AI drops it from what
it knows, and an objective waiting out its settle window is discarded. It asks first, because a story
three objectives in is work you did, and the record goes with its messages. A short story has no
Abandon button.

A short story that is finished, failed or abandoned is not listed on Your story.

Remove is yours alone, reachable from the panel and nowhere else.

### Nothing here is callable by the model

Generation, beginning, abandoning and removing are all your acts, on the panel. Asking for a short story,
and accepting, changing and rejecting a draft, are yours too, by voice or by their buttons, and so are pausing and resuming
a stock story, by voice or by the **Story on** checkbox, and asking for the story so far. the core can
*read* the story — that is what it is for, so it can play off it — and can change nothing about
it. A hostile message arriving in your comms panel cannot propose a story, end one, or delete one.

It also costs nothing: none of this is on the advertised tool surface.

### Accepting, changing or rejecting a draft by voice

While a generated short story waits for your yes, three phrases act on it as its buttons do:

| Say | Does |
| --- | --- |
| "accept the short story" | The same as **Accept**. |
| "change the short story" | Opens the voice entry for what should change, as **Change something** does. |
| "reject the short story" | Removes the draft, as **Decline** does. |

They act on the draft whose page is open on the Stories tab. With no draft page open and exactly one draft,
they act on that one. With two or more drafts and none open, d47 says there is more than one draft and to open the one
you mean, and changes nothing. With no draft the phrases are not heard. The page stays in view and nothing reads the
draft aloud. The model is refused all three.

#### `change_adventure`

Open the entry for a remark that changes the Commander's draft short story. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `accept_adventure`

Accept the Commander's draft short story, as the Accept button does. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `reject_adventure`

Reject the Commander's draft short story and remove it, as the Decline button does. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Your story

Every objective said is also kept as a written message, from whoever said it, so a line said during a fight
is not lost. A stock story's lines are listed on that run's page on the **Story bench**, below. **Your story**,
the first sub-tab of the Stories tab, lists the endings waiting for your answer, and nothing else from the messages.

**Waiting for your answer** shows each story ending still waiting for your choice, with its
answers, **Play** and **Change voice**. A message that was spoken has a
**Play** button that plays the clip it was spoken in; see [Spoken messages keep their clip](speech.md#kept-clips).

The messages live in `data/messages.json`. The file holds the most recent 200; past that the oldest read
message goes first. A stock story's lines are also kept in the story's own record in `data/story-runs/`,
which has no limit; see [Spoken messages keep their clip](speech.md#kept-clips).

Abandoning a story, or shelving it by switching to another, keeps the messages it posted, as finishing one does: its objectives,
nudges, clues, beacon scan and ending. An ending question from a stopped story is not listed as waiting for an answer. The messages of a short story you asked for stay
when you abandon it.

### Story bench {#story-bench}

Open **Story bench** on the Stories tab, after Short Story. It lists your stock-story runs in four groups:
**Active**, **Shelved**, **Finished** and **Stopped**, the last naming whether the run was abandoned or ended.
Short Stories are not listed. Each row shows the title and its state, then Length, Chapters, Clues (given out of
total), Picked and Last line, the time of the newest line in the run's record, or a dash when the run has none.

A shelved run has **Switch to**, and so does the **No story** row below the shelved runs. Either opens a
confirmation naming the story to be shelved and what comes back. **Switch** shelves the active story and makes
the chosen one active again, paused; on **No story** the active story is shelved and none is active. A chapter
being written refuses the switch.

Under the run list, whenever a story is active, the **Beat sheet** section lists every beat the story's length has, in
story order, five to a row, by number and name: Opening Image, Theme Stated, Set-Up, Catalyst, Debate, Break into Two,
B Story, Fun and Games, Midpoint, Bad Guys Close In, All Is Lost, Dark Night of the Soul, Break into Three, Finale and
Final Image. A short story has fewer: a week runs from 01 Opening Image to 07 Final Image. Each beat reads **✓ Reached**
once its stage has passed, **Current** while the story is at its stage, and **Later** before that; Final Image stays
Later until the last finale chapter begins. The count at the right reads, for example, `8 / 15 REACHED`. Only names are
shown, never a beat's line. With no active story the section is not shown.

**Open** on any run shows that run's page one level below the Story bench, named by the story's title. A strip gives its state,
length, chapters so far, clues given, and the days it was picked and last spoke. Below it are every line the run's
record holds, in order, in one section per chapter with the chapter's name and the day of its first line. A clue,
reaction, ending or other story line said between chapters is listed under the chapter before it. Each line shows
the speaker's picture and name, its kind (Objective, Clue, Reaction, Ending or Story line), the time and the
text, **Play** and **Play from here**. Play plays that one line through the same player as Play all and then stops; a kept clip is played as a message's Play does, a clip in your own voice included. **Jump to** has two lists that open in the page: **Chapter**, then **Objective** within that chapter.
Choosing one scrolls the page to it. A run from before the record was kept says **No lines kept** and shows each
chapter's text as it was told instead.

**Play from here**, on every line, plays the run's lines in order from that line. **Play all**, at the end of the Jump to row, plays them from
the chosen chapter or objective. The line playing is marked **▶ Playing** on a cyan
ground, and a playback box opens over the window, and over the headset's big panel, half the width of the window. Its head says
**Playing 23 of 142 · Ysolt Garrow** with the chapter's name and the line's time, then the speaker's picture whole on black, a
progress line, the speaker's name with the time played, the line, and **Pause**, **Next** and **Stop**. A single line's Play
leaves out the count and **Next**. **Stop**, Escape and closing the box end the playback. While it plays, as while a single line's Play does, other speech is silenced: an answer, a callout or radio chatter
from anywhere else is dropped, not saved for later. Music, ambience and cues play as usual. A danger alert pauses
playback and is spoken; so does pressing push-to-talk, whose turn runs and whose reply is spoken, and the box says
**Paused for push-to-talk. Press PLAY to carry on.** Playback stays paused until you press **Play**, which plays the
paused line again from its start. It keeps playing when you leave the page. The shut-up control, or **Play** on a
message, stops it. **Play** on a single line replaces it with that line.

The mini panels, on the desktop and in the headset, have no buttons, so while a playback runs they show a read-out in place
of their text. The avatar slot shows the playing line's picture whole, with a 3px progress line along its bottom edge, cyan
or amber while paused. Beside it are the head, **Playing 3 of 6 · Ysolt Garrow** or **Paused · …** with the time played
(a held line shows its countdown), the line, which scrolls by itself to reach its end as the clip does, and the hint
Say: “play”, “pause”, “next” or “stop”. When the playback ends the panel shows what it showed before.

A line whose clip was not kept says **No clip kept. Played, it is spoken again in the speaker's current voice.** That
covers a line posted with speech off, one whose synthesis failed, one in a voice you deleted, and a clip file that is
missing or cannot be read. Play or Play all speaks it in the voice its speaker has now: a cast member in the voice chosen
for them or else the story's own, the Narrator's and a core's from the current voices. Each one is billed as speaking any line
is, by the provider that voice belongs to. The new clip is kept in the run's record, so the line is synthesised once. During
Play all the next such line is made while the line before it plays, and while a line is made the box reads
**Speaking again · 24 of 142** with the speaker and a gauge. A line in a voice you asked to forget, your own
recording or a deleted custom voice, is never spoken again in it.

A line that cannot be spoken, because no voice provider is set up, the provider fails, or its voice was forgotten,
is shown for reading instead: half a second a word, at least four seconds, and then Play all moves on. The box
reads **Shown for reading · 25 of 142** with a countdown, and a **No voice for this line** notice says why, with the
provider's error. Pause and Next work as for a spoken line; Play after a pause shows the line again from the start of its time.

**Delete** is on every Finished and Stopped row and on the page of every run but the active one. It asks first:
**Delete this run of** the story's title, with the run's line count and **Clips on disk**. **Delete run** removes the run
from the story file, its folder of lines and clips, its archived chapters and its local rating vote; a vote already
sent is not withdrawn. The story stays in the Catalogue, and picking it again starts a new run. The active run cannot be
deleted. **Delete** on a shelved run's page opens **Abandon it first**, because a shelved story can come back; **Abandon**
there abandons the run, and it can be deleted afterwards. Deletion cannot be undone.

By voice, "switch to the story" and a shelved run's title does what **Switch to** does, without the confirmation, and
"play back" and a title opens the page of the newest run with kept lines under that title and starts **Play all** from
its first line. "Switch to the story" is the whole phrase: "switch to" alone also switches cores, Commanders, tabs and
the game, so a story called after a core still switches by this phrase. Where a title has several runs the newest is meant.
Both are the Commander's alone, and the model is refused them.

With no runs the page says so and offers **Open the Catalogue**. Started with `D47_STORY_BENCH=1`, the Jump,
Beats and Checkpoints sections for testing the active story follow the list; without it they are absent.

### A nudge when a story stalls

When the next objective has waited through three play sessions and seven days since the last objective or nudge,
the Narrator's next narration leans toward it. It may hint at where the objective waits; it never quotes the
objective's line. It is kept on the story's page on the Story bench from the narrator, and each story is nudged at most once each
time D47 runs.

### Stock stories

Open **Catalogue** on the Stories tab for the stock stories, each written to run for the length on its
card, a chapter at a time. Until stories are added to the catalog the page says "No stories yet." A sentence
at the top narrows the list: "Show All stories of Any length". Press a word in it to open its choices under
the sentence. The level takes any number of ticks, and with none ticked every level shows. Each story is a
card with its pictures, its title, its length, the level of Commander it was written for, the Guardian core
the story is written for, and a blurb saying why you might pick it. The level is `new` (no engineering done yet), `midrange` (some
ship engineering done) or `endgame` (most ship and on-foot engineers unlocked, and at least one ship, suit
and weapon fully engineered). It is a guide to choosing, not a limit: every story can be picked at any
level, D47 does not work out your level, and where the story's backstory disagrees with your real ships,
credits or ranks, the chapter writer goes by what is true now. A story's page adds the length
beside the level's guideline, the tone, the story in your words and why it sends you to a Guardian beacon.

Each story also has a hidden layer: its secret, a beat sheet of Save the Cat beats, a clue for each of its
length's clue days, a line for each finale chapter, one to four ways it can end, and up to four
speakers who exist only in the story. Each of those speakers has a local voice, Kokoro or Chatterbox, and a
story may mark a speaker `pinned: true` so that it always speaks in that voice. A speaker that is not pinned
speaks through the provider you set for the ship, so a story never sends its lines to a provider you did not
choose. A speaker whose voice is `own` is pinned. Every clue and finale
line names who says it: one of those speakers, the core or the narrator. D47 keeps that layer
sealed, never shows it, and sends it to the language model so the chapters can hint at it.

A speaker may be written in two versions: `forMan`, the version a Commander who is a man meets, and
`forWoman`, the version a Commander who is a woman meets. Each version has its own name, voice and
picture. The story's card never names that speaker and calls them by role ("the engineer"); the hidden
layer names them as `{name:<cast-id>}`, and D47 puts in the version's name before any line is shown,
spoken or sent to the chapter writer.

A story whose children call the Commander Mum or Dad writes `{parent}`, the word they use for the Commander, and
`{parent:<cast-id>}`, the word a member in two versions uses. The hidden entry gives the first as
`"parent": {"man": "Dad", "woman": "Mum"}`, and each version carries `"parent"` beside its name and voice.
D47 puts in the word for your Commander's gender in the same places it puts in a name. A story with these words
needs your Commander's gender before it can be picked, as one with two versions does.

For such a story, the story's page asks **Your Commander is**: **A man** or **A woman**, above **Begin story**.
**Begin story**, **Continue story** and **Switch** stay disabled until you choose, and the page says why. The choice is kept per
Commander and is asked only by a story that needs it. The same control is on
the page of your running story; changing it there takes effect from the story's next line.

**Begin story** makes the card's words your Backstory until the story stops, and has the core write
chapter one, which then begins. D47 keeps the Backstory you had before as your default; a Backstory that is some
story's own words counts as none. Each story keeps the Backstory it had when you shelved it or switched it off, edits included. Act one ends when you collect the Ancient Key at the Guardian beacon nearest to you; arriving in the beacon's system is not enough. The beacon's three pylons must be charged by laser
or railgun fire, all within a few minutes, before the scan opens its core and releases the key; the objective's hand-off says so. Before each act-one chapter D47 checks whether
the ship you are flying or any ship you own can reach that beacon: a ship can when a laser or railgun is fitted in any mount, a cargo rack is fitted, and either you own a fleet carrier or a fuel scoop is fitted. A stored ship counts by its remembered loadout, and not while it is in transit. When only a stored ship can, the chapter ends at the beacon and its first objective is boarding that ship; d47 measures the chapter's later stops from the system where it is parked. Jump range
never decides it; a ship with a short range is told the trip is easier with a longer one. When it can, the
chapter ends at the beacon. When it cannot, the chapter has no beacon objective, keeps to a session's
flying, works toward a ship that can make the trip, and ends on an objective met once your ship can make it; the chapter writer is told the beacon, its
distance, and whether the missing fuel scoop, the missing laser or railgun, the missing cargo rack or any of them stand in the way. The chapter's last line is said before
the core wakes, in the voice that was aboard. Each chapter is a story on this tab. When one finishes, the next is written from it
and begins. **Switch** shelves the running story and picks another; it never abandons one.

#### A shelved story

You have one active story at a time, running or paused. Switching to another story shelves the active one:
the chapter under way is abandoned and the story is switched off. A shelved story gives no objectives, nudges
or clues, holds back no Guardian cores, has no objective on the panel, and its days do not count; its
messages and its last two chapters are kept. Any number of stories can be shelved. **Continue story** on a shelved story, or **Switch** to it, makes that same run active again rather than starting it over. It comes back paused,
and **Resume** writes the chapter that was under way again from where you are. A shelved story's chapter
offers no **Begin again** on its page. To end a shelved story, make it active and **Abandon** it.

A story of 3 days, 1 week or 2 weeks has no trip to a beacon: it begins after you have scanned one. At
**Begin story**, the scan is narrated in the words the story was written with, kept on the story's page on the Story bench
from its speaker and said aloud. Then the cores wake and the story's core comes aboard, as for a real scan, and
chapter one begins in act one with no beacon objective. The story's days count from the pick. A story of 1
month or longer keeps the real scan, unless a scan is on record.

A scan is on record when any other story you picked, running or stopped, recorded a real beacon scan
while it was current, or itself opened with one on record. A scan made with no story running, or before
d47, is not on record, and no journal history is read for it. A story of 1 month or longer picked while a
scan is on record begins with the scan behind you: no trip, no beacon objective, chapter one in act one,
and its days counting from the pick. No scan line is said. Once the opening lines, if any, have been said,
the story's core comes aboard and D47 says "The Guardian cores from your last beacon scan are still in the
ship's systems. {core} has come aboard for this story." The
chapter writer is told the trip is already behind you, and the story's page leaves out "The beacon".
A story whose card says it needs its own beacon trip flies to a beacon even with a scan on record, as if none were.
The Stories page shows a checkbox, "Fly to a Guardian beacon again in my next story", once a scan is on
record. Ticking it makes the next story of 1 month or longer you pick fly to its own beacon as if no scan
were on record; it never changes the story you are running. A real scan in that story clears the tick, and
that scan is on record from then on. Unticking before you pick restores the remembered opening. Only the
panel sets it; it is not a tool.
**Abandon** ends the story, and so does finishing it; either puts your Backstory back to your default, the one you
had before your first story, or to empty. Switch saves the Backstory with the story it shelves and brings back the one saved with the story it makes active.
Neither changes your Guardian cores. **Abandon** on the current chapter's page ends the story, as it
does on the Catalogue; that page also has **Story on**, which pauses the story.

Each story is written for one Guardian core, shown on the list and on the story's page as "Core". When
you scan the story's first Guardian beacon, or at the pick of a story shorter than a month, that core comes aboard in place of the core you had, says
the waking line, and a line on the story's page on the Story bench says it came aboard. A
core you choose afterwards stays; the story does not bring its own back. Pausing, abandoning or finishing
the story leaves the core aboard as it is.

One story runs at a time, kept in `data/story.json`. When a story is picked again, the earlier run stays in `data/story.json`. A story needs a language model and galaxy search,
the same as asking for a short story. A chapter or short story is written by the provider's default model when the chosen model is GPT-6 Luna or Claude Haiku 5.5.

Stories need Elite Dangerous: Odyssey, because most chapters use it: on-foot missions, settlements,
exobiology and suits. D47 reads the `Odyssey` flag on each `LoadGame`. When the last one says
`"Odyssey":false`, the Stories page says why stories are off, **Begin story**, **Continue story** and **Switch** are disabled, a
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
section when they land. **Begin story** on a story whose hidden file is still not on disk fetches it, reads
**Downloading** while it does, and starts the story when every file is in place. On a failure the page says "The story could not be downloaded. Check your
connection and try again." and nothing starts. At startup, a story that is running or paused and
has no hidden file on disk is fetched, and one that has fetches any cast picture the release has and
`data\stories` does not.

A story can be published again with changes. Its card in `index.json` then carries `sealedSha256`, the
SHA-256 of its hidden file, and each time the list lands, a story on disk whose hidden file has another
hash is fetched again. The new file replaces the old one only once it and its cast pictures are in place,
so a failed fetch leaves the old one; pictures already on disk are not fetched again. A card without
`sealedSha256` is never fetched again. A running story reads the new file from its next line.

**Download stock stories**, on by default, is in the Stories settings. Off, nothing is fetched and
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

Each card on the Stories page shows its stars and vote count, or five empty stars and "(No ratings yet)". The **Rating** and **Sort** words
in the Catalogue's filter sentence set the least star rating and the order, and **Clear filters** resets
them. A story's page shows its average under the title. For a story you have picked it also shows
**Your rating**: click a star to rate, click your current rating again to take it back, or use Left,
Right and Delete once the stars have focus. A vote that could not be sent says so, and is sent again
the next time the Stories page opens. When the averages cannot be fetched, every story shows as unrated,
with no error, and they are asked for again the next time the page opens.

**Story ratings**, on by default, is in the Stories settings. Off, nothing is fetched or sent and the
Stories page shows no ratings.

### Cast pictures

A speaker may have a picture, downloaded with the story as `<story-id>.<cast-id>.jpg` into
`data\stories`; a speaker in two versions has one for each, ending `.for-man.jpg` and `.for-woman.jpg`. A
speaker marked `primary` is a recurring character named on the story's card, and always has one. A line
that speaker says is kept with the picture: the story's page on the Story bench shows it beside the line, and a
message's own page shows it above the text, at most 240 px wide. A line from the core or the narrator has none. A story's own page shows each line's
speaker picture beside it, the core's and the narrator's included, and pressing it opens it full size.

While the speaker's line plays, including an answer to you and a line played again from the Story bench or Your story, the
avatar shows the picture whole in place of the core aboard: in the main window, on the mini panel, and on both
VR panels. When the line ends, the core shows again. A speaker whose picture is not on disk leaves the avatar
showing the core.

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

The opening and each objective have one to three lines, spoken in order. When you hear a cast member in a
chapter (a recording plays or a call comes in), their words are a line in their own voice, not a
description of what they said. A cast member may give their own name only when introducing themselves:
"This is …", "My name is …" or "… here".

- **`ship`** is the core aboard, in the ship's voice as you set it under **Settings › Your core › Its voice**. While the core
  aboard is stock COVAS, a `ship` line is spoken in the narrator voice and posted from the Narrator, and
  the chapter writer is told so, so it writes those lines as narration. With a Guardian core or a core you
  wrote aboard, that core speaks them. This is decided when the line is spoken.
- **`narrator`** is the narrator voice set under **Settings › Other voices › Narrator and stories**.
- **A cast member** the story marked `pinned: true` speaks in the provider and voice the story gave it,
  Kokoro or Chatterbox, whatever **Where each voice comes from** says. Any other cast member speaks through the
  provider you set for the ship, in the voice paired with it for that provider, or in the crew voice while none
  is paired. Either kind speaks in the voice you chose for it, if any (see [Cast voices](#cast-voices)). A speaker in two versions speaks in your version's voice, and
  changing **Your Commander is** takes effect from the next line. The line goes through that speaker's
  own sound, a comms link at the story's strength and any Guardian effects the story lists, and nothing
  else: no radio for its role and none of the **Guardian Voice Effects** you set for the ship. The model
  writes a cast member's clue as that member, from their name and who they are.

**A cast member can be aboard your ship.** A story may give a member a span of its beats, from the one
they board at to the one they leave at. Boarding and leaving happen at an objective: when the chapter
writer is given the beat, it places the boarding or leaving on the objective, or the opening, where that
beat happens, and a chapter that does not reach the beat leaves it for a later one. A chapter written
after the beat has passed with nothing placed opens with the boarding or leaving already done. A chapter
opens with the members whose boarding has happened and whose leaving has not, and the writer may add
others for that chapter alone. While a member is aboard, the chapter writer writes their lines as spoken
in the cockpit, and their lines are spoken without the comms link; their Guardian effects still apply. A
chapter's line is voiced for who is aboard at its own objective. Clues, reactions and replies are voiced
for who is aboard at the last objective you completed. A boarding or leaving counts for the next chapter
once its objective has fired. A boarding waits for a free seat: the ship's crew size, less you, less the members already aboard.
While your ship has none, a boarding that comes due is still offered to a chapter that moves you into a ship with enough seats: the
writer is told how many seats the boarding needs and which ships have them, and places it on or after the objective that boards such a ship.
A boarding no ship you own or can afford could seat is not offered. One the chapter does not place is held, and the first chapter written
while your ship has a seat is told the boarding is due and places it as a scene.

**A cast member can live on your fleet carrier.** A story may mark a member's span as on the carrier rather than
your ship. That member boards and leaves the carrier at their beats as a ship rider does, needs no seat, and is
never counted as aboard your ship. While you are docked at your own carrier they are heard in person. Away from
it, they are heard over comms that fade with distance: clear within 250 light years, then more broken up
farther out, though never lost entirely. Beyond 500 light years they cannot hear you, and a reply to them is refused as the captain's
is; their story lines are still spoken. With no carrier of your own, a squadron carrier, or a carrier in a system
you have no position for, they are heard through the story's own comms sound. A chapter that lists them aboard
your ship puts them in the cockpit.

Each line is kept on the story's page on the Story bench under its speaker's name, with the speaker's picture and
the clip it was spoken in. When a chapter's objective belongs to the narrator or a cast member, that speaker says the objective's line
as written and the ship then says where you go next. A line may open with a sound such as
`[static crackle]`: a voice that performs sounds performs it, and every other voice has it taken out.

```csharp
public static StoryLineVoice Of(
    string? speaker, StorySecret secret, string? gender, Persona.Persona? core, IReadOnlyDictionary<string, StoryVoiceChoice>? choices = null,
    ShipSpeech? ship = null)
```

#### Reactions between chapters {#cast-reactions}

While a stock story runs and is switched on, one of its primary cast may say one line about something you did.
The things that count are the ones the chapter writer is told: a death and its rebuy, an interdiction or an
escape, a promotion, an engineer invited or unlocked, a ship bought, a hull you brought home below a quarter,
your largest sale, bounty or exploration data sale, and a first discovery. The newest since the last reaction
is the one reacted to.

- There is at most one reaction a play session, counted by the game's `LoadGame`. The line is said the next
  time you are docked and in your ship, outside a fight, two minutes after the last unprompted line, and after
  a clue owed at the same time. On foot on the station it waits until you are back in the cockpit.
- The model picks the member from the primary cast, given each one's name and who they are, and writes the
  line as them. A reply naming anyone else is not spoken. The model is given the event as the journal
  recorded it, with no other Commander's or pilot's name, and the hidden story as far as that member would know it, and
  is told to state no new fact about the story.
- The line is spoken in that member's voice and kept on the story's page on the Story bench from them, with their
  picture and the clip it was spoken in.
- Nothing is noted while d47 catches up on the journal at start-up, while the story is paused, switched off or
  without Odyssey, or before chapter one begins. Without a language model, or with personality off, no
  reaction is spoken, and the next session tries again.
- **Cast reactions**, on the Stories tab, switches reactions off for every story. Clues and chapter lines
  are unchanged.

#### A story needs its voices first

A story cannot be picked until every voice its cast speaks in is ready, the voices you chose included. A
member that is not pinned needs only the ship's provider, so a story with no pinned member starts without the
Kokoro download.
**Begin story**, **Continue story** and **Switch** are disabled, and the story's page lists what is missing:

- the Chatterbox download and its size, when a member speaks through Chatterbox and it is not downloaded;
- the Kokoro download and its size, when a member speaks through Kokoro and it is not downloaded;
- a recording of Your voice under **Settings**, **Providers**, **Speech synthesis**, **Custom voices**, when a member speaks in your own voice;
- the provider's API key under **Settings**, **Providers**, **Speech synthesis**, when you chose a provider that needs a key and
  none is stored.

The **Local voice** and **Chatterbox voice** download rows, in Settings › Providers › Speech synthesis, appear while a downloaded
story's cast needs them and the model is not downloaded, even when no slot uses that provider. A pick tried anyway is refused, and the ship
posts one message listing the same things. Your story does not list it.

If a voice a running story needs stops being ready, for example because the recording was deleted, the
story is paused within a few seconds, its chapter stops, and the message is posted again. **Resume** is
refused, with the same list, until the voice is back.

### Cast voices {#cast-voices}

A story's card page has a **Cast** section listing its primary characters, and no one else, before you
pick the story. While it runs, the characters it has introduced follow them. A character in two versions is listed once **Your Commander is** is set,
as your version. Each row shows the character's picture, name, their role (who they are to you in the
story, such as "The search-and-rescue coordinator at Ebbrells Watch"), and the provider and voice it speaks in,
with "(the story's voice)" while it is the one it speaks in without your choice: the story's pinned voice, or
the voice paired with it for the ship's provider. **Play sample** speaks the character's name
and one fixed sentence in that voice. **Change picture** and **Use the default** work as they do on a
message.

**Change voice**, on the row, on an ending waiting for your answer, and under each of a story character's lines on its chapter's page, primary or not, opens a picker
of every provider that speaks, then of every voice that provider lists, of either gender. The story's own
voice is marked in the list. The story's text is not rewritten for the voice you choose. Each voice in the
list can be played first; on a paid provider each sample is billed like any line. **Use the default**
returns the character to the story's voice, or to the ship's provider for a character that is not pinned. A character that is not primary is listed on Asset Mgmt › Characters and on the Story comms channel
once the story introduces it.

The choice is kept in `settings.json` under `storyVoices`, keyed `<story-id>.<cast-id>`, with
`.for-man` or `.for-woman` for a character in two versions. Lines spoken through a paid provider count in
the speech spend with every other line. If the chosen provider fails a sentence, that sentence is spoken
in the character's own voice, which for a character that is not pinned is its voice on the ship's provider,
and the character's row on the card page says why.

A character that is not pinned is given a voice from the ship's provider's list the first time you open the
story's page on that provider, even before you begin it; when the story starts or resumes; and again when you
change the ship's provider while the story runs. A character in two versions is given one once **Your Commander is**
is set. The language model chooses it
from the character's name and who they are, told the gender of the voice the story wrote the character for;
with no language model, a voice of that gender is picked from the list. While the list has enough voices, no
two characters of one story share one, and none shares the ship's or the Narrator's. The pairing is kept in
`settings.json` under `storyVoicePairings`, by provider, so changing back to a provider reuses its
pairing. Your choice wins over it, and **Reset voices** forgets every pairing. Until a character is paired,
it speaks in the crew voice, and its row on the card page says so until the pairing arrives. The request sends the cast's names and descriptions and the provider's voice
list to the language model, which the **Language model** entry under **Privacy and egress** says, and its
cost counts in the spend as the cores' pairing does.

```csharp
public static IReadOnlyList<(StorySpeaker Member, StorySpeakerShown Shown)> Unpinned(StorySecret secret, string? gender)
```

A character you put on a hosted provider sends its lines, which are the story's sealed text, to that provider.
**Privacy and egress**, under text to speech, names each such character, its story and its provider, with
that provider's own disclosure, and `get_data_egress` answers from the same entry. A character that is not
pinned sends its lines to the ship's provider, and the same entry says so whenever that provider is hosted. A
character on Kokoro or Chatterbox adds nothing. Only the panel changes these voices; the model has no tool for them.

```csharp
public static PinnedVoice Voice(
    StorySpeaker member, StorySpeakerShown shown, IReadOnlyDictionary<string, StoryVoiceChoice>? choices, ShipSpeech? ship = null)
```

### Characters who speak Japanese {#story-languages}

A story can mark a character as speaking Japanese. The language belongs to the story, not to a setting.

- **Comms.** The character answers in Japanese and the voice speaks only that. The English translation goes
  to the ship's AI and the story's page on the Story bench, and the character's own memory of the exchange keeps both. A reply
  with no translation, or one the claims check refuses, is not said.
- **Clues, reactions, the opening and the beacon scan.** These are given in Japanese too. The voice speaks the
  Japanese, and the transcript shows the English beneath it as a second paragraph in grey at a slightly
  smaller size. Copying the message copies both. A clue or reaction with no English, or one the claims check
  refuses, is not said.
- **In the headset.** A line spoken in Japanese is captioned with its English translation, shown for the whole
  line rather than a sentence at a time.
- **Change voice.** For such a character the picker offers only the providers that speak Japanese, and for a
  provider whose language follows the voice, only the voices that speak it. A provider with none says
  "lists no Japanese voices". A stored choice that does not speak the character's language is ignored, and
  the story's voice speaks.

### How a chapter is fitted to you

**Genre.** Each card has a Save the Cat genre, and the chapter writer is given that genre's three
elements, from Blake Snyder: a Buddy Love story keeps an incomplete hero, a counterpart and a
complication in play. In a Buddy Love story the partner exists only in the fiction; a chapter may offer
hiring a crew member but never requires it.

**Your ship and credits.** The writer is told your position, your ships, the jump range of the one you
are in, your credits at the last load, your fleet carrier and your ranks. In a chapter built on drawn
activities, d47 sets the counts and the stops; such a chapter has you buy a ship only where its Goals step
asks for one. In any other chapter the writer sizes counts and destinations to these facts, and may have you
save up for a ship and a later chapter have you buy and board it. A chapter is sized to finish in one to three play sessions, or
in one session in a story shorter than three months. A longer undertaking, such as engineering, saving
for a ship or a run of ranks, carries on across chapters.

**Activity, not only travel.** Chapter one, and a chapter that ends at the beacon, travel: their objectives are places.
From chapter two on, every other chapter is built on drawn activities.

**Steps.** An objective can take several steps, in order, each with its own place or activity. The objective's
lines are spoken when its last step is done. After any earlier step your core says it was done and what the next
step is, for example "Next: collect 3 bounties." Change objective, Mark done and Never ask this act on the step you
are on. A chapter has as many steps as its story needs. In a chapter built on drawn activities, d47 builds the
steps: the place an activity needs comes first in its objective, and a sight beyond reach is reached through stops on the way.

**Sightseeing.** With notable places on, any story's chapter after the beacon scan and before the finale,
other than one heading home or in a span, may be drawn as a sightseeing chapter, with a chance that grows with
your Exploration record and falls after a sightseeing chapter. d47 offers the eight places nearest you, none
in a system an earlier chapter of the story sent you to and none farther than the story's span can reach,
picks one by the chapter's seed in proportion to its rating, and builds an arrive, land or scan objective
there. A sight within 33 jumps of the best jump range in your fleet is an ordinary sightseeing chapter. A
sight farther away opens a span in the black toward the sight's system: the chapters out travel toward it,
and the one that can reach it carries the objective. Outside the bubble and not in the black, the sights
are only those in the Sol band above. Changing the objective of a sightseeing chapter whose sight is still
ahead gives another sight, and inside such a span the span heads for the new one.

**A credit reserve.** A chapter never spends the Commander to nothing. A story objective that buys something,
which is a `board` into a hull they do not own, `carrierbuy` or `squadronfound`, is allowed only when the
credits at the last load cover the price plus a reserve: the price again or 500,000,000, whichever is less.
Where a fixed threshold above is higher, it applies. A hull already owned is allowed at any balance, and a
hull with no price in d47's table is refused unless owned. The planner refuses a Goals step that would spend past
the reserve. Saving up and buying therefore fall in separate
chapters. In a chapter built on drawn activities a purchase comes only from its Goals step, which is held to the same reserve.

**The reach limits one hop.** A chapter's reach is the longest one hop may be, not how far the chapter
goes: a farther place is reached over several hops. In a chapter of a span the draw opened (below), the reach also
limits the chapter's total travel: the hops from where it starts through each place objective may not add up to more
than the reach.

**The finale's destination.** d47 chooses where the story ends when it plans finale chapter 1: a landable body in a real
system, with no permit needed, drawn by the chapter's seed. d47 looks around points between half the limit and the limit from you,
favours notable places and the story's sights, and favours systems you have not visited. It picks a nearer body only when none lies
between half the limit and the limit. It may be at most five hops at the
chapter's reach from you for each finale chapter after the first, and at least five: with a reach of 360
light years, 1,800 light years in a story with two finale chapters and 5,400 in one with four. Where no landable body is found, finale
chapter 1 is refused. The destination is kept on the
story. Each later finale chapter's writer is told the destination, how far it is from you and how many
finale chapters are left. A finale chapter between the first and the last that starts more than one
reach from the destination must end closer to it than it started. The last finale chapter's last objective
is a land objective on the destination, and that one objective may be farther than the reach. A 3-day story's one
finale chapter both names the destination and lands on it. In a later finale chapter built on drawn activities, d47
builds the stop closer to the destination from places found on the way to it, and the landing on the destination; where
no place on the way is found, the chapter is not written.

**Outside the bubble.** A story chapter that starts more than 500 light years from Sol
(`StarPosition.Origin`) must end 100 to 300 light years nearer Sol than it started, so a Commander left far out by the
beacon scan works back over several chapters. The writer is told how far you are from Sol and the band its last stop
must fall in: a chapter starting 588 light years out has a last stop between 288 and 488. The last stop is the system
of the chapter's last place objective, and a chapter with none counts as ending where it started. A chapter built on
drawn activities is built inside this rule, the home leg and the base rule below, with its last stop drawn from places
that meet them. The
rule does not apply to a chapter that ends at the beacon, a finale chapter, a rewrite or a short story, or to a chapter
that starts within 500 light years, or to a chapter in the black (below). The chapter also keeps one of its two place
objectives for this stop.

**Heading home from the black.** A chapter counts as changed when a **Change objective** detour interrupts it or it is written again with **Write chapter again**; **Never ask this** does not count. When the last two chapters were both changed and you are more than 1,000 light years from both Sol and Colonia, the story heads home: to the nearer of the two, or to Sol when you are within 1,000 light years of Colonia. Each chapter heading home uses the span reach R (`SpanReach.Of`: 100 times the best jump range in your fleet, at least 5,000 light years with a fleet carrier; 1,500 light years, or 5,000 with a fleet carrier, when no jump range is known), may have more than two place objectives, and is told where you are going and why it travels. Its last stop must be at least three quarters of the reach nearer the target than the chapter starts (2,250 light years at an R of 3,000, or 3,750 with a fleet carrier's 5,000), or within 1,000 light years of the target; a draft that misses is refused and goes back through the repair rounds. The chapter is offered the stations and landable bodies nearest a point on the way, found by a galaxy search around it, each with its distance from the target; when that search finds none or cannot be made, the chapter is not refused. The target is cleared by the first chapter written within 1,000 light years of it. Finale chapters and the chapter that ends at the beacon are not sent home. A chapter heading home is not held to the Sol band above. The changed chapters and the target are kept with the story across a restart.

**Ending near your ships.** Without a fleet carrier, your base is the station holding the most of your stored ships, not counting ships in transit, with a tie going to the one nearest you; with no ship in storage it is the **Home system** setting (`Callouts.HomeSystem`); with neither there is no base (`ShipBases.Of`). When a story chapter's last stop is farther from the base than the near-here reach (the larger of 80 light years and three jumps), the last objective ends with one more step: dock at the base station, or arrive in the home system. The writer is told the base and the rule, and a draft whose last step is not that dock, by market id, or that arrival, by system address, is refused and goes back through the repair rounds, where a repair may add the step after the last one. The step may be longer than the reach, and the Sol band measures the stop before it. Where the galaxy search cannot place the base, the step is needed unless the last stop is in the base's system. A station known only by its market id is named from the galaxy search; where none matches, the step is an arrival in its system. The rule does not apply to the chapter that ends at the beacon, a finale chapter, a chapter in the black (below), however many of them a span holds, a chapter heading home from the black, a place you asked to go to, or a short story. When such a story chapter starts, or heads for a place the story or you named, farther from the base than the near-here reach, and you have no fleet carrier, and the base is a station holding your stored ships, the writer is told to say in the opening why the story stays away from the base and to tell you to transfer there the ships you will need; once they are moved, the base moves with them. Where the base is the **Home system** setting, with no stored ship, there is nothing to move and the writer is told nothing. No draft is refused for lacking it.

**In the black.** A story's hidden entry may mark spans of its beat sheet as in the black. A chapter whose stage hands
the writer a beat inside a span is held to neither rule above: it has no Sol band, keeps no place objective for one,
is not sent home, and clears a home target already set. The writer is told the chapter takes place in the black and
its stops may be far from the bubble. A chapter in the black travels far only in a span, at the span reach. Outside
every span both rules apply.

A span may name a destination with `"toward"`: Sol, Colonia, Sagittarius A* or Beagle Point, or any system as an object `{ "system": "...", "address": 123, "position": [x, y, z] }` (`address` optional; the position comes from the entry, so no network call places it). `"toward"` may also be an ordered list of names and objects: the span travels to each in turn, and the next becomes the destination once the Commander is within 1,000 light years of the current one; the span plays on as one with no destination after the last. The stop reached is kept with the story across a restart. A position has three numbers and a system name is not empty. From the beacon scan on, each chapter in such a span travels toward it by the rule for a chapter heading home: at the span reach R, with no sights, its last stop at least three quarters of R nearer the destination (2,250 light years a chapter for a fleet whose best jump range is 30 light years, so Colonia is about 10 chapters from the bubble, or 3,750 at a fleet carrier's 5,000, about 6) or within 1,000 light years of it, and the places listed on the way. A chapter that starts within 1,000 light years of the destination is told it has arrived, and the rest of the span plays as a span with no destination. A span with a destination may not reach the finale or, in a story that flies to a real beacon, start before `breakIntoTwo`. A span with a destination that covers the last stage before the finale holds the finale until the Commander arrives at the last destination in its list: with every clue given and the finale's day passed, chapters are still written in that stage and pulled toward the destination until the Commander is within 1,000 light years of it. With no known position the finale starts as usual.

**Spans the draw opens.** A span in the black can also be opened by the activity draw (`DrawnSpans`). A chapter may open one only when it is a new chapter, not a rewrite or a replacement; the beacon has been scanned; it is not a finale chapter; no span of the hidden entry covers its stage; no drawn span is open; no home target and no heading you chose are set; your Exploration weight is above 0; your position is known; and `SpanReach.Of` gives a reach R, 100 times the best jump range in your fleet with a 5,000 light year floor when you own a fleet carrier. The chapter's activities are drawn twice with the same seed, the second time with one more place objective kept for the stop the span travels to. When the second draw holds an Exploration or Exobiology row, a roll seeded by `{storyId}|{chapter}|black` is made against a chance of min(0.5, 0.25 × Exploration weight / 50): 0 at a weight of 0, 0.25 at 50 and 0.5 at 100, and a quarter of that when the chapter before was the last of a drawn span. Under the chance the span opens and the second draw is used; otherwise the first.

The span's shape is seeded with the roll: K chapters out and S chapters at the target.

| Story length | K | S |
| --- | --- | --- |
| 3 days, 1 week, 2 weeks | 1 | 0 |
| 1 month, 3 months | 1 (70%), 2 (30%) | 0 (50%), 1 (35%), 2 (15%) |
| 6 months, 1 year | 1 (50%), 2 (35%), 3 (15%) | 0 (50%), 1 (35%), 2 (15%) |

The target is a point, not a catalogued place: K × 0.75 × R light years from where you are, at your height, heading away from the nearer of Sol and Colonia and turned by a seeded angle from -60° to +60° in the galactic plane. Within 1 light year of either in the plane, the heading is the angle alone, measured from the +Z axis. You have arrived within 0.25 × R of it. The target is not a galaxy search, but the span opens only when the galaxy search lists a station or landable body around the first chapter's waypoint toward it (below); when the search is off or cannot be made, the span opens without that check.

The span has three phases, settled from your position when each new chapter is written. Out becomes There once you are within 0.25 × R of the target, or Back when S is 0. There counts S down one per chapter and becomes Back at 0. Back closes the span once you are within 1,000 light years of where it opened. Out or There becomes Back when the last two chapters were both changed, by the rule for heading home, or when your Exploration weight is 0. While a drawn span is open, every chapter is in the black as above, the finale waits until it closes, a rewrite or replacement keeps it and never rolls, and each chapter after the first draws its first activity from Exploration and Exobiology rows only. The span and the number of the chapter the last one closed at are kept with the story across a restart.

**How a drawn span's chapters travel.** In every chapter of a drawn span, the hops from where it starts through each place objective add up to no more than R. A chapter Out or Back also takes R as the longest one hop may be. A chapter Out travels toward the target by the rule for a chapter heading home: its last stop at least three quarters of R nearer the target, or within 0.25 × R of it. A chapter Back travels by the same rule toward where the span opened, arriving within 1,000 light years of it. A chapter There travels no leg and plans at the session reach from where you are. A chapter Out or Back is offered the stations and landable bodies around the waypoint, 85% of R along the line to where it is going, and the notable places nearest the waypoint, which are favoured and never required. When a chapter after the first Out is written and the galaxy search lists no station or landable body around its waypoint, the span turns Back. The writer is told R and, when the ship aboard is not the one that gives it, which ship does and where it is stored.

**The comfort zone.** Every third chapter after the beacon scan, d47 picks the activity your
`Statistics` show you have done least, and it is the first of the chapter's drawn activities:

| Activity | Figure |
| --- | --- |
| bounty | `Combat.Bounties_Claimed` |
| bond | `Combat.Combat_Bonds` |
| mine | `Mining.Quantity_Mined` |
| organic | `Exobiology.Organic_Data` |
| rescue | `Search_And_Rescue.SearchRescue_Count` |
| passenger mission (a `Mission_Passenger` mission) | `Passengers.Passengers_Missions_Delivered` |

A tie goes to the earlier row, counting from a different row for each comfort-zone chapter: the first
counts from bounty, the second from bond, and so on round the table. Before d47 has seen a `Statistics` event, no activity is picked. An
activity you have refused in the story is never picked.

A mission family you have never completed comes first. The activity ledger files each `MissionCompleted` under
`mission:<family>`, the family in the draw table that the mission's `Name` starts with. If any family in the table has
no such entry, passes the gates and is not refused in the story, the first slot is one of them, with that row's count
range, and the table above is not used. Ties count from a different family for each comfort-zone chapter. The ledger only
knows the journals on this PC, so a family it has not seen may have been completed before them; **Never ask this** bars it.

### Drawing a chapter's activities {#chapter-draw}

`ChapterDraw.Draw` chooses three counted activities from a fixed table, `ChapterActivities.All`. The draw is seeded by
`ChapterDraw.Seed`, an FNV-1a hash of the story id and the chapter number, plus the index of the objective being
replaced in a rewrite, so the same inputs give the same activities.

| Career | Kinds | Mission families |
| --- | --- | --- |
| Combat | bounty, bond, onfootbond, conflict | Massacre, Assassinate, OnFoot_Massacre, OnFoot_Onslaught, OnFoot_Assassination, OnFoot_Defence |
| Trading | sell | Courier, Delivery, Collect, OnFoot_Delivery, OnFoot_Collect |
| Mining | mine | Mining |
| Exploration | map, signal, codex, newcodexpicture, codexpicture, wreck, uss, srv, datasale (cartographic) | none |
| Exobiology | organic, datasale (organic) | none |
| Passengers | none | PassengerBulk, PassengerVIP, Sightseeing, LongDistanceExpedition |
| Search and rescue | rescue, salvage | Rescue, Salvage, OnFoot_Salvage, OnFoot_Reboot, OnFoot_RebootRestore |
| Crime | none | Smuggle, OnFoot_Smuggle, Piracy, OnFoot_Heist, OnFoot_ProductionHeist, OnFoot_Sabotage, and every illegal family |

Never drawn: Altruism, AltruismCredits, MassacreThargoid, every set-aside family, and the onfoot, collect, suitmod
and faction kinds. An activity the story refuses is excluded.

An activity is excluded unless the ship aboard and the Commander pass its gate:

| Gate | Applies to |
| --- | --- |
| A mining weapon (`hpt_mininglaser`, `hpt_mining_`, `hpt_miningtoolv2`) and `int_refinery` | mine, Mining missions |
| `int_detailedsurfacescanner` | map |
| `int_buggybay` | srv |
| `int_passengercabin` or `int_mkii_passengercabin` | the Passengers families |
| Cargo capacity above zero | sell, salvage, Delivery, Collect, Mining, Smuggle, Piracy |
| Odyssey | every OnFoot family, onfootbond, organic, and the Exobiology data sale |
| Combat rank 3 and three engineered modules | bond, conflict, Massacre, Assassinate |
| Mercenary rank 2 | the OnFoot combat families |
| A station within reach | bounty, bond, conflict, sell, both data sales, rescue, every Mission family |
| A station with Frontline Solutions in a system at war, within reach | onfootbond |

The station gate is checked once per chapter, before the draw. `StoryDirector` asks the galaxy search for fixed stations
within the chapter's reach of the Commander's system (300 light years or more, or the span reach in a span) and counts one that
needs no permit and has a pad for the ship aboard. With none, the rows in the last gate row are never drawn, and a comfort
chapter's first slot can only be mine or organic. When the position is unknown or the query
fails, the draw is not gated. A redrawn slot after **Never ask this** follows the same gate.

A dock objective's station must have a pad for the ship the Commander will be flying at that point in the chapter: the ship named by the latest earlier board objective in the same draft, otherwise the ship aboard when the chapter is written. The list of candidate stations is filtered on the ship aboard.

On each adventures tick, when the current dock objective of the running chapter or of an active short story names a station whose largest pad is smaller than the pad the ship aboard needs, d47 says so once per objective step and ship: the station, its largest pad, the ship, and what to do (say "never ask this" for a chapter, or use Never ask this on the short story's page). The objective is not rewritten. A dock objective saved before this check has no recorded pad and is never warned about.

The Frontline Solutions gate is checked beside it. `StoryDirector` asks the galaxy search for fixed stations within the same
reach that list Frontline Solutions, and counts one that needs no permit, has a pad for the ship aboard and is in a system the
galaxy search or the Commander's own reading puts at war or civil war. It is not skipped when the
position is unknown or the query fails: with no station checked, onfootbond is never drawn. A drawn onfootbond slot is
planned with a dock objective just before it, at a station with Frontline Solutions in a system at war.

The front objective is drawn only while a campaign front is chosen. `StoryDirector` asks the campaign for the chosen front and the
galaxy search for its distance from the Commander's system; the front counts when that distance is within the chapter's reach.
With none in reach, when the distance cannot be told, or when d47 has not worked in the front's system and so holds no address for it, the Work for a faction row is never drawn. A drawn front objective is
"Work for a faction" for the campaign faction in the front's system, one to three influence marks, and the chapter's brief tells the
writer it is the Commander's own cause. A conflict front gets the same objective; the war row is not tied to the front.

A row weighs its career's weight, and a quarter of that when the last three chapters asked for it. `CareerWeights.From`
reads the ratings of 0 to 100; while every rating but Crime's is still 50, a career with activity in `Statistics`
weighs 75, and Crime weighs its rating. A career rated 0 is never drawn. In a comfort-zone chapter the first slot is a mission family never completed, else the least-done activity that passes
the gates. The rest are drawn without replacement, with no two slots in one career while another career has a row
left. Conflict and every illegal family need a place objective just before them; at most two place objectives may
be in a chapter, counting those the chapter already has. Tons for sell and mine are 10 to 40, capped at the cargo capacity.

### A chapter planned in code {#drawn-chapters}

A story chapter from chapter two on is drawn three activities, and d47 builds its objectives from them; a chapter that ends at the beacon is drawn none. `StoryDirector`
calls `ChapterDraw.Draw` with the Commander's ratings, ranks, ship and `Statistics` from the last loaded Commander's game
state, the activities the last three chapters asked for, and the activities the story refuses. A last finale chapter
already has a land objective, and a finale chapter after the first already has a stop nearer the destination, so each
leaves room for one fewer place objective in the draw. Then:

- `AdventureGenerator.ChapterPlacesAsync` gathers the places the chapter's activities need, and `ChapterPlanner.Plan`
  builds every objective: one for each activity with its drawn count, the place before it where the activity needs one,
  the sight, the Goals step, the last stop the Sol band, the home leg, the finale or the base holds the chapter to, and up
  to two story stops, three to eight objectives in all. In act one, a chapter whose beacon is out of reach ends on the reach objective. Each objective's function (setup, complication, turn, setback,
  resolution) is set by code; the turn is named midpoint or all is lost at those stages.
- One model call writes the words against that plan: the chapter's title and spine, the opening, the reply, and for each
  objective its title, reason, one to three lines, the task of an event step, and any boardings and leavings. It is told
  each objective in plain words, the stage and its lines, both layers, the genre, the cast, how the previous chapter's spine
  said it ends, and any mission held whose destination is a stop. Its answer has no field for a kind, a place or a count.
- Code checks the words first: each objective has a title, a line and a reason, an event step's task holds its
  phrase, and every line has one of the story's speakers. Then the fidelity and plain-wording checks read them. A repair writes the lines again for the
  objectives named, against the same plan, and a draft that still fails is written again with the same plan.
- Where no plan stands, the write fails with the rule that stopped it and is not written again.

Chapter one, and an act-one chapter that ends at the beacon, are planned in code with no drawn activity: one to four places
the Commander arrives at, docks at, lands on or scans, a Guardian site within reach among them, and the beacon scan last. When only a
stored ship can make the trip, boarding it comes first. Chapter one when the beacon is out of reach is planned the same way and ends on
the reach objective. A chapter written again with **Write chapter again** is written by the model, which writes its objectives. When you refuse an objective that
held a drawn activity, only that activity is drawn again, without the refused one or the chapter's other activities, and
the seed includes the index of the replaced objective. The chapter's activities are read back from its objectives, so
nothing extra is stored.

### A short story planned in code {#short-stories-planned}

A short story is planned by the same planner and draw as a story chapter. The model writes only its words.

- **The brief.** When you gave one, one short model call reads it into three things from closed lists: the system the
  story ends in (home, your carrier's system, Sol, Colonia or a system you name, found by the galaxy search), the
  activities you ask for, each with the count you said, and a ship you ask to fly. With no brief there is no such call.
- **The activities.** The ones the brief asks for come first; one with no count said takes one from the activity's usual
  range for your ship. `ChapterDraw.Draw` draws the rest from your **Career ratings**, or from your statistics where
  they are unset, up to three activities in all, never one you barred, never a crime with Crime at 0, and only what the
  galaxy search finds within a session's reach of you.
- **The count.** Code draws the number of objectives within the length's range. Story stops at places within reach fill it
  after the activities, and the labels come from code: setup, complication, turn, setback and resolution, and for *Long*
  the opening, the hesitation and the closing as well.
- **The ship.** A ship the brief names is boarded first, unless it is the ship you are in, **This ship only** is chosen,
  or you cannot afford it with the credit reserve a Goals story keeps.
- **The place.** A system the brief names is the last objective, an arrival there however far it is.
- **The words.** One model call writes the title, spine, opening, reply and each objective's title, reason and lines,
  given your brief for its theme and mood. Its answer has no field for a place, a kind or a count. The code checks and
  the fidelity and plain-wording checks read them as for a chapter. A draft written again keeps the same objectives.

The Sol band, the home leg and the base rule do not hold a short story. Where no plan stands, D47 says
"The short story could not be planned:" and the rule that stopped it, and writes no other draft.

**Change something** on a draft reads your brief and what you have said about the draft with the same call, the
where they differ, the latest thing you said counts. A remark that asks for a place, an activity or a ship, or to drop an activity, plans the
objectives again from all of it, never drawing an activity you asked to drop. Any other remark, such as "make it
darker", keeps every objective exactly as it is and writes the words again in the light of what you said.

### A Goals story {#goals-stories}

A stock story whose card sets `"goals": true` is a Goals story. Each of its chapters after the beacon scan and before the
finale works two of your Goals arcs: the unfinished ones with the lowest share of their whole, so the arcs rise together.
Powerplay runs to rank 100 and a career to Elite V, so at Powerplay rank 20 and Combat rank 6 the Powerplay arc is
further behind and is worked first. An arc whose progress d47 cannot read counts as not started, and a tie goes to the
arc first on the Goals page. While d47 knows you are not pledged to a Power, the pledge comes before every other arc.
`GoalClimb.Steps` makes the choice and gives the chapter one step for each of the first two arcs that can give one, or
for one arc when only one can. The arc furthest behind takes two of the chapter's three drawn activities and the
second takes one; a lone arc takes all three. When only one activity can be drawn for the first arc, the second arc
takes its other place. Each step is one of these:

- A career or navy within a quarter of its next rank: an objective for that rank.
- A career or navy further from it: the drawn activities are only ones that earn that rank. Combat: bounties, bonds,
  conflicts, and massacre and assassination missions. Trade: selling cargo and trade missions. Exploration:
  cartographic data sales, discoveries, mapping and first footfalls. Exobiology: organic samples and organic data
  sales. Mercenary: kill bonds earned on foot, and on-foot massacre, onslaught, assassination and defence missions.
  A navy: missions, and the writer is told they are for that superpower's factions.
- Powerplay: a pledge objective while you are unpledged, otherwise an objective for the next Powerplay rank.
- Engineers: an objective to reach the next stage with the first engineer on the unlock route. An engineer out at Colonia (more than 5,000 light years from Sol) more than 1,000 light years from the Commander is never carried plainly. In a story of three months or longer the chapter opens a span of kind Engineers toward their system, whatever the Exploration weight, arriving within 1,000 light years; the span stays There while the next engineer on the route is out at Colonia, each chapter there carrying this step, then turns Back as any span does. In a shorter story the step is not offered for such an engineer.
- Ships: an objective to board the cheapest hull you do not own.

An arc is passed over for the next one when it has no step to give now: a pledge while d47 has not read whether you
are pledged, a hull your credits do not cover, no engineer on the route, or a career none of whose activities can be
drawn, given your ranks, your ship and the activities the story has refused. An objective step takes the place of one drawn activity, and a chapter without that
objective is refused like a chapter missing a drawn activity. With two steps the writer is told both goals and asked
for one story line that gives you a reason for both. A rewritten chapter carries no Goals step.

A Goals story's clues follow your Goals progress as well as their days. Overall progress is the mean share done of
every built-in Goals arc, with an arc d47 cannot read counted as 0. Powerplay counts while you are unpledged, and so
does an arc you removed from the Goals page. Each clue before the finale waits for
its day and also for overall progress to reach an even step on the way from where it stood when you picked the story
to 100%. With fourteen clues and 40% at the pick, the first clue waits for 44% and the fifth for 60%. Its finale waits
for its day and for every built-in Goal to be done; once the finale has begun, it is not withdrawn. A drop in progress,
such as the ship collection reading 0 before the fleet has been listed this session, holds the next clue and takes
back none already given. The story's entry on the Goals page says what it is waiting for: "Clue 5 waits for Goals
progress of 60%; you are at 37%", or "The finale waits for every Goal: 9 of 11 done". The Story Bench cannot jump a
Goals story.

A Goals story's card may also list `goalArcs`, the keys of the arcs it works, such as
`["rank.combat", "rank.trade", "rank.explore", "rank.soldier", "rank.exobiologist"]` for Elite V in the five careers.
Its chapters' steps, its overall progress, its finale and its Goals page entry then read only those arcs, and the
pledge step is offered only when `powerplay` is listed. A card without the list works every arc.

### What you like doing {#career-ratings}

The Stories settings hold eight ratings, one per career, each from 0 to 100 and 50 until you set it. They are kept per
Commander, so two Commanders on one install keep separate values, and a Commander with none set reads 50 for each. They
are the ratings `CareerWeights.From` reads when a chapter's activities are drawn. A value outside 0 to 100 is refused on the panel and by voice. A rating of 0 means that career is never asked for. Say "set my
mining rating to 80" or "rate exploration 30", with the number in digits or words. The Commander sets a rating: it is
not offered to the model, which is refused if it asks.

#### Combat {#rating-combat}

How much you like combat.

#### Trading {#rating-trading}

How much you like trading.

#### Mining {#rating-mining}

How much you like mining.

#### Exploration {#rating-exploration}

How much you like exploration.

#### Exobiology {#rating-exobiology}

How much you like exobiology.

#### Passengers {#rating-passengers}

How much you like passengers.

#### Search and rescue {#rating-search-and-rescue}

How much you like search and rescue.

#### Crime {#rating-crime}

How much you like crime. At 0 no chapter, detour or short story asks you to commit a crime: an illegal mission, piracy, smuggling, or an event step on stolen cargo, items or goods. Above 0 the rating sets how often crime is drawn.

### Refusing an objective

A story chapter's current objective has a **Never ask this** button, on the chapter's page and on the Stories
page, and a voice command: "never ask this", "this objective is not for me", "not for me" or "give me a different objective". The
button asks "Write a different objective?" and, for an objective that asks for an activity, "This story won't ask
you to collect bounties again." (or whichever activity it is). On yes d47 builds new objectives from the current
one to the end of the chapter: as many as it replaces where the story's activities allow, with the same roles in
the chapter, never at the refused objective's place and never for the refused activity. The core writes only their
titles and lines. Objectives already done stay as they were, the chapter count and the
clues do not change, and the new objectives count only what happens after you refused. A replacement arrive
objective for a system you had already visited waits for your next arrival there. If the write fails, the objective
stays as it was and the Stories page says why.

The activity is remembered for the rest of the story. A refusal is kept under the objective's kind, and for a
mission under the kind and the mission family, so refusing a `Mission_Massacre` objective still allows a
`Mission_Courier` one. A refused activity is not drawn again for the rest of the story. The Stories page lists them. An objective that names a place
(arrive, dock, land, scan, board, rank) is replaced and not remembered. The Guardian beacon scan that
ends act one cannot be refused. A refusal cannot be taken back.

The model is refused this tool.

#### `refuse_story_beat`

Refuse the objective the Commander's story chapter is waiting on and write a different one in its place. The story remembers the activity and does not ask for it again. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Changing an objective

Beside **Never ask this**, a story chapter's current objective has a **Change objective** button, on the chapter's
page and on the Stories page, in the window and on the VR big panel. Say "change this objective" or "change the
objective" to open the same page. The page asks "What would you rather do?" and takes your words by speech or
keyboard. One short model call turns them into a detour: one objective of your own, to arrive or dock at a named
place, or one activity with its count. When your words give no count, d47 takes one from the activity's usual range
for your ship. The status line reads "Writing a different objective…" meanwhile, and the
answer comes back in seconds. D47 says its reply in its own voice, not as a story line, and shows it with three
buttons:

- **Use this** sets the detour.
- **Keep the current one** drops it.
- **Say something else** opens the page again, and what was said so far is kept.

The detour's place is checked with Galaxy search, as a chapter's places are. When your words name no place and no
activity ("something closer to home"), or name a place Galaxy search does not know, D47 asks one short question
instead, with up to three systems as buttons: places it knows, such as home, your fleet carrier, Sol or Colonia, or
the nearest systems of a minor faction or Power you named. A button sets that detour. After two questions without a
usable answer, the reply is "Name a place or an activity." A detour never asks for an activity the story refused or
that you barred.

While the detour is open, the story waits on it and writes no chapter. The chapter it interrupted is set aside, and
once the detour is done that chapter is written again, at the same number, from where you are then. A detour does not
count as a chapter. An open detour shows **Change objective**, which replaces the detour, and **Mark done**; it does
not show **Never ask this**.

**Write chapter again** sits beside **Change objective** and **Mark done** on a story chapter's page and on the Stories
page, in the window and on the VR big panel, wherever **Change objective** shows on a story chapter, an open detour
included. A short story does not get it. One press, or "write this chapter again", sets the chapter aside and writes it
again from where you are; there is no question first, and the status line reads "Writing the chapter again…". It works
on any step, the last included. Objectives already done stay done. A finale chapter keeps its destination, and a beacon
chapter stays a beacon chapter aimed at the nearest beacon; it is refused when no ship you own can reach a beacon. It drops an
open detour. It does not count toward the activity bar. The model is refused this tool.

#### `rewrite_story_chapter`

Set the story's current chapter aside and write it again from where the Commander is now; objectives already done stay done. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Unlike **Never ask this**, changing an objective does not bar the activity for the story. d47 counts, for each Commander, the detours applied in place of an objective that asks for an activity (arrive, dock, land and scan objectives count nothing; **Keep the current one** counts nothing). At five changes for one activity, it is barred for good: no later story chapter or short story asks for it, as **Never ask this** bars it for one story. A mission objective counts under its mission family. The counts are kept in `activity-bars.json` in the data folder. Nothing lifts a bar yet.
A proposal is refused when the story has moved on while it was written, and the objective stays as it was. Both
buttons are left out while an objective is being written, and for an objective that cannot be refused, such as the
Guardian beacon scan. The model is refused this tool.

#### `change_story_objective`

Open the entry where the Commander says what they would rather do instead of the story chapter's current objective; the core then proposes a replacement for them to use, keep or reword. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Marking an objective done

Only the journal completes an objective on its own. When you did the objective and the journal missed it, press **Mark
done** under NEXT, beside the ship button, on a story chapter's page or a begun, unfinished short story's page, in the
window and on the VR big panel. It asks "Mark "The Anchorage" done? The story moves on as if the journal had shown it." (with
the objective's title), and on **Mark it done** the objective fires: its lines are told, the story waits on the next one, and
a chapter's last objective moves the story on to the next chapter. The button is left out while an objective is being
written.

Say "mark this objective done", "mark the objective done" or "this objective is done" to do the same without the question.
The phrase marks the running story's chapter. While your story is paused it marks nothing and asks you to resume it
or use the button. With no story under way it marks the one short story under way; with more than one, d47 names them
and asks you to press **Mark done** on the page of the one you mean. A chapter is not marked while the game runs without
Odyssey.

The Guardian beacon objective can be marked done: d47 records the beacon scan on the story, so its Guardian cores come aboard on
the next tick as they would from a real scan. A beacon objective with no beacon system is refused with "The beacon beat has no
beacon system to scan." An objective to fit out a ship for the trip can be marked. A marked
objective stays fired after a restart, and the logbook names it as marked done by the Commander. Marking does not bar
the activity or count toward a bar. The model is refused this tool.

#### `mark_objective_done`

Mark the objective the Commander's story is waiting on as done, as if the journal had shown it: the running story's chapter, or with no story running the one short story under way. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Setting course for the objective

Say "set course for my next objective" to plot to the system the objective names, as "set course for" a system does.
"Set course to" works as well, and the objective can be "objective", "my objective",
"current objective" or "next objective", with or without "the". Say "copy my objective to clipboard" (any of the same
names for the objective, and "to the clipboard" or "to my clipboard") to put the system name on the clipboard. Both take the same objective as "mark this objective done": the running story's chapter, or with no story
running the one short story under way. They refuse when no story is under way, when your story is paused (resume it
first), when more than one short story is under way (d47 names them), and when the objective has no system, such as a
rank, bounties or boarding a ship. Nothing is plotted or copied on a refusal. The model is refused these tools.

#### `set_course_for_objective`

Set course for the system of the objective the Commander's story is waiting on, by plotting it in the galaxy map: the running story's chapter, or with no story running the one short story under way. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `copy_objective_system`

Put the system of the objective the Commander's story is waiting on on the clipboard: the running story's chapter, or with no story running the one short story under way. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Choices

An objective can begin with a choice of two or three options. The ship or a character offers it in the line
before, never the Narrator, and d47 then says the phrases as written: "Say choose option one to fight, or choose
option two to flee." Say "choose option one", "choose option two" or "choose option three". The phrases work only
while a choice is waiting and its offer has been said; at any other time they do nothing. A line that offers a
choice and comes due in combat waits until the ship is out of danger and is then said in full. If the offer was
never said, because d47 was closed or the story was off when it came due, it is said again.

Then do what the option asks. Before you choose, completing or abandoning a mission an option refers to fails the
chapter. After you choose, doing what only another option asks for, such as abandoning the mission you chose to
complete, fails it too. An option either rejoins the chapter or ends it in a named ending of its own, which is
said when its steps are done; the next chapter is written from the ending you reached.

The choice is stored in `data/adventures.json` with its time, so a restart applies it again in order with your
journal. While the story is on an objective that begins with a choice, Change objective, Write chapter again and Never ask this are
refused, and Mark done works only once you have chosen. The model is refused this tool. The ship does not yet write
choices into the stories it writes; a choice runs only in a story file that holds one.

```csharp
public static AdventureStanding Chose(AdventureStanding standing, int option, DateTimeOffset at)
```

#### `choose_story_option`

Choose one of the options a story chapter offers, by the number the offer gave it, from one. The Commander must then do what the option asks. The Commander's choice alone.

```json
{"type":"object","properties":{"option":{"type":"integer","description":"The option\u0027s number in the offer, from one."}},"required":["option"],"additionalProperties":false}
```

### Pausing the story

Clear **Story on**, on the Stories page or on the chapter's page, or say "pause the story", and the
running stock story goes quiet until you switch it on again. While it is off no objective is said, no
nudge or clue is owed, nothing from the story is posted, missions get no story aside, and the
hidden layer is left out of every prompt, so the Narrator, the core aboard and chatter do not hint at it. The clue clock stops.
Narration and chatter work as they always do, and your Backstory goes back to your default, with your cores and persona unchanged. Switching the story on again brings back its own Backstory.

A place you visit while the story is off does not count. Its objective waits for the next visit. The
switch is kept in `data/story.json`, so it lasts across restarts. Say "resume the story" or tick
**Story on** to bring it back.

Both phrases are the model-free router's, and the model is refused them.

#### `switch_story`

Switch the Commander's story to a shelved one by title, as Switch to does on the Story bench: the running story is shelved and the named one comes back paused. The Commander's choice alone.

```json
{"type":"object","properties":{"title":{"type":"string","description":"The story\u0027s title."}},"required":["title"],"additionalProperties":false}
```

#### `play_story`

Open a story run's page and play its kept lines from the first, as Play all does. The Commander's choice alone.

```json
{"type":"object","properties":{"title":{"type":"string","description":"The story\u0027s title."}},"required":["title"],"additionalProperties":false}
```

#### `story_playback`

Play, pause or skip to the next line of the story playback that is open, as the playback box's tiles do. The Commander's choice alone.

```json
{"type":"object","properties":{"action":{"type":"string","description":"What to do to the playback.","enum":["play","pause","next"]}},"required":["action"],"additionalProperties":false}
```

While a story playback is open, playing or paused, four words drive it: "play" or "resume" carries on, "pause" holds it, and
"next", "next line" or "skip" moves to the next line. The words are live only while the playback is open; outside it, "pause"
and "play" act on the music and "next" matches nothing. "Pause" is the Commander's own pause even when the playback is
already paused for push-to-talk. "Next" plays the next line, stays paused at it when you paused the playback, and on the last
line ends the playback. The replies are "Playing.", "Paused." and "Next line.". "Stop", "quiet", "enough", "shut up" and
"be quiet" end the playback and close the box. The model is refused.

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

### The story so far

Say "where am I up to", "recap the story" or "what's happened in the story so far", or press **Story so far**, and the
Narrator says where the story stands in two to four sentences. The button is on your stock story's page on the Stories
tab, under "This is your story.", and on the page of a short story under way, in the window and on the VR big panel.
The phrases recap your stock story while it is running or paused, and otherwise the short story whose objective fired
last. The button on a short story's page recaps that short story. With no story under way, d47 says "No story is under way."

The recap is made only from what has happened. For a stock story that is your backstory, the objectives reached in
every chapter and the clues given so far; for a short story, its premise and the objectives reached. It ends on where
the story waits, as the place and the act, the way a hand-off names them. It never names an objective still to come,
never says the current objective's line, and says nothing of the hidden layer beyond the clues you have had.

It is one model call, counted in spend like a narration. Without a model, or when the model writes the recap in the
first person, the Narrator says the last objective's line as written and then where the story waits. The recap is not
kept with the story's lines, and nothing says it unless you ask. The model is refused this tool.

#### `recap_story`

Have the Narrator recap the story under way: what has happened so far and where it waits now. The Commander's choice alone.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Ending a story

When a stock story's last finale chapter is done, the story is finished and d47 posts its ending under
Waiting for your answer on Your story, said by the Narrator while stock COVAS is aboard or narration is on and
otherwise by the core aboard, with the story's options under it. Press an option there, or say "choose ending two". A story with one option takes "accept the ending".
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
later stages. A second or later act-one chapter is not given the act-one lines an earlier chapter already had: it gets the lines
after Catalyst (Debate, and Break into Two once the beacon is in reach), or the last act-one line when none follow.
The writer of any next chapter is also told how the previous chapter's spine said it ends. The full sheet has all fifteen objectives. The short sheet has only the objectives of the stages its
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
does not change who speaks a clue. A clue is also kept on the story's page on the Story bench, from whoever spoke it. A clue needs
a language model and personality; without them it waits.

The Narrator, hired crew, scene chatter and the chapter writer all read the same hidden layer:
the story's secret, its end, and the clues you have had so far. A Guardian core or a core you wrote
reads it too, in conversation and in its own lines. Stock COVAS never reads it: it is standard
equipment with no history, so it has nothing to hint at. Strangers on comms, the pilot you have
targeted, invented strangers in overheard chatter and custom characters do not read it. They are told to
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
system an arrive objective or its station a dock objective, so the mission's trip is also the story's. In a chapter
built on drawn activities, d47 may make one mission's destination a stop, and the writer is told that mission. No objective asks
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
per line, with when each of its objectives fired; when a story is abandoned or finished, all of
its chapters move there. A shelved story keeps its last two chapters in `data/adventures.json`, so Resume can
write the one under way again. Archived chapters leave the list on the Stories tab, the chapter writer still reads
the last ten of them by name and premise, and your log still lists their objectives. Story chapters do not
count toward the 40 stories of your own that the file holds.

### What it does not do yet

- **Branching**, beyond a choice the Commander makes. One current objective, and only it can match; an objective
  that would match out of order is ignored rather than banked.
- **Importing** somebody else's story. The store file is already the format, so this is a copy
  and a validate when it comes.
