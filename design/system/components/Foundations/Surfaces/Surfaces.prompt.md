Surfaces — The window has a 1px line frame so it reads on black, a 44px title bar on bar, and scanlines in Elite only. Section heads sit on a full-width 1px orange rule. No rounded corners. Glow only on the brand diamond and the active tab.

**Source:** DS-UPDATE.md §4. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `Surfaces` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<div className="d47-root" data-d47-theme="elite"><Surfaces version="1.6.3" style={{ height: 720 }}>…</Surfaces></div>
```

Props are in `Surfaces.d.ts`. The four-theme reference is `Surfaces.themes.html`.

## Markup (hand-written)

```html
<div class="d47-window" style="width:100%;height:230px">
      <div class="d47-titlebar"><span class="d47-brand-mark"></span><span class="d47-brand-name">DIRECTIVE 47</span><span class="d47-version">1.6.3</span>
        <div class="d47-window-controls"><span class="d47-window-control">—</span><span class="d47-window-control">□</span><span class="d47-window-control close is-hover">✕</span></div></div>
      <div class="d47-window__body" style="gap:14px">
        <div class="d47-title-block"><div class="d47-title-block__text"><span class="d47-title-block__context">Fleet carrier · BNH-T2F</span><span class="d47-title">Sacred Fire</span></div>
          <div class="d47-title-block__figure"><span class="d47-title-block__figure-label">Carrier balance</span><span class="d47-title-block__figure-value">990,302,661 CR</span></div></div>
        <div class="d47-section-head">Unlock prerequisites<span class="d47-section-head__aside">28 planned items</span></div>
      </div>
      <div class="d47-scanlines"></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-window` | The 1px line frame. Put .d47-scanlines last inside it. |
| `.d47-titlebar` | 44px, bar ground, line2 rule. Holds .d47-brand-mark, .d47-brand-name, .d47-version, .d47-window-controls. |
| `.d47-window__body` | Content padding 20 / 28 / 24. |
| `.d47-title-block` | Orange context over a white 28px title, a figure at the right, on a 1px orange rule. |
| `.d47-section-head` | White 15/600 uppercase on a full-width 1px orange rule; __aside for an orange count. |
| `.d47-scanlines` | The overlay. Off in Dark and Light. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
