CheckboxTile — Every two-state control. An orange square outline with an 8px fill when checked, inside a tile the whole of which is clickable. Group related checkboxes in a 2- or 4-column grid rather than one per row.

**Source:** DS-UPDATE.md §6 Checkbox tile. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `CheckboxTile` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<div className="d47-checkbox-grid"><CheckboxTile checked={aec} onChange={setAec}>Cancel D47's own voice out of the mic</CheckboxTile></div>
```

Props are in `CheckboxTile.d.ts`. The four-theme reference is `CheckboxTile.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state wide"><span class="cap">grid of 2</span><div class="d47-checkbox-grid" style="width:100%"><div class="d47-checkbox checked"><span class="d47-checkbox__box"></span>Cancel D47's own voice out of the mic</div><div class="d47-checkbox is-hover"><span class="d47-checkbox__box"></span>Take the room out of what D47 hears</div></div></div>
      <div class="state wide"><span class="cap">grid of 4</span><div class="d47-checkbox-grid cols-4" style="width:100%"><div class="d47-checkbox"><span class="d47-checkbox__box"></span>Cylon</div><div class="d47-checkbox"><span class="d47-checkbox__box"></span>Pitch down</div><div class="d47-checkbox checked"><span class="d47-checkbox__box"></span>Chorus</div><div class="d47-checkbox disabled"><span class="d47-checkbox__box"></span>Reverb</div></div></div>
      <div class="state"><span class="cap">chrome label (filters)</span><div style="display:flex;gap:2px"><div class="d47-checkbox caps checked"><span class="d47-checkbox__box"></span>Hide the Colonia eight</div><div class="d47-checkbox caps"><span class="d47-checkbox__box"></span>Show every setting</div></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-checkbox, .checked` | The tile, 44px, Sintony 15 white label. The box is .d47-checkbox__box. |
| `.caps` | A 36px chrome-labelled checkbox for filters in a toolbar. |
| `.d47-checkbox-grid, .cols-4` | Two columns by default, four with .cols-4, 2px gaps. |
| `.d47-check` | The bare box, for a table cell such as the mixer MUTE column. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
