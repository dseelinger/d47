TileButton — Every button is a flat tile: default or destructive, nothing else. No button weight: SEND is a default tile. At rest tile with orange text, hover tile2, pressed or selected solid orange with knock text. Destructive is a 22% red tint that fills solid red on hover.

**Source:** DS-UPDATE.md §4, §6 Tile button. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `TileButton` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<TileButton onClick={add}>Add to checklist</TileButton>
<TileButton variant="destructive" size="tall">Forget them all</TileButton>
```

Props are in `TileButton.d.ts`. The four-theme reference is `TileButton.themes.html`.

## Markup (hand-written)

```html
<div class="states">
      <div class="state"><span class="cap">rest</span><button class="d47-tile-button">Add to checklist</button></div>
      <div class="state"><span class="cap">hover</span><button class="d47-tile-button is-hover">Add to checklist</button></div>
      <div class="state"><span class="cap">pressed</span><button class="d47-tile-button is-pressed">Add to checklist</button></div>
      <div class="state"><span class="cap">disabled</span><button class="d47-tile-button" disabled>Add to checklist</button></div>
      <div class="state"><span class="cap">destructive</span><button class="d47-tile-button tall destructive">Forget them all</button></div>
      <div class="state"><span class="cap">destructive hover</span><button class="d47-tile-button tall destructive is-hover">Forget them all</button></div>
      <div class="state"><span class="cap">full (SEND)</span><button class="d47-tile-button full" style="width:120px">Send</button></div>
      <div class="state"><span class="cap">compact (SPEND)</span><button class="d47-tile-button compact">Spend</button></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-tile-button` | The default: 36px, tile ground, orange Saira 13/600 uppercase. |
| `.destructive` | Red text on a 22% red tint; solid red with white text on hover. |
| `.tall / .full / .compact` | 40px in settings rows, 44px beside a 44px field, 28px in the status row. |
| `.selected / .is-pressed` | Solid orange, knock text. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
