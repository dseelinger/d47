SettingsRow — Grid 240px | minmax(0,1fr) | 44px, gap 16, minimum height 56, a 1px line2 separator. Every row has a 3px left border, orange when the setting is protected, so every label sits at the same x. The reset column is always reserved; ↺ shows only when the value is dirty. Content is capped at 900px.

**Source:** DS-UPDATE.md §5 Settings rows. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `SettingsRow` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<SettingsRow label="Push-to-talk" protected dirty onReset={reset}><KeyBinding binding="BUTTON 11" /></SettingsRow>
```

Props are in `SettingsRow.d.ts`. The four-theme reference is `SettingsRow.themes.html`.

## Markup (hand-written)

```html
<div class="d47-settings" style="width:100%"><div class="d47-group">
      <div class="d47-group-head"><span class="d47-group-head__name">Microphone</span><span class="d47-group-head__desc">The input device, and how D47 knows you're talking to it.</span><span class="d47-group-head__reset"><span class="d47-glyph"><span class="d47-glyph__tip">Reset to default</span><span class="d47-glyph__face">↺</span></span></span></div>
      <div class="d47-row"><span class="d47-row__label">Microphone</span><div class="d47-row__control"><div class="d47-dropdown"><button class="d47-dropdown__button"><span class="d47-dropdown__value">System default · Logi 4K Stream Edition</span><span class="d47-dropdown__caret">▼</span></button></div></div><span class="d47-row__reset"></span></div>
      <div class="d47-row protected"><span class="d47-row__label">Push-to-talk</span><div class="d47-row__control"><div class="d47-keybind"><span class="d47-keybind__chip">BUTTON 11</span><button class="d47-tile-button tall">Bind</button><button class="d47-tile-button tall">Clear</button></div></div><span class="d47-row__reset"><span class="d47-glyph"><span class="d47-glyph__tip">Reset to default</span><span class="d47-glyph__face">↺</span></span></span></div>
      <div class="d47-row"><span class="d47-row__label">Capture before the key</span><div class="d47-row__control"><div class="d47-number-stepper"><span class="d47-stepper__arrow">◄</span><span class="d47-stepper__value">500 MS</span><span class="d47-stepper__arrow">►</span></div></div><span class="d47-row__reset"></span></div>
      <div class="d47-inset"><div class="d47-checkbox-grid"><div class="d47-checkbox checked"><span class="d47-checkbox__box"></span>Cancel D47's own voice out of the mic</div><div class="d47-checkbox checked"><span class="d47-checkbox__box"></span>Take the room out of what D47 hears</div></div></div>
    </div></div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-settings` | The 900px column of groups, 28px apart. |
| `.d47-row, .protected` | The grid; __label, __control, __reset (always present). |
| `.d47-inset` | A checkbox grid or data grid under a group, inset to the label edge. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
