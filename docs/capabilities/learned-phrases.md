---
title: Learned phrases
group: Voice
nav_order: 125
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Four steps to your own wording for a phrase D47 knows.</p>
<section>
<h2><span class="num">1</span> Say you want to teach one.</h2>
<p class="say">Say "teach a phrase".</p>
<p class="expect">D47 asks "What do you want to say?" and takes your next reply word for word without acting on it.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a teach command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">teach a phrase</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"new phrase" does the same.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Say your wording, then what it should do.</h2>
<p class="say">Say your own wording, such as wheels out. When D47 asks what it should do, say a phrase it knows, such as "gear down".</p>
<p class="expect">D47 repeats the pair and asks whether to keep it: 'wheels out' will do 'gear down'. Keep it?</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with the phrase it stands for typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">gear down</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It must be a phrase D47 already knows.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Keep it or drop it.</h2>
<p class="say">Say "yes" to keep it, or "cancel" to drop it.</p>
<p class="expect">A yes stores the wording for this Commander; anything else leaves nothing behind.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with the answer typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">yes</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"cancel" at any step learns nothing.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Check it on the Phrases page.</h2>
<p class="say">Open Settings › Phrases and look under Your phrases.</p>
<p class="expect">The wording is at the top, newest first, with the phrase it runs.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The Phrases page lists what you taught">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">wheels out</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It runs "gear down".</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Settings › Phrases, under Your phrases.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A taught wording is stored per Commander. D47 swaps it for the phrase it stands for before the router or the model sees it.</p>
<section>
<h2><span class="num">1</span> You give it two replies and a yes.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Three replies in turn: the wording, the phrase it runs, and a yes">
 <rect x="20" y="24" width="250" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">WORDING</text>
 <text x="145" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">wheels out</text>
 <line x1="282" y1="69" x2="296" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="310,69 294,61 294,77" fill="var(--accent-muted)"/>
 <rect x="320" y="24" width="250" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="445" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">PHRASE</text>
 <text x="445" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">gear down</text>
 <line x1="582" y1="69" x2="596" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="610,69 594,61 594,77" fill="var(--accent-muted)"/>
 <rect x="620" y="24" width="240" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="740" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ANSWER</text>
 <text x="740" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">yes</text>
</svg>
<p class="body">After “teach a phrase”, D47 asks for the wording, then for the phrase it should run, then for a yes.</p>
</section>
<section>
<h2><span class="num">2</span> The wording is stored as you said it.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="The wording is stored as said, and the phrase must be one D47 already knows">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">The first reply is stored, never run</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">the second must be a phrase D47 knows</text>
</svg>
<p class="body">The first reply is never run, so saying a command to teach it does not fire it. The second reply must be a phrase D47 knows, or close enough to be offered as a near miss.</p>
</section>
<section>
<h2><span class="num">3</span> A yes stores the pair for this Commander.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="A yes writes the pair under the flying Commander">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">data/phrases.json</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">wheels out runs gear down, for this Commander</text>
</svg>
<p class="body">Any other answer drops it. A wording that is already another phrase is refused, and D47 names the phrase it clashed with.</p>
</section>
<section>
<h2><span class="num">4</span> Next time, the wording is replaced before anything reads it.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="A taught wording is replaced by its phrase before routing">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">“wheels out” becomes “gear down”</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">then the router handles it as if you had said that</text>
</svg>
<p class="body">The turn logs <code>Read "{Said}" as the learned phrase "{Phrase}"</code> and carries on as if you had said “gear down”.</p>
</section>
</div></div>
</details>

## The details

When D47 offers "did you mean…" and you pick one, it asks once whether to remember the wording
you used. A yes writes it down, per Commander; a no or anything else leaves nothing behind. You can
also teach a wording by voice, without waiting for a near miss. This page covers where those
entries are listed, added and forgotten.

```text
"set focus on elite" → "set focus to elite"
```

### Seeing what has been learned

Settings → **Phrases** lists, under YOUR PHRASES, every entry the flying Commander has taught D47,
newest first: the wording or pattern, and the phrase it now runs. Below that is every built-in
phrase, grouped by capability, with one sentence saying what it does. The search box filters both.
Nothing here is shared between Commanders — each has their own.

### Adding a phrase on the page

Under ADD A PHRASE, type a wording or a pattern in the SAY field, pick the phrase it stands for
from the list, and press **Add** or Enter. The new entry goes to the top of YOUR PHRASES. The page
adds through the same tool as the voice, so it refuses the same things: a clash names the wording
that clashed and what it already stands for, with `BUILT-IN PHRASE · <capability>` or
`YOUR PHRASE · <pattern>` under it. An empty field says NOTHING TO ADD, and no phrase picked says
PICK A PHRASE. Editing the field clears the notice.

### Teaching a phrase by voice

Say "teach a phrase" or "new phrase" while you are flying, and D47 asks three things in turn:

> "What do you want to say?" — "wheels out"
>
> "What should it do?" — "gear down"
>
> "'wheels out' will do 'gear down'. Keep it?" — "yes"

The first reply is taken word for word and never acted on, so a wording that is already a command
does not run while you say it. A wording D47 reads as an answer, such as "yes", "no" or "the
second one", is refused and asked for again. The second reply must be a phrase D47 knows; if it is
close to one, D47 asks "did you mean…" and you pick. If it matches nothing, D47 says "No phrase matches that" and
the exchange ends. A yes stores the wording; anything else drops it. "Cancel" at any step ends the
exchange and nothing is learned. If the wording is refused, for example because it is already
another phrase, D47 says why.

A spoken wording is plain words. Patterns with `[a|b]` groups are typed on this page.

### Teaching a pattern

A phrase you teach is a pattern: plain words, plus `[a|b|c]` groups of alternatives. An empty
alternative makes a group optional.

```text
[please|] wheels out
[boost|get clear] and [jump|engage]
```

The first stands for two wordings and the second for four. Groups do not nest, and one pattern
makes at most 100 wordings. A pattern is refused if any wording is already a phrase D47 knows, or
is one of your own phrases standing for something else. The entry on this page is the pattern as
written.

### Forgetting one

Press the **✕** beside the entry, or say it:

> "forget 'set focus on elite'"

Either removes the entry from the page and from `data/phrases.json`, and every wording it produced
no longer matches — it goes back to being offered as a near miss, if it still scores as one, rather
than running silently.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `teach_phrase`

Start teaching D47 a new wording by voice: it asks for the words, then the phrase they run, then
confirms. Never callable by the model.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `add_phrase`

Teach D47 a pattern of words that runs an existing phrase. A pattern is plain words and `[a|b|c]`
groups of alternatives; an empty alternative makes a group optional, as in "[please|] drop the
wheels". Groups do not nest and a pattern makes at most 100 wordings. Never callable by the model.

```json
{"type":"object","properties":{"pattern":{"type":"string","description":"The words to teach, with optional [a|b] groups of alternatives."},"phrase":{"type":"string","description":"The phrase in the phrase book the pattern should run."}},"required":["pattern","phrase"],"additionalProperties":false}
```

#### `forget_learned_phrase`

Forget one pattern the Commander taught D47, as written, with every wording it produced. Never
callable by the model — only the panel, a hotkey or the Commander's own "forget" phrase reach it.

```json
{"type":"object","properties":{"said":{"type":"string","description":"The pattern to forget, exactly as it reads on the Phrases page."}},"required":["said"],"additionalProperties":false}
```

</details>
