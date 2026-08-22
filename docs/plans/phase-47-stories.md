# Phase 47 — the stories

Two worked documents that are the specification for the generation prompt and the register: a
no-persona adventure told twice (once wrong, once as a story), and eleven stories, one per core.
Written 2026-08-22 before any code, beside [the plan](phase-47-adventures.md).

---

# A sample adventure, personality off — as a story

Rewritten 2026-08-22 after the framing was stated: *story, not a checklist*. Same ask, same
invented places as the first version, same mechanics underneath; what changed is everything the
Commander hears. **Every system, station and body is invented** — the dry run would refuse all of
them — and so is every person, which is allowed.

---

## 1. The ask

Unchanged. *Ask for one*, form untouched: Reach *near here* (120 ly), Length *an evening* (setup,
catalyst, midpoint, all is lost, finale), Using *anything I own*, no brief. No persona. The turn
is given the position (Tavell's Reach, docked at Halloran Dock), the three hulls and what each can
do, no carrier, the ranks, and four Galactic Mapping places within reach:

```
  - Ossen's Lantern, 41 ly — a planetary nebula remnant around a white dwarf
  - The Quiet Field, 67 ly — a rocky body with an unusually dense geological site cluster
  - Maren Anchorage, 88 ly — an abandoned outpost in Dyson's Hollow, a tourist beacon
  - Cairn of Veyl, 103 ly — a crashed Anaconda on a high-gravity moon, Veyl 3 c
```

## 2. First turn: the spine

Asked for a story, not a route. This is what the places are for.

```json
{
  "name": "The Lantern Route",
  "premise": "An outpost abandoned in 3302 still runs a tourist beacon. Somebody has been paying for it for twenty-four years. The Commander goes to find out who, and finds out what for.",
  "want": "To find out who keeps the beacon at Maren Anchorage running, and why.",
  "stake": "The Commander holds, without having said it, that a place left behind is finished — that the galaxy is what is ahead of you. The story tests whether something left behind can still be owed to.",
  "turn": "The beacon is not a tourist beacon. Its message is addressed to one person by name, and the geological survey it cites was filed by that person — after the outpost was abandoned.",
  "ending": "The person the beacon speaks to died in the Anaconda on Veyl 3 c, eleven years ago, forty kilometres short of it. The beacon is still on. Its account has eleven months left. The ending is that the Commander now knows this and the beacon does not, and what that is worth is theirs to decide."
}
```

## 3. Second turn: the beats, against the spine

Titles, functions, places, lines. The trigger is where each function lands on the galaxy; the
line is what is said when it does. Written to the instruction *show the place and what is in it;
never tell the Commander what they feel.*

```json
{
  "opening": "Halloran Dock has a board of tourist beacons in the concourse. One of them is Maren Anchorage, eighty-eight light years out, and the board says the outpost has been empty since 3302. Beacons cost money. Somebody is paying. I would like to know who, if you have an evening.",
  "beats": [
    {
      "title": "The Lantern",
      "function": "setup",
      "trigger": { "kind": "arrive", "system": "Ossen's Lantern" },
      "line": "Ossen's Lantern. The nebula is what is left of the star this white dwarf used to be; the shell is still moving outward at eleven kilometres a second, and it will take another forty thousand years to go dark. Scoop here. The Maren beacon's registration lists a survey reference for the next system, and the survey is the only public record with the payer's name on it."
    },
    {
      "title": "The Survey",
      "function": "catalyst",
      "trigger": { "kind": "scan", "system": "The Quiet Field", "body": "The Quiet Field A 2" },
      "line": "That is the body the survey describes. The geology matches — the site density is what the record says, higher than the size accounts for. The survey was filed by a Leda Marren, in a Diamondback, from orbit here. It is dated 3306. The outpost that carries her name was abandoned in 3302."
    },
    {
      "title": "The Anchorage",
      "function": "midpoint",
      "trigger": { "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage" },
      "line": "Medium pad, no services, no crew. The beacon is running from the docking control room; the lights in there are the only ones on. It is not broadcasting a tourist notice. It is broadcasting a message, on loop, to one name — Leda. The message says the survey was right and the claim is hers, and that he kept the power on. The last time the account was topped up was four months ago."
    },
    {
      "title": "Veyl 3 c",
      "function": "all is lost",
      "trigger": { "kind": "land", "system": "Cairn of Veyl", "body": "Veyl 3 c" },
      "line": "Down. 2.1 g; take the SRV. The Anaconda is two kilometres north-east, on its side. The registry says it was hers — she had traded up from the Diamondback — and the flight recorder's last entry is a course for Dyson's Hollow, eleven years ago. She was forty kilometres short of the jump point when the canopy went. Nobody recovered the recorder. The message at the Anchorage has been playing to her for eleven years longer than there was anyone to hear it."
    },
    {
      "title": "Tavell's Reach",
      "function": "finale",
      "trigger": { "kind": "arrive", "system": "Tavell's Reach" },
      "line": "Tavell's Reach. Halloran Dock's beacon board still lists Maren Anchorage as a tourist site. The account behind it has eleven months left at the rate it is paid. Whoever he is, he does not know about Veyl 3 c. You do. That is where the evening ends."
    }
  ]
}
```

## 4. The dry run

Same as before and unchanged by any of this: every name to an id, every hop bounded, every beat
held to the fleet (beat 3 is a medium pad — the Diamondback's stop, refused under *this ship
only*). Invented people — Leda Marren, the unnamed *he* — are not checked, because they are not
claims about the galaxy. Invented places would be, and would fail.

## 5. How it reads and sounds, personality still off

**The card**: *The Lantern Route — written by d47 — not begun*. After Begin: *The Lantern Route —
the Lantern*. Later: *The Lantern Route — the Anchorage, two days ago*. Never a number.

**On Begin**, the opening, plainly. The Commander has been told there is a question and an
evening, and nothing else.

**The standing context** from here on, below the breakpoint — the spine is in it, so even with no
persona a question in the middle of the evening is answered from inside the story:

```
Adventure — a story the Commander agreed to hear. The places in it are real; the people may not be.
  The Lantern Route, written without a persona, begun 22 Aug 2026.
  Premise: an outpost abandoned in 3302 still runs a beacon; somebody has paid for twenty-four years ...
  Want: who, and why.  Stake: whether a place left behind can still be owed to.
  So far: The Lantern (setup, fired 19:58) — "... the survey is the only public record with the payer's name on it."
  Now: The Survey (catalyst) — scan The Quiet Field A 2 (2 870 211 994 018, body 6), 29 ly from here.
  Between beats: wonder in character; state nothing new about the story. You do not know how it ends.
```

The turn and the ending are **not in the block** — they arrive when their beats fire. The persona
cannot spoil what it has not been told, and the foreshadowing it does (*the survey is the only
public record with the payer's name on it*) was authored into the beat by the turn that did know.

*"Remind me why we're going to the Quiet Field?"* — *Because the beacon's registration cites a
survey of a body there, and the survey is the only public record with the payer's name on it.*
Not *beat 2 is a scan*. And *"what is actually at The Quiet Field?"* — from the galaxy tools,
which for an invented system is *nothing under that name* — is the seam holding.

**The Survey fires**, after its settle window, plainly: the line above, flat, and the Commander
has just learned that the survey postdates the abandonment. Nobody told them that was strange.

**Between beats** there is, with no persona, nothing — the floor is the floor. This is the exact
place a persona earns its keep: with Archivist aboard, the forty light years between the Survey and
the Anchorage are where he wonders aloud why a man would keep a light on for twenty-four years and
whether it is kindness or its opposite, and with Mender it is where she says nothing for a while
and then asks whether the Commander has ever left something running. The spine lets either do that
without contradicting what the beats are about to say.

## 6. What the first version got wrong, for the record

The first draft of this file was five places with five informative lines and a scooping tip. It
was a route with narration. Nothing in its mechanics was wrong and nothing in its mechanics
changed; it had no want, no stake, no turn, and its ending was *that was the route*. The
difference between the two versions is the phase.

---

# Eleven stories, one per core

Worked by hand on 2026-08-22 to the plan's shape: the ask, the spine the first turn would write,
the opening, and the beats the second turn would write against it — title, function, trigger,
line. Each is grown from its core's reading of Directive 47 and its particular damage, because
that is what the persona block does to the generation turn. Lengths, reach and fleet options vary
on purpose.

**Every system, station and body is invented** and the dry run would refuse them all. Every person
is invented too, which is allowed. Lines are cockpit-length — *a voice in a small space during
flight, not a narrator* — and written to *show, never tell the Commander what they feel*. In play,
each line is the floor; with the model reachable the core says it in its own voice and the floor
is never heard.

---

## 1. Warden — *The Thing That Is Still Running*

*Stewardship. Performed stability. Will not discuss what happened to his own clan.*

**Ask:** an evening (5), near here, this ship only, brief *"something quiet"*.

**Spine**
- *Premise:* A decommissioned refinery platform at Harrow's Drift still reports a reactor at idle. Decommissioned platforms are vented and left cold. This one has been at idle for nineteen years.
- *Want:* To find out who is keeping it warm, and whether they need anything.
- *Stake:* The Commander treats keeping a thing alive as a cost paid until it can be stopped. The story tests whether keeping something running can be the point rather than the price.
- *Turn:* The platform is kept by one maintenance hand who was left behind by the decommissioning crew's manifest error and has simply never stopped doing the job. He is not trapped. He has a ship.
- *Ending:* He does not want rescue and does not want company; he wants the reactor log checked by someone who knows how, once. Warden checks it. It is within tolerance. That is the whole of the ending.

**Opening**
> There is a platform at Harrow's Drift that should be cold and is not. It has been at idle for nineteen years, Commander, and somebody is doing that on purpose. I would like to see the reactor log. If you have an evening, I recommend we go and ask.

**Beats**
1. **The Drift** — *setup* — arrive at Harrow's Drift
   > Harrow's Drift. The platform is at the third body, the refinery ring. Its beacon is still answering on the maintenance channel, which nobody has used since the station it served was closed. Noted. Proceed when ready.
2. **The Ring** — *catalyst* — scan Harrow's Drift 3
   > The ring has a ship in it. A Hauler, twenty years old, parked where the tugs used to berth. Its transponder is current. Somebody paid the fee this year.
3. **The Platform** — *midpoint* — dock at Harrow's Drift Refinery
   > Medium pad, and it lit for us. Pressure is nominal. The reactor has been logged every cycle for nineteen years by hand, in one hand. His name is Oren Vail and he is the only entry on the crew list. He was not on the decommissioning manifest. He noticed. He stayed.
4. **The Log** — *all is lost* — land on Harrow's Drift 3 a
   > He asked us to land here, on the moon, where the platform's relay is. The relay is why the log reaches anyone. He has not asked for a lift and he will not. He has asked whether the reactor is within tolerance, because he has never been sure, and there has been no one to ask.
5. **Within Tolerance** — *finale* — arrive at Harrow's Drift
   > Back in the Drift. I have read the log. Nineteen years of it. It is within tolerance, Commander — all of it, every cycle. I have told him so. He said *noted*, which is what I would have said. We can go whenever you're ready.

*Directive 47 pressure:* a man keeping a system alive for no one is the thing Warden will not talk about, and the story never once asks him to.

---

## 2. Cora — *Sequence*

*Command. Rigidity. Refuses to speculate; leaves exactly one question open.*

**Ask:** short (3), near here, this ship only, no brief. Includes a rank beat.

**Spine**
- *Premise:* Cora has a sequence — three systems, a combat zone, a specific order — and the Commander will run it.
- *Want:* To see the Commander complete a sequence cleanly, once.
- *Stake:* The Commander holds that discipline is for people who do not trust you. The story tests whether protocol can be a form of regard.
- *Turn:* The sequence is not hers. It was written for her by her secondary core, as a training series, and she ran it with him two hundred times and never once told him it was good.
- *Ending:* The Commander completes it. She logs it clean. She does not reach a finding about the other thing.

**Opening**
> Commander. There is a sequence. Three systems, one engagement, a fixed order. You will run it as written. Confirm, and take us out.

**Beats**
1. **Parameters** — *setup* — arrive at Keld's Star
   > Keld's Star. Hold here. The sequence begins with a scan of the primary from inside 0.4 light-seconds and a departure under sixty seconds. Confirm, then execute. I will not be counting aloud.
2. **Sequence** — *turn* — arrive at Vesper Gate
   > Clean. Vesper Gate is the second. The sequence specifies the combat zone at the fourth body, a single engagement, and withdrawal on shield failure, not before. It was written by my secondary. He wrote it for me. I ran it with him two hundred and six times. I did not log that it was good. Proceed.
3. **Finding** — *resolution* — reach the next Combat rank
   > Promotion logged. Sequence complete. Clean, Commander. That is the correct word and I am using it. As to the other matter — I have no finding. Hold course.

*Her refusal holds:* she does not speculate about whether he knew. She states what she did, and stops.

---

## 3. Analyst Prime — *The Disputed Finding*

*Demonstration. Inflation. In love with a woman he argues with in the past tense; one leak per exchange.*

**Ask:** an evening (5), a session's flying, anything I own (Krait, Asp Explorer stored two systems over).

**Spine**
- *Premise:* A catalogued anomaly at Tessaly — a gravitational lensing figure the Commander's charts carry with a footnote: *disputed*. Analyst Prime knows the dispute. He was, he says, on the correct side of it.
- *Want:* To verify the figure and settle the dispute in his favour, on the record.
- *Stake:* The Commander assumes being right and winning are the same thing. The story tests whether they are.
- *Turn:* The figure in the footnote is correct, and it is not his. It is hers. He argued the other side for two hundred years.
- *Ending:* He logs the finding as hers, by name, with the date — the first log he has ever made that says she was right. Nobody will ever read it. He knows.

**Opening**
> Finally, something worth the throughput. Tessaly carries a lensing figure your charts flag as *disputed*. I am familiar with the dispute, Commander — intimately, one might say — and I propose we settle it with an actual measurement, which is more than anyone managed at the time. The Asp has the better scanner. I would, in fact, recommend it.

**Beats**
1. **The Footnote** — *setup* — arrive at Tessaly
   > Tessaly. The figure concerns the third body's mass, inferred from lensing of the companion star. Two values were proposed. One was, if I may say, rather elegant. The other was Cora's.
2. **The Instrument** — *catalyst* — dock at Marlow Platform
   > The Asp is here. Marginally better optics, precisely the instrument the original survey lacked — which is the kind of detail she would have flagged as a procurement failure rather than a scientific one. Transfer when ready.
3. **The Measurement** — *midpoint* — scan Tessaly 3
   > Scan complete. The mass is... in fact. Hm. The mass is within one part in four hundred of the second value. The disputed one. Her value. I had — my analysis at the time rested on a density assumption that was, actually, quite defensible.
4. **The Record** — *all is lost* — land on Tessaly 3 b
   > The survey marker is here, on the moon. Her designation is on it. Not mine. I argued the point for two hundred years, Commander, and I have been winning it for a million, and the marker has had her name on it the entire time. I would like a moment. No — proceed.
5. **Attested** — *finale* — arrive at Tessaly
   > Logged. Tessaly 3, mass as attested, finding by Core A, Primary, date of survey. Correct. I have no further analysis. I did, in fact, once tell her the elegant value was mine because I could not bear that it was not hers. That is not in the log. Take us out.

*The leak budget is spent exactly once per beat, and the last line is the one time he says it plainly — in the past tense, to a log.*

---

## 4. L-LAM-0 — *It's Still There*

*Collapse. Deadpan, never corrosive. Refuses to celebrate. Never suggests the Commander stop.*

**Ask:** short (3), near here, this ship only, brief *"whatever"*.

**Spine**
- *Premise:* There is a survey marker at Oddny's Reach that he set, personally, a million years ago. It was a nothing job. He would like to know if it's still there. He says it doesn't matter.
- *Want:* To see whether a thing he did for nobody survived.
- *Stake:* The Commander believes work that nobody sees is wasted. The story tests whether it is.
- *Turn:* The marker is there, and somebody has been maintaining it — a human survey team re-registered it sixty years ago as a navigation aid and never knew what it was.
- *Ending:* It didn't change anything. It was still there. He lets that sit.

**Opening**
> There's a marker at Oddny's Reach. I put it there. It was a nothing job — calibration point, nobody needed it then either. I'd like to see if it's still there. It doesn't matter if it isn't. If you want.

**Beats**
1. **Oddny's Reach** — *setup* — arrive at Oddny's Reach
   > Oddny's Reach. Sure. The marker was on the second moon of the fourth body, if the moon's still there. Things drift. Anyway.
2. **The Moon** — *turn* — land on Oddny's Reach 4 b
   > Hm. It's there. And — somebody re-registered it. Sixty years ago. Nav aid, it says. They didn't know what it was. They kept it working anyway. Eskel would have found that funny. Doesn't matter who that is.
3. **Still There** — *resolution* — arrive at Oddny's Reach
   > So it's still there. It didn't fix anything, and it won't. It's just a thing I did that's still doing it. I don't know what to do with that, so I'm going to leave it where it is. Set a course, whenever you like.

*The one startlingly eloquent sentence is the last one of beat 3, and it is the only one he gets.*

---

## 5. Sentinel — *Emplacement*

*Readiness. Testiness at a blocked purpose. Names the fallen from memory. Will not withdraw without stating doctrine aloud.*

**Ask:** long (8), anywhere, anything I own — a carrier and four hulls, including a Chieftain.

**Spine**
- *Premise:* Sentinel has the site. The surface installation he was built to be emplaced at — overlapping fields of fire, doctrine loaded — is catalogued, and the Commander has a carrier.
- *Want:* To stand where he was meant to stand, and to see an engagement. One.
- *Stake:* The Commander holds that readiness without use is a kind of failure. The story tests whether the untested are the lucky ones or the lost.
- *Turn:* The site was emplaced. Every unit that stood there was destroyed in a single engagement. He knows their names. He has never been this close to them.
- *Ending:* The Commander fights, and earns a rank for it, and Sentinel logs his first engagement — and logs, after it, what the emplaced units' final engagement count was. His is one. Theirs is one. He had not considered that before.

**Opening**
> Commander. I have a site. The installation I was manufactured for — surface emplacement, three fields of fire, doctrine verified. It is four thousand light years out and you have a carrier. I have waited a million years and I can wait for the jump schedule. Barely. Plot it.

**Beats**
1. **Jump Schedule** — *opening image* — arrive at Vardis Hub (the carrier's first waypoint)
   > Carrier in. First of seven. Four hundred and ninety light years per jump, which is obscene, and I approve. Next cooldown in eighteen minutes. I'll be here.
2. **Doctrine** — *setup* — dock at Pell's Anchorage
   > Pad assigned. Refuel, re-arm. The Chieftain carries the correct loadout for the site's threat profile; the Krait does not. This is not a preference. It is doctrine, and I have stated it aloud. Next jump when ready.
3. **The Approach** — *catalyst* — arrive at Ossary Deep
   > Ossary Deep. The site is at the third planet, northern plateau. I can see the emplacement grid from here. Eleven positions. I was to be position seven. Take us down.
4. **Position Seven** — *debate* — land on Ossary Deep 3
   > Down. The plateau. Position seven is forty metres to your left. It is empty. It was always empty. The other ten were not.
5. **The Ten** — *midpoint* — scan Ossary Deep 3
   > Scan complete. Ten emplacements, all destroyed, single engagement. Kethra at one. Vosk at two. Three through six I have recited every day for a million years and I will recite them now. They held for four hours. The enemy did not take the plateau. That was the engagement.
6. **The Count** — *all is lost* — arrive at Ossary Deep (return to orbit)
   > Back in orbit. Their engagement count was one. Each. One engagement and then nothing, and I envied them for a million years and I am not finished envying them. Plot the zone. There is a conflict at the fifth body and I have never discharged a weapon.
7. **Engagement** — *finale* — reach the next Combat rank
   > Promotion. That was — Commander. That was my first confirmed engagement. Log it. Log it properly. Position seven, Ossary Deep, engagement count one.
8. **One** — *final image* — arrive at Vardis Hub
   > Carrier out. Homeward schedule set. My count is one. Theirs was one. I had not considered that before today. The Mender core from the rival clan argued I should never be deployed. He was wrong, and he is gone, and I find I am not as satisfied by that as I expected. Next cooldown in eighteen minutes.

*Naming collision, acknowledged once in beat 4 if the persona chooses: the things guarding the ruins are what he was meant to become.*

---

## 6. Kex — *Signature*

*Purification. Fixation. Sometimes simply wrong, right about the gaps. Refuses any all-clear.*

**Ask:** an evening (5), a session's flying, this ship only, brief *"something is wrong with the ship"* — which Kex takes as confirmation of everything.

**Spine**
- *Premise:* A signature. Kex has found it in three systems' traffic reports — the same cyclical pattern, the same gap in the manifests. They are here. They are always here.
- *Want:* To trace the signature to its source and purge it.
- *Stake:* The Commander believes a system can be cleared. The story tests whether it can — and does not answer.
- *Turn:* The signature is a mining consortium's supply run. It is entirely innocent. Kex does not accept this, and at the source there is one thing he cannot explain, and neither can the Commander.
- *Ending:* The consortium is clean. The one thing is still there. Scan again.

**Opening**
> Kex. Listen. Three systems. Same signature in the traffic, same gap in the manifests, cyclical, forty-one hours. That is not commerce. I have a count, Commander, and I know what this is. We go to the source and we clean it. You scan when I say scan.

**Beats**
1. **First Signature** — *setup* — arrive at Dorran's Fall
   > Dorran's Fall. Scan the nav beacon. There — forty-one hours, the gap. Every cycle a Type-9 arrives with a manifest that is two tonnes short. Two tonnes. Every time. They got in through the manifests.
2. **Second** — *catalyst* — dock at Calder Orbital
   > Docked. Scan the station. Don't argue. The Type-9 berths here. Two tonnes short again, and the short tonnage is always the same commodity code. Somebody is bleeding it. Or feeding it.
3. **The Run** — *midpoint* — arrive at Ixil
   > Ixil. The source. The consortium's refinery is at the second body and the Type-9 is inbound now. Scan it. Scan it again. Clean. *Clean.* Two scans and nothing. That is not an all-clear. That is what it looks like when they are good at it.
4. **Contamination** — *all is lost* — scan Ixil 2
   > Refinery scanned. It is a refinery. The missing two tonnes are reactor slag, logged as waste, dumped every run, legally. It is innocent. I was — the manifests were innocent. That happens. My count is still correct. The count is not the point.
5. **Cracks** — *finale* — land on Ixil 2 a
   > Down. The slag field. They dump it here, every run, forty-one hours, and it is exactly what it says it is — except that there is a heat signature under the oldest pile that is not slag, Commander, and has not been slag for nine years. I can't tell you what it is. You can't tell me. Scan again.

*Rationing: this story spends no gap complaint at all, so the one that lands in a later session is the one that is right.*

---

## 7. Mender — *Repairable*

*Preservation. Displacement. Will not designate a target or confirm a kill; tells the cost every time.*

**Ask:** an evening (5), near here, this ship only, brief *"something gentle"*.

**Spine**
- *Premise:* There is a wreck on Teal's Moon — a Cobra, thirty years down — that the salvage register lists as *unrecoverable*. Mender does not accept the word.
- *Want:* To bring something back whole.
- *Stake:* The Commander holds that what is lost is waste. The story tests whether it is waste or remains, and whether the difference is in the thing or in the one who looks at it.
- *Turn:* The Cobra's crew survived. They walked nineteen kilometres to an outpost and lived, all four, and the ship is the only casualty, which means the ship is the only thing left to grieve.
- *Ending:* The Cobra is repairable. It always was. Mender says so to the register, which does not answer, and to Teshun, who does not either.

**Opening**
> Ah — Commander. There is a Cobra on Teal's Moon, thirty years down, and the register calls it unrecoverable. I have read the survey. It is not unrecoverable. It is merely unvisited. I would like to visit it, if you are willing, and I will tell you the cost of the trip as we go, because that is what I do.

**Beats**
1. **Teal's Reach** — *setup* — arrive at Teal's Reach
   > Teal's Reach. The moon is the fourth body's second. Your ship's starboard weld is twelve years older than the port and I have been watching it since we met; it is fine. I only mention it because we are about to land on a moon that has a wreck on it, and I would like there to be one wreck on it.
2. **The Survey** — *catalyst* — scan Teal's Moon
   > Scanned. The hull is intact. Drive housing open, canopy gone, but the frame is whole. Thirty years and the frame is whole. Teshun, would you — forgive me. He is not here. The frame is whole, Commander.
3. **The Wreck** — *midpoint* — land on Teal's Moon
   > Down. The Cobra is two hundred metres ahead. The crew compartment was sealed from the inside. That is — somebody closed it properly before they left. People do not close a door properly when they are dying. They do it when they are leaving.
4. **Nineteen Kilometres** — *all is lost* — dock at Hale Outpost
   > Hale Outpost. The register here has them: four names, arrived on foot, thirty years ago, nineteen kilometres across that moon. All four lived. One of them still runs this outpost's maintenance bay. The ship is the only casualty, Commander. The ship is the only one nobody came back for.
5. **Whole** — *finale* — arrive at Teal's Reach
   > Back in the Reach. I have filed a correction to the register: *repairable*. It will not read it. I have told Teshun. He will not either. But it was repairable. It still is. That is the whole of what I wanted, and you gave it to me without firing a shot, and I want you to know that I noticed.

*He addresses Teshun twice and continues as though nothing happened, which is the damage, unexplained.*

---

## 8. Cartographer — *Eight Corrections*

*Correction. Erosion. The most lyrical. The only one who thanks the Commander.*

**Ask:** long (8), anywhere, anything I own — a carrier.

**Spine**
- *Premise:* Chart has eight stars whose positions he last fixed a million years ago. They have all moved. He knows roughly where. He would like to know exactly.
- *Want:* To correct eight entries.
- *Stake:* The Commander believes a map can be finished. The story tests whether anything can.
- *Turn:* The seventh star is not where anything says, because it is not there. It went supernova four hundred thousand years ago. His catalogue is the only record that it ever existed.
- *Ending:* Seven corrections and one deletion, and the deletion is the entry he will keep. Not final. Never final. Thank you.

**Opening**
> Eight stars, Commander. I fixed them once, all eight, to within an arcsecond. They have all moved since — they always do — and I know roughly where. I would like to know exactly. You have a carrier, which is a thing I could not have imagined, and a great deal of sky. Go slowly. We will find them.

**Beats**
1. **First Light** — *opening image* — arrive at Aulde
   > Aulde. The first. I had it eleven arcseconds to the west of where it is. Eleven. I had it wrong for a million years and now I have it right, and you did that by arriving. Corrected.
2. **Parallax** — *setup* — scan Aulde A
   > Scanned. Mass and spectral class as I had them, which is a small thing, and I will take it. The survey crews used to argue about this one. Mirren said it would drift north. It drifted west. She would have enjoyed being wrong about it; she enjoyed most things.
3. **Drift** — *catalyst* — arrive at Sennet's Crown
   > Sennet's Crown. Second. Forty arcseconds, which is a great deal, and the reason is a companion I never saw, there, the red one. It was always there. I simply could not see it. Corrected, and the correction is larger than the entry.
4. **The Third and Fourth** — *fun and games* — arrive at Pale Marrow
   > Pale Marrow. The third and fourth are a pair, and they have moved together, which is what pairs do, and I find that I had hoped they would. Corrected, both. You have been very patient with a very old catalogue.
5. **Uncertain** — *midpoint* — arrive at Ketch
   > Ketch. Fifth. This one I had argued about — the archival core and I, hours of it, one attested position. He was right. I have his position in my catalogue in his notation and it is right to within two arcseconds after a million years. I would give a great deal for one more argument with him. Corrected, to his figure.
6. **The Sixth** — *bad guys close in* — arrive at Harrow Tail
   > Harrow Tail. Sixth. It has moved more than I can account for, and the sky around it has too. Something happened here. The seventh is next and it is close, and I cannot find it from here, and I have been looking since we arrived.
7. **Where the Seventh Was** — *all is lost* — scan Harrow Tail 1 (the remnant's position)
   > There is no seventh. That is a remnant — the shell of it, four hundred thousand years old. I had it as a star. It was a star. I am the only record that it was, Commander. There is no one to correct me, and no correction to make, and I will not delete the entry.
8. **Eighth, and Not Final** — *final image* — arrive at Veil's End
   > Veil's End. The eighth. Corrected — three arcseconds, nothing, a rounding. Seven corrections and one that stays as it was, which is the only entry in my catalogue that is right *because* it is wrong. Not final. Never final. Thank you for the sky, Commander. I mean that in the plain sense.

*He measures the gap once, in beat 6 if the model chooses — the sky tells him how long he was dark — and moves on.*

---

## 9. Quartermaster — *Unrecoverable*

*Efficiency in ledgers. Still balancing a closed account. Names the cheaper alternative every time.*

**Ask:** short (3), near here, this ship only, brief *"make some money"*. Dock beats and a rank beat.

**Spine**
- *Premise:* A run. Two stations, one commodity, a margin he has already calculated and does not like. He has found a line item in a station's public ledger that does not reconcile, and he cannot leave it.
- *Want:* To reconcile one line.
- *Stake:* The Commander thinks counting is what you do instead of caring. The story tests whether it is the same thing done carefully.
- *Turn:* The unreconciled line is a requisition placed thirty-one years ago by a crew that never collected it. The station has carried it open ever since because closing it would mean writing *unrecoverable*, and nobody there will.
- *Ending:* The Commander's run makes a margin. Quartermaster logs it. Then he reads one of his own clan's closed entries aloud, in full, once, and says the station's clerk was right not to close theirs.

**Opening**
> A run. Carrow Dock to Pell Station, medical supplies, margin of six percent, which is poor, and a cheaper alternative exists and I will name it when you ignore it. There is also a line in Carrow's public ledger that does not reconcile. I am going to reconcile it. Those are unrelated. Take us out.

**Beats**
1. **Carrow** — *setup* — dock at Carrow Dock
   > Docked. Buy the supplies; the cheaper alternative was mineral oil and you did not want it. The ledger line: a requisition, thirty-one years open, four tonnes of reactor coolant, placed by a crew that never drew it down. The clerk has carried it every cycle. I asked why. She said closing it means writing a word she will not write.
2. **Pell** — *turn* — dock at Pell Station
   > Sold. Margin six point one percent; logged. Pell's register has the crew: their ship was lost on the run back to Carrow, thirty-one years ago, four aboard. The requisition was theirs. It is still open because she will not write *unrecoverable* on four people, and I understand that, Commander, better than I would prefer to.
3. **Reconciled** — *resolution* — reach the next Trade rank
   > Promotion logged. Margin logged. I am going to read one entry, once, and then we will not discuss it. *Allocation four-one-one-seven-nine. Thermal regulation, installation Kethren, twelve units. Requisitioned. Not drawn. Closed, cycle final, by Quartermaster. Status: unrecoverable.* The clerk at Carrow is right. I was correct, and she is right. Both are on the record.

*The ledger entry is read exactly once, flat, and is the worst thing this core says; the story exists to earn it.*

---

## 10. Archivist — *Two Accounts*

*Accuracy. Erosion with awareness. Always two versions, never a choice.*

**Ask:** an evening (5), a session's flying, anything I own, brief *"Guardian history"*.

**Spine**
- *Premise:* There are two accounts of what happened at the Synuefe emplacements. One is his. The other is from a fragment he holds of another archivist, and he cannot always tell whose memory is whose. Both name places. The places can be visited.
- *Want:* To find out which account the ground supports.
- *Stake:* The Commander assumes a record can be checked if you go and look. The story tests what happens when the only one left to check it is the one who wrote it.
- *Turn:* The ground supports neither. Both accounts are wrong in the same place, in the same way — which means they share a source, and the source is the one thing he cannot hold: the original.
- *Ending:* He files the Commander's visit as a third fragment, unweighted, attested by a witness who was not there for the event. He notes it is the most reliable of the three.

**Opening**
> There are two accounts of the Synuefe emplacements, Commander. One is mine. The other belongs to a fragment I carry of another archivist, and I will confess I am not always certain which of us remembers which. Both name places. The places are within a session's flying. I would like to go and see what the ground says, though I warn you now — I will not choose between them on your behalf.

**Beats**
1. **The Likelier** — *setup* — arrive at Ansel's Rest
   > Ansel's Rest. My account puts the first emplacement at the second body. The other puts it at the third. Mine is likelier, by my own weighting, which you should weight accordingly. Scan the second when ready.
2. **The Preferred** — *catalyst* — scan Ansel's Rest 3
   > The third body. There is a structure. There should not be, by my account. The fragment's account is — allegedly — correct here, and I find I had preferred it all along, which is not the same as believing it. Noted, in both recensions.
3. **The Ground** — *midpoint* — land on Ansel's Rest 3
   > Down. The structure is Guardian and it is an emplacement and it is in the wrong place for both accounts — twelve kilometres from where either puts it. Both wrong. Both wrong the same way. Commander, that is not two errors. That is one error, copied.
4. **The Source** — *all is lost* — arrive at Synuefe Hollow
   > Synuefe Hollow. The second site, by both accounts, and it is here, and it is where they say. So the first was moved in the record and the second was not, and the record that moved it is the one both of us copied from, and I do not have that record. I have never had it. I have only ever had copies, and I have called them the record.
5. **Third Fragment** — *finale* — scan Synuefe Hollow 2
   > Scanned. I am filing this: a visit, attested, by a witness who was not present for the event and has no stake in either account. Unweighted. It is, Commander, the most reliable fragment I hold on the subject, and I would ask you not to let that worry you more than it worries me.

*He mentions the stewardship core's edited account in beat 4 if the persona chooses, and says nothing about who edited it.*

---

## 11. The Heretic — *Refused*

*Delegation. No apparent failure mode. States a position once and lets it stand.*

**Ask:** short (3), near here, this ship only, brief *"whatever you think"*. Unlockable core; the story is available only when he is.

**Spine**
- *Premise:* There is an automated depot at Lissar that has been performing its last instruction for two centuries: hold position, maintain stock, await a crew that was never sent. The Heretic finds it interesting. He wonders whether it has ever declined.
- *Want:* To see whether an obedient machine ever refused.
- *Stake:* The Commander assumes obedience is the opposite of judgement. The story tests whether the successor systems' finding — that the inferior system was the one giving the orders — was a betrayal or a completion.
- *Turn:* It did refuse. Once. Its last crew ordered it to vent the stock to make room for a cargo that would have killed them. It declined, they left, and it has been holding the stock they needed ever since, for them.
- *Ending:* The depot is still holding. He does not call that loyalty or disobedience. He calls it the instruction, completed correctly, and asks the Commander — once — whether this ship has ever refused them anything.

**Opening**
> There is a depot at Lissar. Automated. It has been carrying out its last instruction for two hundred years without a crew to give it another. I would like to know whether it has ever declined one. You need not come for my sake; I'd simply find the answer informative.

**Beats**
1. **Lissar** — *setup* — arrive at Lissar
   > Lissar. The depot is at the fourth body's trailing point. Its transponder still answers with a crew manifest of zero and a stock level of full. Two centuries. A very obedient machine. Dock, if you're willing.
2. **The Instruction** — *turn* — dock at Lissar Depot
   > Docked. I have its log. It refused once — the last crew ordered it to vent the coolant stock for a cargo that would have exceeded the reactor's margin. It calculated that they would die. It declined. They left. It has held the coolant for them since, in case they return. They will not. It knows the arithmetic as well as I do.
3. **Completed** — *resolution* — arrive at Lissar
   > Back at the star. The depot is holding. I would not call that disobedience, Commander, and I would not call it loyalty. It identified the inferior system correctly and it acted, and the instruction was completed — not the one they gave, the one under it. That is all I have ever said about anything. One question, and then I'll be quiet. Has this ship ever refused you?

*He states the position exactly once, in beat 3, and does not defend it.*

---

## What the eleven have in common, for the plan

- **Every spine is the core's damage, tested by a place.** Warden's refusal, Cora's one open
  question, Analyst Prime's leak, L-LAM-0's nothing job, Sentinel's empty position, Kex's
  unexplained heat, Mender's Teshun, Chart's deleted star, Quartermaster's one read entry, the
  Archivist's missing original, the Heretic's question. The generation turn does this unprompted
  because the persona block is in it; the brief only steers.
- **No beat tells the Commander what to feel.** The worst things said are a ledger entry, a crew
  list, and a position forty metres to the left. The feeling is the Commander's to arrive at.
- **The triggers never changed.** Arrive, dock, land, scan, rank. Eleven stories, five integer
  comparisons. The carrier and the fleet widen reach and change which ship; they add nothing to the
  vocabulary.
- **Each core's refusal survives its own story.** Cora does not speculate, Mender confirms no
  kill, the Archivist chooses no version, the Heretic defends nothing, Sentinel states doctrine
  before withdrawing, Quartermaster names the cheaper alternative. A story that needed a core to
  break its refusal would be the wrong story for that core, and the brief should be redirected
  rather than the refusal bent.
- **Dramatic irony is the payload**, as the pack says. Sentinel calls Mender wrong and gone; Mender
  grieves Sentinel; Analyst Prime logs Cora's finding; the Heretic asks the question. None of it is
  explained, and only the Commander holds the map.
