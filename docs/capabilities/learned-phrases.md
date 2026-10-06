---
title: Learned phrases
group: Voice
nav_order: 125
---

## The details

When D47 offers "did you mean…" and you pick one, it asks once whether to remember the wording
you used. A yes writes it down, per Commander; a no or anything else leaves nothing behind. You can
also teach a wording by voice, without waiting for a near miss. This page is where those entries
live, and where they stop.

```text
"set focus on elite" → "set focus to elite"
```

### Seeing what has been learned

Settings → **Learned phrases** lists every entry the flying Commander has taught D47, newest
first: the wording said, and the phrase it now runs. Nothing here is shared between Commanders —
each has their own.

### Teaching a phrase by voice

Say "teach a phrase" or "new phrase" while you are flying, and D47 asks three things in turn:

> "What do you want to say?" — "wheels out"
>
> "What should it do?" — "drop the wheels"
>
> "'wheels out' will do 'drop the wheels'. Keep it?" — "yes"

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
[please|] drop the wheels
[boost|get clear] and [jump|engage]
```

The first stands for two wordings and the second for four. Groups do not nest, and one pattern
makes at most 100 wordings. A pattern is refused if any wording is already a phrase D47 knows, or
is one of your own phrases standing for something else. The entry on this page is the pattern as
written.

### Forgetting one

Press **Forget** beside the entry, or say it:

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
{"type":"object","properties":{"said":{"type":"string","description":"The pattern to forget, exactly as it reads on the learned-phrases page."}},"required":["said"],"additionalProperties":false}
```

</details>
