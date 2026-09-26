KeyBinding — A grey mono chip showing the bound key, then BIND, then CLEAR.

**Source:** DS-UPDATE.md §6 Key binding. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `KeyBinding` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<KeyBinding binding="BUTTON 11" onBind={bind} onClear={clear} />
```

Props are in `KeyBinding.d.ts`. The four-theme reference is `KeyBinding.themes.html`.

## Markup (hand-written)

```html
<div class="states">
      <div class="state"><span class="cap">bound</span><div class="d47-keybind"><span class="d47-keybind__chip">BUTTON 11</span><button class="d47-tile-button tall">Bind</button><button class="d47-tile-button tall">Clear</button></div></div>
      <div class="state"><span class="cap">unbound</span><div class="d47-keybind"><span class="d47-keybind__chip unbound">UNBOUND</span><button class="d47-tile-button tall is-hover">Bind</button><button class="d47-tile-button tall">Clear</button></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-keybind, .d47-keybind__chip, .unbound` | The chip, then two .d47-tile-button.tall. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
