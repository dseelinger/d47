---
title: Campaign
group: Knowledge
nav_order: 166
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Keep a plan for one minor faction, a system at a time.</p>
<section>
<h2><span class="num">1</span> Name the faction, if it is not your squadron's.</h2>
<p class="say">Say "our campaign faction is Hutton Truckers".</p>
<p class="expect">D47 checks the name with galaxy search and says "The campaign faction is Hutton Truckers."</p>
</section>
<section>
<h2><span class="num">2</span> Say what you mean to do in a system.</h2>
<p class="say">Say "hold Shinrarta Dezhra", "grow Lave" or "leave Lave alone".</p>
<p class="expect">D47 confirms the system and the intent, such as "Lave: grow."</p>
</section>
<section>
<h2><span class="num">3</span> Ask for the plan.</h2>
<p class="say">Say "what is our campaign".</p>
<p class="expect">D47 names the faction and how many of its systems are set to each intent.</p>
</section>
<section>
<h2><span class="num">4</span> Ask what needs doing.</h2>
<p class="say">Say "what are our fronts".</p>
<p class="expect">D47 names the three most pressing fronts, each with its system and why, and counts the rest.</p>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Give an intent for a system D47 has not placed the faction in.</p>
<p class="expect">Nothing is set. Ask "what is our campaign" first, which fetches every system the faction is in.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A campaign is one faction and what you mean to do in each of its systems.</p>
<section>
<h2><span class="num">1</span> The faction is your squadron's unless you name another.</h2>
<p class="body">D47 already knows your squadron's faction. Naming another replaces it for the campaign, and the intents set for the old faction's systems are cleared.</p>
</section>
<section>
<h2><span class="num">2</span> Each system has one intent from a fixed list.</h2>
<p class="body">Hold, grow, expand from here, keep under seventy-five percent, take control, or leave alone. A system with no intent is hold where the faction controls it and leave alone where it does not.</p>
</section>
<section>
<h2><span class="num">3</span> The plan is kept per Commander.</h2>
<p class="body">It is saved in <code>data/campaigns.json</code> and is still there after a restart. The model can read it and cannot change it.</p>
</section>
<section>
<h2><span class="num">4</span> D47 works out the fronts, not the model.</h2>
<p class="body">After each BGS tick D47 fetches the faction's systems, uses your journal readings in place of its figures where they are newer, and lists what needs attention, most pressing first. The model only reads the list.</p>
</section>
</div></div>
</details>

## The details

A campaign belongs to one Commander and holds the campaign faction, an intent for each system the
faction is in, and the chronicle of resolved fronts. It is kept in `data/campaigns.json`, keyed by
the Commander's Frontier id.

### The faction

The campaign faction is your squadron's faction until you name another:

> "our campaign faction is Hutton Truckers"

D47 checks the name with galaxy search, which sends it to the galaxy search service, and stores the
spelling the service gives. Naming a different faction clears the intents set for the previous one.

### Intents

Each intent is spoken with the system's name:

| Say | Intent |
|---|---|
| "hold Lave" | Hold |
| "grow Lave" | Grow |
| "expand from Lave" | Expand from here |
| "keep Lave under seventy-five" | Keep under 75% |
| "take control of Lave" | Take control |
| "leave Lave alone" | Leave alone |

None of these needs the language model. They work for a system you have seen the campaign faction in
during the last week, a system that already has an intent, and every system the galaxy service listed for the
faction when you last named it or asked for the campaign this session. A system with no intent is
Hold where the faction controls it and Leave alone elsewhere:

```csharp
public SystemIntent IntentFor(string system, bool controls) =>
    Intents.TryGetValue(system, out var set) ? set : controls ? SystemIntent.Hold : SystemIntent.LeaveAlone;
```

### Reading it back

> "what is our campaign"

The answer names the faction and counts the systems by intent. The model reads the full list, one
line per system, with "(default)" after an intent nobody set, and the chronicle's latest three
fronts. Asking fetches the faction's systems from galaxy search; when that is not available, only
the systems with an intent set are listed.

### The chronicle

Each resolved front is kept with its kind, the system, the opponent if there was one, the outcome,
the ticks it spanned and your own BGS work on it. It records what happened and claims no share of
the outcome.

### Fronts

> "what are our fronts"

A front is a system where the campaign faction has something to win, defend or check. D47 computes
them in code from the faction's galaxy report and your own journal readings, so the list is the same
every time it is asked for the same data. The model reads it and never ranks it.

