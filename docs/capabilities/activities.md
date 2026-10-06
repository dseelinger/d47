---
title: Activities
group: Conversation
nav_order: 152
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to what you have not done lately.</p>
<section>
<h2><span class="num">1</span> Ask what you have not done in a while.</h2>
<p class="say">Say "what's something I haven't done in a while".</p>
<p class="expect">D47 names the three activities you did longest ago, oldest first, and how long ago each was.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with the question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what's something I haven't done in a while</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Fourteen activities are read from your journals, with no AI.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ask when you last did one.</h2>
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
<p class="expect">D47 answers once it has read your older journals.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The journals are still being read">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what did I last do</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Older journals are read in the background.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band" open>
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Ask what you have not done in a while and D47 answers from your journals, with no language model.</p>
<section>
<h2><span class="num">1</span> The three oldest, every time.</h2>
<svg viewBox="0 0 880 196" role="img" aria-label="The three activities you did longest ago, oldest first">
 <rect x="20" y="20" width="840" height="156" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="56" font-size="16" fill="var(--text)">Search and rescue</text>
 <text x="836" y="56" text-anchor="end" font-size="16" fill="var(--text-muted)">a year and eight months ago</text>
 <text x="44" y="92" font-size="16" fill="var(--text)">Powerplay</text>
 <text x="836" y="92" text-anchor="end" font-size="16" fill="var(--text-muted)">a year and four months ago</text>
 <text x="44" y="128" font-size="16" fill="var(--text)">Combat zones</text>
 <text x="836" y="128" text-anchor="end" font-size="16" fill="var(--text-muted)">a year and one month ago</text>
 <text x="44" y="162" font-size="14" fill="var(--text-muted)">The rest are on the Activities page.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Switching one off hides the suggestion, not the date.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Switching an activity off removes it from suggestions and leaves its date on the Activities page">
 <rect x="20" y="20" width="400" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="62" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text-muted)">SUGGESTIONS</text>
 <text x="220" y="98" text-anchor="middle" font-size="16" fill="var(--text)">the activity is left out</text>
 <rect x="460" y="20" width="400" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="62" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">ACTIVITIES PAGE</text>
 <text x="660" y="98" text-anchor="middle" font-size="16" fill="var(--text)">its date stays, from the journals</text>
</svg>
</section>
</div></div>
</details>

## The details

D47 reads the dates of fourteen activities out of your journals: mining, exploration, bounty hunting,
combat zones, Powerplay, on-foot work, exobiology, trading, engineering, fleet carrier, missions,
search and rescue, the ship-launched fighter and colonisation. The Activities page, under Checklist,
lists them all with their dates.

### What have I not done lately

> "what's something I haven't done in a while"

D47 names the three suggested activities with the oldest last-done date, oldest first, and says how
long ago each was. If fewer than three can be suggested, it says how many, and if none can, it says so.
An activity with no date in your journals is never suggested.

```text
Search and rescue, a year and eight months ago. Powerplay, a year and four months ago. Combat zones, a year and one month ago. The rest are on the Activities page.
```

Until d47 has finished reading your older journals it says "I'm still reading your journals. Ask again
in a minute."

### When did I last do one

> "when did I last go mining"

```text
Mining, last on 2 September 3312. Four weeks ago.
```

Dates are in the galactic year. An activity with no date in your journals is described by where the
journals begin, never as "never": "Ship-launched fighter: not in your journals since 14 December 3310."
This works for an activity switched off from suggestions too.

### Stop suggesting one

> "don't suggest bounty hunting anymore"

> "suggest bounty hunting again"

```text
Not suggesting: bounty hunting. It stays on the Activities page, with its date.
Back in the suggestions: bounty hunting.
```

This is your decision, so the language model cannot make it. Asked indirectly, the model is refused and
passes on the phrase to say.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_stale_activities`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `get_activity_last_done`

```json
{"type":"object","properties":{"activity":{"type":"string","description":"Which activity, by name or by what the Commander calls it."}},"required":["activity"],"additionalProperties":false}
```

#### `set_activity_suggested`

```json
{"type":"object","properties":{"activity":{"type":"string","description":"Which activity, by name or by what the Commander calls it."},"suggested":{"type":"boolean","description":"False to stop suggesting it, true to suggest it again."}},"required":["activity","suggested"],"additionalProperties":false}
```

</details>
