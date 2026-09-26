ApiKey — A masked grey block with yellow KEY STORED, then REPLACE and VERIFY. The input field appears only after REPLACE, and its outline turns cyan on focus.

**Source:** DS-UPDATE.md §6 API key. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `ApiKey` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<ApiKey last4="a91C" onSave={saveKey} onVerify={verify} />
```

Props are in `ApiKey.d.ts`. The four-theme reference is `ApiKey.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state"><span class="cap">stored</span><div class="d47-apikey"><div class="d47-apikey__stored"><span class="d47-apikey__mask">••••••••a91C</span><span class="d47-apikey__status">Key stored</span></div><button class="d47-tile-button tall">Replace</button><button class="d47-tile-button tall">Verify</button></div></div>
      <div class="state"><span class="cap">after REPLACE</span><div class="d47-apikey" style="width:560px"><input class="d47-apikey__input is-focus" placeholder="Paste the new key"><button class="d47-tile-button tall">Save</button><button class="d47-tile-button tall">Cancel</button></div></div>
    </div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-apikey__stored, __mask, __status` | The grey block; __status is yellow, .missing grey. |
| `.d47-apikey__input` | Shown only after REPLACE. Orange outline, cyan on focus. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
