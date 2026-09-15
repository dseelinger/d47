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

### Forgetting one

Press **Forget** beside the entry, or say it:

> "forget 'set focus on elite'"

Either removes the entry from the page and from `data/phrases.json`, and the wording it forgets no
longer matches — it goes back to being offered as a near miss, if it still scores as one, rather
than running silently.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `forget_learned_phrase`

Forget one utterance the Commander taught D47 to treat as a command, by the wording they said.
Never callable by the model — only the panel, a hotkey or the Commander's own "forget" phrase
reach it.

```json
{"type":"object","properties":{"said":{"type":"string","description":"The utterance to forget, exactly as it reads on the learned-phrases page."}},"required":["said"],"additionalProperties":false}
```

</details>
