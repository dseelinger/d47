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

<details markdown="1">
<summary>The tool surface, for contributors</summary>

`set_campaign_faction` and `set_system_intent` are protected: the phrases above reach them, and the
model is refused.

#### `get_campaign`

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
