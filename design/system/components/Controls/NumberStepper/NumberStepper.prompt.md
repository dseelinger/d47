NumberStepper — A number with its unit, 220px wide: ◄ value ►, the value in mono.

**Source:** DS-UPDATE.md §6 Number stepper. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `NumberStepper` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<NumberStepper value={500} step={50} min={0} format={v => v + ' MS'} onChange={setMs} />
```

Props are in `NumberStepper.d.ts`. The four-theme reference is `NumberStepper.themes.html`.

## Markup (hand-written)

```html
<div class="states">
      <div class="state"><span class="cap">milliseconds</span><div class="d47-number-stepper"><span class="d47-stepper__arrow">◄</span><span class="d47-stepper__value">500 MS</span><span class="d47-stepper__arrow">►</span></div></div>
      <div class="state"><span class="cap">price, arrow pressed</span><div class="d47-number-stepper"><span class="d47-stepper__arrow is-pressed">◄</span><span class="d47-stepper__value">$0.05</span><span class="d47-stepper__arrow">►</span></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-number-stepper` | 220px. Uses the .d47-stepper__arrow and __value parts. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
