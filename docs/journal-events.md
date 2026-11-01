---
title: Journal events
group: General help
nav_order: 14
---

What each of 45 journal events is, and what d47 does with it.

<!--
  The How it works band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Every entry below is a journal event Elite writes, and what d47 does with it.</p>
<section>
<h2><span class="num">1</span> Elite writes the line. D47 reads it.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="Elite writes a journal line and d47 reads it">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">A JOURNAL LINE</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">one JSON event</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">D47 READS IT</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">never writes to the file</text>
</svg>
<p class="body">Each event is one JSON line in Elite's journal. d47 reads the file as it grows and never writes to it.</p>
</section>
<section>
<h2><span class="num">2</span> Every event gets a sentence on the Journal page.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="One event, one sentence on the Journal page">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">FSDJump</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">the event name</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">A SENTENCE</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">on the Journal page</text>
</svg>
<p class="body">Docking, jumping and scanning each read as a sentence on the Journal File reading, with the fields behind it.</p>
</section>
<section>
<h2><span class="num">3</span> Some events also change what d47 knows.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="Some events also change what d47 knows">
 <rect x="20" y="22" width="248" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="144" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">FSDJump</text>
 <text x="144" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">you arrived</text>
 <line x1="276" y1="70" x2="292" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="306,70 290,62 290,78" fill="var(--accent-muted)"/>
 <rect x="316" y="22" width="248" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">CURRENT SYSTEM</text>
 <text x="440" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">what d47 knows</text>
 <line x1="572" y1="70" x2="588" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="602,70 586,62 586,78" fill="var(--accent-muted)"/>
 <rect x="612" y="22" width="248" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="736" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">CALLOUTS</text>
 <text x="736" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">what follows</text>
</svg>
<p class="body">An FSDJump sets the system d47 believes you are in, and the callouts that follow an arrival run from it. A few events, such as <code>case "FSDTarget" when _jumpingTo is { } flyingTo:</code> in <code>DangerCallout</code>, are read for one narrower purpose.</p>
</section>
</div></div>

## BackpackChange

Elite writes BackpackChange when items are added to or removed from your on-foot backpack. d47 shows it on the Journal page and does nothing else with it.

## Cargo

Elite writes Cargo at the start of a session and when the contents of your ship's hold change, listing what is aboard. d47 shows it on the Journal page and does nothing else with it.

## CarrierLocation

Elite writes CarrierLocation to say which system your fleet carrier is in. d47 reads it to know where your carrier is.

## ColonisationConstructionDepot

Elite writes ColonisationConstructionDepot when you dock at a colonisation construction site, giving how far the build has progressed and what it still needs. d47 reads it to keep the list of construction sites and what each still needs.

## Commander

Elite writes Commander at the start of a session, with your Commander name and Frontier ID. d47 reads it to know which Commander is playing, so that each Commander's progress is kept apart.

## CommunityGoal

Elite writes CommunityGoal when you open the community goals list, with each active goal and how it stands. d47 reads it to keep the board of community goals you have seen.

## CollectItems

Elite writes CollectItems when you pick up items on foot. d47 reads it to progress story steps that ask for an item to be collected.

## DatalinkScan

Elite writes DatalinkScan when you scan a data link, with the message it holds. d47 shows it on the Journal page and does nothing else with it.

## DockingGranted

Elite writes DockingGranted when a station or carrier grants your docking request, with the landing pad. d47 reads it to learn your carrier's callsign when the request is to your own carrier.

## DockingRequested

Elite writes DockingRequested when you ask a station or carrier for permission to dock. d47 counts it, and reads it to learn your carrier's callsign when the request is to your own carrier.

## Docked

Elite writes Docked when your ship finishes landing at a station, outpost or carrier. d47 reads it to know where you are, and it is how your carrier's callsign is learned, at the airlock. Docking at a station where a mission can be handed in prompts a reminder of it.

## EngineerCraft

Elite writes EngineerCraft when an engineer applies a modification to one of your modules. d47 reads it to update your ship's modules and the materials you hold.

## EngineerProgress

Elite writes EngineerProgress at the start of a session and when your standing with an engineer changes. d47 reads it to know which engineers you have unlocked and how far you have taken each.

## FSDJump

Elite writes FSDJump when your ship arrives in a new system by hyperspace. d47 reads it to know which system you are in, and for the callouts that follow an arrival.

## FSDTarget

Elite writes FSDTarget when you select the next system to jump to. d47 shows it on the Journal page, and reads it during a jump to notice when the route did not advance because the ship was pulled out.

## FSSAllBodiesFound

