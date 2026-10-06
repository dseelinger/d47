---
title: Learned phrases
group: Voice
nav_order: 125
---

## The details

When D47 offers "did you mean…" and you pick one, it asks once whether to remember the wording
you used. A yes writes it down, per Commander; a no or anything else leaves nothing behind. This
page is where those entries live, and where they stop.

```text
"set focus on elite" → "set focus to elite"
```

### Seeing what has been learned

Settings → **Learned phrases** lists every entry the flying Commander has taught D47, newest
first: the wording said, and the phrase it now runs. Nothing here is shared between Commanders —
each has their own.

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
