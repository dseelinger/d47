DataBlock — Read-only data on a neutral grey slab: grey label, orange value, or white when the value is a name and cyan when it means here. Capacity gauges are yellow. The prerequisite ladder reads blue met, orange in progress, white unknown, grey not met.

**Source:** DS-UPDATE.md §4, §6 Data block. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `DataBlock` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<div className="d47-data-grid"><DataBlock label="Current system" value="GIRYAK" tone="cyan" /><DataBlock label="Capacity" value="1,110 / 25,000 T" tone="yellow" small gauge={0.044} gaugeKind="capacity" /></div>
```

Props are in `DataBlock.d.ts`. The four-theme reference is `DataBlock.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state wide"><span class="cap">stat grid</span><div class="d47-data-grid" style="width:100%">
        <div class="d47-data"><div class="d47-data__row"><span class="d47-data__label">Current system</span></div><span class="d47-data__value cyan">GIRYAK</span></div>
        <div class="d47-data"><span class="d47-data__label">Tritium in tank</span><span class="d47-data__value">967 T</span></div>
        <div class="d47-data"><span class="d47-data__label">Where you stand</span><span class="d47-data__value white">Known · no invitation</span></div>
        <div class="d47-data" style="gap:5px"><div class="d47-data__row"><span class="d47-data__label">Capacity</span><span class="d47-data__value yellow small d47-data__end">1,110 / 25,000 T</span></div><div class="d47-gauge capacity"><div class="d47-gauge__fill" style="width:4.4%"></div></div></div>
        <div class="d47-data span-2"><span class="d47-data__label">Services</span><span class="d47-data__value">CAPTAIN · REFUEL · REPAIR · REARM</span></div>
      </div></div>
      <div class="state wide"><span class="cap">prerequisite ladder</span><div class="d47-ladder" style="width:100%">
        <div class="d47-ladder__row"><span class="d47-ladder__state met">✓ Met</span><span class="d47-ladder__text">Grade 3 with Hera Tani.</span></div>
        <div class="d47-ladder__row"><span class="d47-ladder__state">In progress</span><span class="d47-ladder__text">Gain combat rank Competent or higher.</span><span></span><div class="d47-gauge"><div class="d47-gauge__fill" style="width:34%"></div></div></div>
        <div class="d47-ladder__row"><span class="d47-ladder__state unknown">? Unknown</span><span class="d47-ladder__text">Provide 50 units of Fujin Tea.</span></div>
        <div class="d47-ladder__row"><span class="d47-ladder__state unmet">Not met</span><span class="d47-ladder__text">Refer a friend.</span></div>
      </div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-data-grid, .span-2` | Three columns, 2px gaps. |
| `.d47-data, __label, __value` | Value modifiers .white (a name), .cyan (here), .yellow (stored), .small. |
| `.d47-data.prose` | A block of grey read-only prose. |
| `.d47-gauge, .capacity, .over` | 6px track: orange progress, yellow capacity, red over limit. |
| `.d47-ladder__state .met / .unknown / .unmet` | Blue, white, grey; orange when in progress. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