| Front | When |
|---|---|
| Conflict | A war, civil war or election, pending or active. Days won and stakes come from your journal when you have visited. |
| Coup threat | Another faction at 50% or more in a system the faction controls; at 60% it can take control. |
| Retreat | Retreat pending or active. |
| Thin margin | In a controlled system, the lead over the nearest rival is under the intent's margin. Skipped during a conflict, when influence is frozen. |
| Bad state | Lockdown, civil unrest, famine, blight, drought, outbreak, infrastructure failure, pirate attack, terrorist attack or bust. |
| Unwanted expansion | Influence of 70% or more where the intent is keep under 75%. |
| Expansion under way | Expansion pending or active. |
| Stale report | The newest report of a system that is not left alone is more than 30 days old. |

They are ranked in that order. Within a kind, a conflict with a stake comes first, then a system the
faction controls, then the more populous system, then the smaller margin. The thresholds are in one
table, each with its source:

```csharp
/// <summary>Influence at which a faction expands (p.65).</summary>
public const double Expansion = 0.75;

/// <summary>Influence at which a faction that does not control a system can take it by a coup (p.60).</summary>
public const double Coup = 0.60;
```

The margins a thin margin is measured against are D47's own: 10 points for hold, grow and keep under
75%, 15 for expand from here, and none for take control or leave alone.

D47 fetches the report when it first knows the campaign faction, again after each BGS tick, and
again when you name another faction. The fetch runs in the background and sends the faction's name
to the galaxy search service, as the Faction search on the Search tab does. When it fails, the fronts from the last
report are kept, the answer says when they were computed, and D47 tries again after half an hour.
Asking for the fronts never fetches.

### One front at a time

Naming a front to work keeps you on it. While none is chosen, D47 proposes the most pressing front
after each fetch and at the start of a session. A proposal writes nothing.

| Say | Effect |
|---|---|
| "take it" | Works the front D47 proposes. Heard only while one is proposed. |
| "next front" | Proposes the next front in the ranking, wrapping to the top. |
| "take the war in Lave" | Works that front. The noun is the front's kind: "war", "civil war", "election", "coup threat", "retreat", "bad state" and so on. |
| "take the front in Lave" | Works the front there, whatever its kind. |
| "drop the front" | Stops working it. Nothing is added to the chronicle. |
| "what should I do for the faction" | One sentence naming the work, then at most one sentence naming work that does not count. |

A chosen front stays chosen across restarts until it resolves or you drop it. Naming another campaign
faction drops it. D47 notes each BGS tick the front spans. A front resolves when the fronts computed
after you chose it no longer hold it: the conflict ended, the state is gone, the margin is restored.
A thin margin that disappears because a conflict or coup threat now covers the system has not resolved.
D47 then adds the front to the chronicle, with the BGS work you did for the faction there, and clears the choice.
A conflict that ended with a result in your journal is recorded as won or lost.

The next action is built in code from the front and from the bounties and combat bonds your journal
shows the faction paid you for in the last week and no redemption has followed:

```csharp
public sealed record CampaignNextAction(Front Front, string Sentence, string? NotCounting)
```

A war says that trade does not count, an election that combat does not count and that firing a weapon
may void some election missions, a Lockdown that only bounties help, and an Outbreak that bounty
vouchers do nothing. The model reads the answer and does not compose it.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

`set_campaign_faction`, `set_system_intent`, `take_campaign_front`, `next_campaign_front` and `drop_campaign_front` are protected: the phrases above reach them, and the
model is refused.

#### `get_campaign`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `get_campaign_fronts`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `next_campaign_action`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `take_campaign_front`

```json
{"type":"object","properties":{"kind":{"type":"string","description":"The kind of front there, when the system has more than one.","enum":["Conflict","Coup","Retreat","ThinMargin","BadState","UnwantedExpansion","Expansion","Stale"]},"system":{"type":"string","description":"The star system of the front; leave out to take the proposed front."}},"required":[],"additionalProperties":false}
```

#### `next_campaign_front`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `drop_campaign_front`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `set_campaign_faction`

```json
{"type":"object","properties":{"faction":{"type":"string","description":"The minor faction\u0027s exact name, as the Commander said it."}},"required":["faction"],"additionalProperties":false}
```

#### `set_system_intent`

```json
{"type":"object","properties":{"intent":{"type":"string","description":"What the Commander means to do there.","enum":["Hold","Grow","ExpandFromHere","StayUnder75","TakeControl","LeaveAlone"]},"system":{"type":"string","description":"The star system."}},"required":["intent","system"],"additionalProperties":false}
```

</details>
