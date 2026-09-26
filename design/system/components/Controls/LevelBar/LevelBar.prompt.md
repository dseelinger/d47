LevelBar — A 0–1 level as 20 clickable segments, with the value in mono beside it. A muted channel draws its level in grey2.

**Source:** DS-UPDATE.md §6 Level bar. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `LevelBar` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<LevelBar value={0.5} onChange={setLevel} muted={false} />
```

Props are in `LevelBar.d.ts`. The four-theme reference is `LevelBar.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state"><span class="cap">level</span><div style="width:420px"><div class="d47-level"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span><span class="d47-level__seg"></span></div><span class="d47-level__value">0.50</span></div></div></div>
      <div class="state"><span class="cap">muted</span><div style="width:420px"><div class="d47-level muted"><div class="d47-level__track"><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span><span class="d47-level__seg on"></span></div><span class="d47-level__value">1.00</span></div></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-level, .muted` | __track of 20 __seg (+ .on), and __value. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
