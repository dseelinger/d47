Card — A selectable tile for a ship or carrier. Filled, not outlined; selection is a solid orange fill.

**Source:** D47-VS-ELITE.md 8. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Card` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Card context="Panther Clipper MkII" name="Campaigner" meta="Muang · docked" selected={sel} onClick={pick} />
```

Props are in `Card.d.ts`. The four-theme reference is `Card.themes.html`.

## Markup (hand-written)

```html
<div class="states">
      <div class="state"><span class="cap">rest</span><div class="d47-card" style="width:220px"><span class="d47-card__context">Panther Clipper MkII</span><span class="d47-card__name">Campaigner</span><span class="d47-card__meta">Muang · docked</span></div></div>
      <div class="state"><span class="cap">hover</span><div class="d47-card is-hover" style="width:220px"><span class="d47-card__context">Mandalay</span><span class="d47-card__name">Wanderer</span><span class="d47-card__meta">Stored · Giryak</span></div></div>
      <div class="state"><span class="cap">selected</span><div class="d47-card selected" style="width:220px"><span class="d47-card__context">Type-9 Heavy</span><span class="d47-card__name">Hauler</span><span class="d47-card__meta">Stored · Leesti</span></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-card, .selected` | __context, __name, __meta. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
