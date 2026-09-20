# Building with d47

d47 is a Windows desktop application written in Avalonia. This design system is its dress —
colour roles, a type scale and the control themes — reproduced in HTML and CSS. **There are no
React components to import.** `window.D47` carries only theme metadata and a `setTheme` helper.
Build with the classes and tokens below.

## Wrapping

Two things must be on an ancestor or nothing is styled:

```jsx
<div className="d47-root" data-d47-theme="elite">
  {/* everything else */}
</div>
```

`d47-root` sets the background, ink, the Saira face and the 1.214 line height the controls are
measured against. `data-d47-theme` picks the palette: `elite` (amber on black, the default and the
one that matches the cockpit), `dark`, `light` or `guardian`. Set it on the element you want
repainted — a whole page, or one panel.

Nothing else is needed. `styles.css` carries the fonts, the tokens and the kit.

## The idiom: classes and tokens

Style with the kit's classes where one exists, and with `var(--d47-*)` tokens everywhere else.
Never invent a class name — a name that is not below does nothing.

| Class | What it dresses |
| --- | --- |
| `d47-button`, `+ primary` | A button. Secondary by default; `primary` is solid Accent. |
| `d47-toggle-switch`, `+ checked` | A square switch. Needs one `d47-toggle-switch__knob` child. |
| `d47-segment`, `+ checked` | Two to four adjacent choices in a row. |
| `d47-list-box-item`, `+ selected` | A list row, with `__bar` and `__content` children. |
| `d47-text-box` | A text field. |
| `d47-numeric-up-down` | A number field, with `__text` and a `d47-number-spinner`. |
| `d47-number-spinner`, `+ left` | The stepper pair, with `__increase` and `__decrease`. |
| `d47-card`, `+ selected` | A HUD card. |
| `d47-current-row` | The current row, with `__bar` and `__content`. |
| `d47-chamfered-border`, `+ skew` | Cut corners or a sheared tab, with a `__fill` child. |
| `d47-title`, `+ sentence` | A title: the label face, upper case, tracked. |
| `d47-pane` | The transcript pane's tint and border. |
| `d47-scanlines` | The tiled overlay. Nothing shows in Light. |

Tokens are named after the resource key they come from: `D47.FillHigh` is `--d47-fill-high`.
Colour roles: `--d47-background`, `--d47-surface`, `--d47-surface-alt`, `--d47-border`,
`--d47-text`, `--d47-text-muted`, `--d47-accent`, `--d47-accent-muted`, `--d47-danger`,
`--d47-info`. Derived from Accent: `--d47-rule` (a hairline), `--d47-fill-low`, `--d47-fill-high`,
`--d47-card-fill`, `--d47-card-fill-selected`, `--d47-row-fill`, `--d47-tag-border`,
`--d47-tab-strip-rule`, `--d47-tag-ink`, `--d47-accent-border`, `--d47-accent-ink`,
`--d47-pane-fill`, `--d47-pane-border`. The Commander's side of a conversation uses
`--d47-info-fill`, `--d47-info-border` and `--d47-info-ink`.

Type: `--d47-type-heading` 20, `--d47-type-subheading` 16, `--d47-type-body` 14,
`--d47-type-secondary` 13, `--d47-type-small` 12. Faces: `--d47-font-body` (Saira, for prose) and
`--d47-font-label` (Saira Semi Condensed, for labels, tabs and data).

**Corners are square.** The dress carries no rounded shape — do not add `border-radius`.

**Glow is a dark-theme thing.** `--d47-bloom-fill`, `--d47-bloom-rule` and `--d47-bloom-edge` (and
their `-headset` variants) resolve to `none` in Light, as does `--d47-scanlines`. Apply them
unconditionally; Light turns them off by itself.

## Where the truth is

`_ds/<folder>/styles.css` and its imports — `tokens/palette.css` has every role written out per
palette, `_ds_bundle.css` is the kit itself. Each component's `.prompt.md` carries its markup.

## A build

```jsx
<div className="d47-root" data-d47-theme="elite" style={{ padding: 20 }}>
  <h2 className="d47-title" style={{ fontSize: 'var(--d47-type-heading)' }}>Route</h2>

  <div className="d47-card" style={{ marginTop: 12 }}>
    <div style={{ color: 'var(--d47-text-muted)', fontSize: 'var(--d47-type-secondary)' }}>
      Sol to Colonia
    </div>
    <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
      <button className="d47-button primary">Set course</button>
      <button className="d47-button">Copy</button>
    </div>
  </div>
</div>
```
