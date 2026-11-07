---
title: Activities
group: Conversation
nav_order: 154
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to when you last did something.</p>
<section>
<h2><span class="num">1</span> Ask when you last did one.</h2>
<p class="say">Say "when did I last go mining".</p>
<p class="expect">D47 gives the date and how long ago, such as "Mining, last on 2 September 3312."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a last-done question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">when did I last go mining</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The date is in the galactic year.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> Give it time to read your journals.</h2>
<p class="say">If D47 says it is still reading your journals, ask again in a minute.</p>
<p class="expect">D47 answers once it has read your older journals. An activity with no date in them is described by where your journals begin.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The journals are still being read">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">when did I last go mining</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Older journals are read in the background.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Ask when you last did an activity and D47 answers from your journals, with no language model.</p>
<section>
<h2><span class="num">1</span> Each activity has one last-done date.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="An activity and the date D47 last found it in your journals">
 <rect x="20" y="20" width="840" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="62" font-size="16" fill="var(--text)">Mining</text>
 <text x="836" y="62" text-anchor="end" font-size="16" fill="var(--text-muted)">2 Sep 3312 · four weeks ago</text>
 <text x="44" y="102" font-size="16" fill="var(--text)">Ship-launched fighter</text>
 <text x="836" y="102" text-anchor="end" font-size="16" fill="var(--text-muted)">not in your journals since 1 Jan 3310</text>
</svg>
<p class="body">D47 takes the date of each activity from your journals. The question reads that date and says it with how long ago it was. No language model is asked.</p>
</section>
<section>
<h2><span class="num">2</span> The Activities page lists them all.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="The Activities page lists every activity, the ones not in your journals first">
 <rect x="20" y="20" width="840" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">COMMANDER › STATISTICS › ACTIVITIES</text>
 <text x="440" y="98" text-anchor="middle" font-size="16" fill="var(--text)">the ones not in your journals first, then oldest first</text>
</svg>
<p class="body">The page has the same dates as the answer, for every activity at once.</p>
</section>
<section>
<h2><span class="num">3</span> Until the older journals are read, it says so.</h2>
<svg viewBox="0 0 880 130" role="img" aria-label="The reply while the journals are still being read">
 <rect x="20" y="24" width="840" height="82" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="72" text-anchor="middle" font-size="19" font-weight="700" fill="var(--text)">“I'm still reading your journals. Ask again in a minute.”</text>
</svg>
<p class="body">D47 reads your older journals once, in the background, to date each activity. Until that finishes it gives this reply, a constant in <code>ActivitiesCapability.cs</code>, instead of a guess.</p>
</section>
</div></div>
</details>

## The details

D47 reads the dates of fourteen activities out of your journals: mining, exploration, bounty hunting,
combat zones, Powerplay, on-foot work, exobiology, trading, engineering, fleet carrier, missions,
search and rescue, the ship-launched fighter and colonisation. The Activities page, at Commander › Statistics › Activities,
lists them all with their dates, the ones not in your journals first.

Until d47 has finished reading your older journals it says "I'm still reading your journals. Ask again
in a minute."

### When did I last do one

> "when did I last go mining"

```text
Mining, last on 2 September 3312. Four weeks ago.
```

Dates are in the galactic year. An activity with no date in your journals is described by where the
journals begin, never as "never": "Ship-launched fighter: not in your journals since 14 December 3310."

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_activity_last_done`

```json
{"type":"object","properties":{"activity":{"type":"string","description":"Which activity, by name or by what the Commander calls it."}},"required":["activity"],"additionalProperties":false}
```

</details>
