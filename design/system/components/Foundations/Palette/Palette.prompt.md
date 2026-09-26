Palette — One base palette per theme. Colour carries relationship and state, never decoration. Blue and cyan are two meanings: cyan is yours, current or ready; blue is confirmed or met.

**Source:** DS-UPDATE.md §2. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Palette` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Palette tokens={['a', 'cyan', 'blue', 'yellow', 'red']} />
```

Props are in `Palette.d.ts`. The four-theme reference is `Palette.themes.html`.

## Markup (hand-written)

```html
<div class="swatches" data-palette></div>
```

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
