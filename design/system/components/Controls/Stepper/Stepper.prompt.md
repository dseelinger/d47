Stepper — For 5–7 fixed options. ◄ value ► at most 420px wide, with the n / N count inside the value box on the right.

**Source:** DS-UPDATE.md §6 Stepper. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Stepper` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Stepper options={['Edge Neural · free', 'ElevenLabs · paid', 'OpenAI · paid']} value={tts} onChange={setTts} />
```

Props are in `Stepper.d.ts`. The four-theme reference is `Stepper.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state"><span class="cap">rest</span><div class="d47-stepper" style="width:420px"><span class="d47-stepper__arrow">◄</span><div class="d47-stepper__value"><span class="d47-stepper__text">ElevenLabs · paid</span><span class="d47-stepper__count">3 / 6</span></div><span class="d47-stepper__arrow">►</span></div></div>
      <div class="state"><span class="cap">arrow hover</span><div class="d47-stepper" style="width:420px"><span class="d47-stepper__arrow">◄</span><div class="d47-stepper__value"><span class="d47-stepper__text">ElevenLabs · paid</span><span class="d47-stepper__count">3 / 6</span></div><span class="d47-stepper__arrow is-hover">►</span></div></div>
      <div class="state"><span class="cap">inherited value</span><div class="d47-stepper" style="width:420px"><span class="d47-stepper__arrow">◄</span><div class="d47-stepper__value"><span class="d47-stepper__text inherited">Same as the ship</span><span class="d47-stepper__count">1 / 7</span></div><span class="d47-stepper__arrow">►</span></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-stepper` | Max 420px. __arrow, __value holding __text and __count. |
| `.d47-stepper__text.inherited` | Grey, for a value that follows another setting. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
