StatusRow — Above the message input. The PTT dot and text in cyan; the SESSION cost with a grey label and an orange value; SPEND a 28px tile at the far right.

**Source:** DS-UPDATE.md §6 Status row. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `StatusRow` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<StatusRow status="PTT ready" cost="$0.1432" onSpend={openSpend} />
```

Props are in `StatusRow.d.ts`. The four-theme reference is `StatusRow.themes.html`.

## Markup (hand-written)

```html
<div style="width:100%;display:flex;flex-direction:column;gap:8px">
      <div class="d47-status"><span class="d47-status__dot"></span><span class="d47-status__text">PTT ready</span><span class="d47-status__session">Session <span class="d47-status__cost">$0.1432</span></span><button class="d47-tile-button compact">Spend</button></div>
      <div class="d47-composer"><div class="d47-field"><span class="d47-field__prefix">TO [D47]:</span><input placeholder="What can you do?"></div><button class="d47-tile-button full">Send</button></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-status` | 28px. __dot, __text, __session holding __cost, then a .d47-tile-button.compact. |
| `.d47-composer` | The input row: a .d47-field and a 120px SEND tile. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
