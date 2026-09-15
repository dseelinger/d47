---
title: Carrier
group: Ship
nav_order: 104
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
<p class="intro">One question, answered from what Elite last wrote about the carrier.</p>
<section>
<h2><span class="num">1</span> Own a fleet carrier and let it write a status once.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">carrier report</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Nothing to set up. Open the carrier management screen once and D47 has the figures.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Fuel, cargo, balance, jump range and every service come from the same screen.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ask any of the ways in.</h2>
<svg viewBox="0 0 880 190" role="img" aria-label="Several phrasings that all reach the same report">
 <rect x="20" y="20" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="53" font-size="17" fill="var(--text)">how is my carrier</text>
 <text x="44" y="116" font-size="15" font-weight="700" fill="var(--accent)">carrier report · carrier status · carrier services · carrier fuel</text>
 <text x="20" y="166" font-size="16" fill="var(--text-muted)">Any of these reaches the same tool with no model in the loop.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Say "Captain" to talk to the carrier's captain.</h2>
<svg viewBox="0 0 880 212" role="img" aria-label="A question opened with Captain, answered by the carrier's captain until the line is ended">
 <rect x="20" y="20" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="53" font-size="17" fill="var(--text)">Captain, how much fuel have we got</text>
 <text x="836" y="53" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="112" font-size="16" fill="var(--text)">The captain answers, and answers the next question too, until you say "that's all".</text>
 <text x="20" y="144" font-size="15" fill="var(--text-muted)">"that'll be all", "thank you captain", "dismissed", "carry on" or the ship AI's name also end it.</text>
 <text x="20" y="182" font-size="15" fill="var(--text-muted)">More than 500 light years away, the ship AI says the carrier is out of range.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="Figures are only as fresh as the last carrier management screen read.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Figures are only as fresh as the last time you opened the screen.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Fuel, cargo and balance carry the date they were reported, because the carrier keeps working while you are away.</text>
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
<p class="intro">The carrier's own figures, apart from "where is it".</p>
<section>
<h2><span class="num">1</span> "Where is my carrier" and "how is my carrier" are different questions.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="get_fleet answers position, describe_carrier answers everything else">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">"where is my carrier"</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">system, and when</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">that was last seen</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">"how is my carrier"</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">fuel, cargo, balance,</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text)">jump range, services</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Kept apart so a short question does not answer with everything it did not ask for.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> The figures come from one journal event, and it only writes when you look.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="CarrierStats is only written when the Commander opens the carrier management screen">
 <rect x="20" y="36" width="840" height="90" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="70" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">CarrierStats</text>
 <text x="440" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">fuel · cargo · balance · jump range · docking access · services</text>
 <text x="440" y="160" text-anchor="middle" font-size="16" fill="var(--text)">Elite writes it when the carrier management screen opens, not continuously.</text>
 <text x="440" y="188" text-anchor="middle" font-size="15" fill="var(--text-muted)">A report says how old its own figures are rather than pretending they are live.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> No carrier known is not the same as no carrier.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="An unseen carrier is reported as unseen, never as owning none">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="62" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">"I have not seen you own a fleet carrier this session."</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Not "you have no carrier" — D47 only knows what the journal has shown it.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> The captain keeps a separate conversation, and the ship AI overhears it.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="The captain answers from their own brief and transcript; the ship AI is told the exchange as overheard">
 <rect x="20" y="36" width="400" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Captain</text>
 <text x="220" y="106" text-anchor="middle" font-size="15" fill="var(--text-muted)">own brief, own transcript</text>
 <rect x="460" y="36" width="400" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Ship AI</text>
 <text x="660" y="106" text-anchor="middle" font-size="15" fill="var(--text-muted)">hears each exchange as overheard</text>
 <text x="440" y="176" text-anchor="middle" font-size="16" fill="var(--text)">The captain never answers as the ship AI, and the ship AI can still refer to what was said.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">Where "where is my carrier" is answered.</span></a>
<a class="card" href="colonisation.html"><span class="ct">Colonisation →</span><span class="cd">A build that sources from the same carrier.</span></a>
</div>
</div>
</div></div>

## The details

The fleet carrier's own figures, apart from its position — `get_fleet` on the [Journal](journal.html)
page answers where it is.

### Ask for it

> "carrier report"
> "carrier status"
> "how is my carrier"
> "carrier services"
> "carrier fuel"

```text
Fleet carrier Sacred Fire (BNH-T2F) is in Fixture Nebula Point, as of 2026-09-05 00:00 UTC.
Fuel 792 t, as of 2026-09-05 00:00 UTC.
Cargo 540/25000 t (2% full), as of 2026-09-05 00:00 UTC.
Balance 750,352,669 cr.
Jump range 500 ly.
Docking access: all.
Services:
  BlackMarket: not bought
  Refuel: open, staffed by Rosa Guthrie
  Repair: closed, staffed by Ev Chang
```

### Talk to the captain

> "Captain, how much fuel have we got"

A question that opens with "Captain" goes to the carrier's captain instead of the ship AI, and is
spoken in the captain's voice. The captain answers from `describe_carrier` and the galaxy tools, and
answers follow-up questions with no name needed until the line is ended by one of these:

```csharp
public static readonly IReadOnlyList<string> Dismissals =
    ["that's all", "that'll be all", "thank you captain", "dismissed", "carry on"];
```

Starting a question with the ship AI's name also ends it. The ship AI is told each exchange as one it
overheard, so it can refer to it, but the captain's answers are not added to the ship AI's own
conversation.

With galaxy search on, a carrier more than 500 light years from you is out of range: the ship AI
says so and gives the distance, and no model is asked. With galaxy search off, or when the distance
cannot be found, the captain answers. The captain cannot be reached until a carrier you own and its
system have been seen in the journal.

### What Directive 47 actually knows about your carrier

Only what `CarrierStats` reports, and only from the last time you opened the carrier management
screen:

| Known | From |
|---|---|
| Fuel | `CarrierStats.FuelLevel` |
| Cargo against capacity | `CarrierStats.SpaceUsage` |
| Balance | `CarrierStats.Finance.CarrierBalance` |
| Jump range | `CarrierStats.JumpRangeCurr` |
| Docking access | `CarrierStats.DockingAccess` |
| Pending decommission | `CarrierStats.PendingDecommission` |
| Services and who staffs each | `CarrierStats.Crew` |

```csharp
/// <summary>Tritium in the carrier's own tank, from CarrierStats.</summary>
public int? FuelLevel { get; init; }
```

Every figure carries the date it was read, because a carrier keeps jumping, trading and burning
fuel while you are away from the screen that reports it. A booked jump — destination, body and
departure time — comes from `CarrierJumpRequest` instead, and is cleared the moment the jump fires
or is cancelled.

If no `CarrierStats` has ever been seen, the report says so rather than saying you have no
carrier — those are different answers, and only one of them is true.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `describe_carrier`

Reports the system it is in, a booked jump, fuel, cargo against capacity, balance, jump range,
docking access, the decommission flag and every service with its state and crew. Takes no
arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

</details>
