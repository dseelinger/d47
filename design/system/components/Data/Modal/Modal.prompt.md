Modal — A 1px orange frame on bar, over a 72% black scrim. White title, orange dek, a key figure at the top right, breakdowns as data blocks, and button tiles in a footer on a line2 rule.

**Source:** DS-UPDATE.md §6 Modal (SPEND). d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Modal` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<Modal open={open} onClose={close} dek="Estimated from published rates" title="What this has cost" figureLabel="Today" figureValue="$0.4208">…</Modal>
```

Props are in `Modal.d.ts`. The four-theme reference is `Modal.themes.html`.

## Markup (hand-written)

```html
<div style="position:relative;width:100%;height:520px"><div class="d47-scrim">
      <div class="d47-modal">
        <div class="d47-modal__head"><div class="d47-title-block__text"><span class="d47-modal__dek">Estimated from published rates</span><span class="d47-modal__title">What this has cost</span></div>
          <div class="d47-title-block__figure"><span class="d47-title-block__figure-label">Today</span><span class="d47-modal__figure-value">$0.4208</span></div></div>
        <div class="d47-modal__body">
          <div class="d47-hint">D47 knows each provider's published rates, not what your account is billed.</div>
          <div class="d47-list"><div class="d47-section-head" style="font-size:14px;border-bottom-color:var(--d47-line)">Now</div>
            <div class="d47-data" style="gap:6px"><div class="d47-data__row"><span class="d47-data__label" style="font-size:13px">This session</span><span class="d47-data__value d47-data__end" style="font-size:18px">$0.1432</span></div>
              <div class="d47-breakdown"><span class="d47-breakdown__name">claude-sonnet-5</span><span class="d47-breakdown__use">5 turns</span><span class="d47-breakdown__cost">$0.1057</span></div>
              <div class="d47-breakdown"><span class="d47-breakdown__name">Edge Neural</span><span class="d47-breakdown__use">749 chars</span><span class="d47-breakdown__cost free">FREE</span></div></div></div>
        </div>
        <div class="d47-modal__foot"><button class="d47-tile-button tall">Close</button><span class="d47-hint">Say: “what has this cost today”</span></div>
      </div></div></div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-scrim, .d47-modal` | Black 72% scrim; 640px modal on bar with a 1px orange frame. |
| `__head, __dek, __title, __figure-value` | Orange dek over a white title, a figure at the right, orange rule. |
| `__body, __foot` | Scrolling body; footer of tiles on a line2 rule. |
| `.d47-breakdown` | Name, mono use, mono cost (.free is grey). |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
