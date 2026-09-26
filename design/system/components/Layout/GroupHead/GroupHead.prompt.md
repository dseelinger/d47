GroupHead — The white group name, a one-line grey description, then a group reset in a reserved 44px slot, all on a 1px orange rule. No per-section HELP tiles. Above the groups, the page head: orange breadcrumb, 28px white title, and the legend for protected rows.

**Source:** DS-UPDATE.md §5 Group head. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `GroupHead` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<GroupHead name="Microphone" description="The input device, and how D47 knows you're talking to it." dirty onReset={reset} />
```

Props are in `GroupHead.d.ts`. The four-theme reference is `GroupHead.themes.html`.

## Markup (hand-written)

```html
<div class="d47-settings" style="width:100%;gap:20px">
      <div class="d47-page-head"><span class="d47-crumb">Voice and hearing ›</span><span class="d47-title">Voice input</span><span class="d47-page-head__legend">Protected. D47 won't change these just because you ask it to.</span></div>
      <div class="d47-group-head"><span class="d47-group-head__name">Microphone</span><span class="d47-group-head__desc">The input device, and how D47 knows you're talking to it.</span><span class="d47-group-head__reset"><span class="d47-glyph"><span class="d47-glyph__tip">Reset to default</span><span class="d47-glyph__face">↺</span></span></span></div>
      <div class="d47-group-head"><span class="d47-group-head__name">Corrections</span><span class="d47-group-head__desc">Names D47 has learned to hear correctly.</span><span class="d47-group-head__reset"></span></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-page-head, __legend` | Holds .d47-crumb and .d47-title; the legend draws its own 3px orange bar. |
| `.d47-group-head, __name, __desc, __reset` | The reset slot is always there, empty when nothing is dirty. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
