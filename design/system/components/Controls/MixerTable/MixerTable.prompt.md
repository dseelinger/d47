MixerTable — One row per channel: CHANNEL | LEVEL | MUTE | DUCK | reset. Show — when a channel cannot duck. The reset column is always reserved.

**Source:** DS-UPDATE.md §6 Mixer table. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `MixerTable` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<MixerTable channels={[{ id: 'd47', name: 'D47', level: 1, duck: 0.35 }, { id: 'cues', name: 'Cues', level: 1, duck: null }]} onChange={(id, patch) => …} onReset={id => …} />
```

Props are in `MixerTable.d.ts`. The four-theme reference is `MixerTable.themes.html`.

## Markup (hand-written)

```html
<div style="width:100%">
      <div class="d47-mixer__head"><div>Channel</div><div>Level</div><div style="text-align:center">Mute</div><div>Duck while D47 speaks</div><div></div></div>
      <div class="d47-mixer__row"><div class="d47-mixer__name">D47</div><div class="d47-level"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span></div><span class="d47-level__value">1.00</span></div><div class="d47-mixer__center"><span class="d47-mixer__mute"><span class="d47-check"></span></span></div><div class="d47-level"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span></div><span class="d47-level__value">0.35</span></div><div class="d47-row__reset"></div></div>
      <div class="d47-mixer__row"><div class="d47-mixer__name">Game audio</div><div class="d47-level muted"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span></div><span class="d47-level__value">0.50</span></div><div class="d47-mixer__center"><span class="d47-mixer__mute"><span class="d47-check checked"></span></span></div><div class="d47-level"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span></div><span class="d47-level__value">1.00</span></div><div class="d47-row__reset"><span class="d47-glyph"><span class="d47-glyph__tip">Reset to default</span><span class="d47-glyph__face">↺</span></span></div></div>
      <div class="d47-mixer__row"><div class="d47-mixer__name">Cues</div><div class="d47-level"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span></div><span class="d47-level__value">1.00</span></div><div class="d47-mixer__center"><span class="d47-mixer__mute is-hover"><span class="d47-check"></span></span></div><div class="d47-mixer__none">—</div><div class="d47-row__reset"></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-mixer__head, .d47-mixer__row` | Grid 170 | 1fr | 56 | 1fr | 44, gap 16. |
| `.d47-mixer__mute` | The 44×40 tile around a .d47-check. |
| `.d47-mixer__none` | The — for a channel that cannot duck. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