Elite writes FSSAllBodiesFound when every body in a system has been found with the Full Spectrum System Scanner. d47 reads it to remember that the system is fully scanned. With Share with EDDN on, it also sends the result to EDDN.

## FSSDiscoveryScan

Elite writes FSSDiscoveryScan when the discovery scan of a system completes, with how many bodies it found. d47 reads it to tell that the system has been scanned.

## FuelScoop

Elite writes FuelScoop while you scoop fuel from a star, with the amount scooped and the total in your tank. d47 reads it to keep your fuel level current.

## LaunchDrone

Elite writes LaunchDrone when you launch a limpet. d47 counts the prospector and collector limpets launched in your mining run.

## Loadout

Elite writes Loadout at the start of a session and whenever your ship's fit changes, listing every module. d47 reads it to know what your ship carries.

## Location

Elite writes Location at the start of a session and when you respawn, giving where you are. d47 reads it to know your system and station.

## Market

Elite writes Market when you open a station's commodity market. d47 reads it as a sign that you are trading.

## MaterialCollected

Elite writes MaterialCollected when you pick up a material, with its name, kind and how many. d47 reads it to keep your materials count current, and to announce when a material reaches a milestone.

## Missions

Elite writes Missions at the start of a session, listing the missions you have active. d47 reads it to rebuild its mission board.

## MiningRefined

Elite writes MiningRefined when your refinery turns mined material into a commodity. d47 reads it to progress story steps that ask for mining.

## NavRoute

Elite writes NavRoute when you plot a route, but the event carries no fields: the route itself is in NavRoute.json. d47 reads the file for the route, and treats the event as a sign that you are planning a trip.

## NavRouteClear

Elite writes NavRouteClear when your plotted route is cleared, but the event carries no fields: the route is in NavRoute.json. d47 shows it on the Journal page and does nothing else with it.

## PowerplayMerits

Elite writes PowerplayMerits when you earn merits for your pledged power, with the merits gained and your total. d47 reads it to keep your pledge's rank current.

## Progress

Elite writes Progress at the start of a session, giving how far you are through your current rank in each career. d47 reads it to say how far into a rank you are.

## ProspectedAsteroid

Elite writes ProspectedAsteroid when a prospector limpet reads an asteroid, with its content and any materials in it. d47 reads it to announce what the asteroid holds.

## Rank

Elite writes Rank at the start of a session, giving your rank in each career. d47 reads it to know your ranks.

## ReceiveText

Elite writes ReceiveText when a message reaches you from a player, a ship or a station. d47 reads it to speak incoming messages and to learn your carrier's name. It treats a player's message as data and never sends it to a model.

## RefuelAll

Elite writes RefuelAll when you refuel your ship at a station, with the cost and the amount. d47 adds the cost to your spend ledger.

## Reputation

Elite writes Reputation at the start of a session, giving your standing with the major powers. d47 reads it to report how you stand with each power.

## Scan

Elite writes Scan when you scan a star, planet or other body. d47 reads it to keep the record of bodies you have scanned, and for the callouts about a body's features.

## ScanBaryCentre

Elite writes ScanBaryCentre when you scan the barycentre that a group of bodies orbits. d47 shows it on the Journal page. With Share with EDDN on, it also sends the scan to EDDN.

## ShieldState

Elite writes ShieldState when your ship's shields go down or come back up. d47 reads it to warn you when your shields drop.

## ShipTargeted

Elite writes ShipTargeted when you target or lose a ship, with what its scan has revealed so far. d47 reads it to remember the pilots you have targeted, and says so when you meet one again.

## StartJump

Elite writes StartJump when a jump begins, either to hyperspace or into supercruise. d47 reads it to know that a jump has started.

## StoredModules

Elite writes StoredModules when you open Outfitting, listing the modules you have in storage. d47 reads it to show your stored modules.

## SupercruiseDestinationDrop

Elite writes SupercruiseDestinationDrop when you drop out of supercruise at a destination. d47 reads it to know where you have arrived, and to learn your carrier's name when the destination is your own carrier.

## SupercruiseEntry

Elite writes SupercruiseEntry when your ship enters supercruise. d47 reads it to know that you are in supercruise.

## SupercruiseExit

Elite writes SupercruiseExit when your ship drops out of supercruise, with the body it is near. d47 reads it to know that you are back in normal space and where.

## UnderAttack

Elite writes UnderAttack when your ship is being attacked. d47 reads it to warn you of danger.

## Undocked

Elite writes Undocked when your ship leaves a station, outpost or carrier. d47 reads it to know you are no longer docked, and to remind you of missions you could have handed in there.
