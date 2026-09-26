TypeScale — Saira 500/600 for chrome, headings and tiles, uppercase with light tracking. Sintony for body text and speech, in sentence case. JetBrains Mono for numbers, keys, timestamps and costs.

**Source:** DS-UPDATE.md §3. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `TypeScale` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<TypeScale />
```

Props are in `TypeScale.d.ts`. The four-theme reference is `TypeScale.themes.html`.

## Markup (hand-written)

```html
<div class="states type">
      <div class="type-row"><span class="type-sample">title 28</span><span class="d47-title">Sacred Fire</span></div>
      <div class="type-row"><span class="type-sample">tab 15</span><span class="d47-tab active">Transcript</span></div>
      <div class="type-row"><span class="type-sample">group 15/600</span><span class="d47-group-head__name">Microphone</span></div>
      <div class="type-row"><span class="type-sample">row label 15</span><span class="d47-row__label">How D47 knows you're talking to it</span></div>
      <div class="type-row"><span class="type-sample">body 16</span><span class="d47-message__body">Functioning within tolerance, Commander.</span></div>
      <div class="type-row"><span class="type-sample">control 13–14</span><span class="d47-chrome" style="font-size:13px;font-weight:600">Add to checklist</span></div>
      <div class="type-row"><span class="type-sample">meta 11–12</span><span class="d47-data__label">Carrier balance</span><span class="d47-mono" style="font-size:12px;color:var(--d47-grey2)">19:42 · $0.1075</span></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-title` | Saira 28/500, uppercase, 0.04em, white. |
| `.d47-chrome` | Saira, uppercase, 0.06em tracking. For any chrome string. |
| `.d47-mono` | JetBrains Mono, for numbers, keys, timestamps and costs. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
