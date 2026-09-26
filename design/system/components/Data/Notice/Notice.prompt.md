Notice — An error or warning message: a 3px bar in the level colour on a 12% tint of it, a chrome label, white prose, optional tiles at the right. Use it wherever something failed (red) or needs attention (amber, `--d47-warn`).

**Source:** Intentional addition (not in DS-UPDATE.md). Built from existing rules: the 3px left bar of protected rows, the red tint of destructive tiles.

## Usage

```jsx
<Notice label="Speech engine offline" detail="HTTP 401 · GROQ" actions={<TileButton size="tall">Retry</TileButton>}>
  Groq rejected the key. D47 is using Whisper on this computer until you replace it.
</Notice>
<Notice level="warning" label="Journal not found">D47 can't see the game yet. Start Elite, or point D47 at the journal folder.</Notice>
```

Props are in `Notice.d.ts`.

## Markup (hand-written)

```html
<div class="d47-notice" role="alert"><div class="d47-notice__main"><span class="d47-notice__label">Speech engine offline</span><span class="d47-notice__text">Groq rejected the key.</span><span class="d47-notice__detail">HTTP 401 · GROQ</span></div><div class="d47-notice__actions"><button class="d47-tile-button tall">Retry</button></div></div>
```

## Rules

- Red means it failed or is blocked. Amber means caution. Never yellow (stored) or orange (can act) for either.
- Label: uppercase chrome, naming what failed. Text: sentence case, what happened then what to do. No "Oops", no exclamation marks, no emoji.
- One notice per problem, at the top of the page or group it belongs to. Don't stack more than two.
- Field-level problems go on the field itself: `<TextBox error="…" />`.
- In the transcript, a failed turn is a `.d47-message.error` (red bar) with a Notice inline in it.

## Classes

| Name | What it does |
| --- | --- |
| `.d47-notice, .warning` | Red by default; amber with .warning. |
| `__main: __label, __text, __detail` | Chrome label, Sintony 14 white, mono 11 grey. |
| `__actions` | Tile buttons, 2px apart. |
| `.inline` | Tighter padding for use inside rows. |
| `.d47-message.error` | Red bar on a D47 message. |
