TextBox — A text field: 1px orange outline on a transparent ground, white text, grey2 placeholder, an optional channel prefix. The outline turns cyan on focus.

**Source:** ARCHITECT-ELITE-03 §5 Text input. d47 is an Avalonia desktop app; this card is the target dress in HTML and CSS.
Use the `TextBox` React component, or write the markup below by hand. Wrap everything in `<div class="d47-root" data-d47-theme="elite">`.

## Usage

```jsx
<TextBox prefix="TO [D47]:" placeholder="What can you do?" value={text} onChange={setText} onSubmit={send} />
```

Props are in `TextBox.d.ts`. The four-theme reference is `TextBox.themes.html`.

## Markup (hand-written)

```html
<div class="states col">
      <div class="state"><span class="cap">rest, with prefix</span><div class="d47-field" style="width:520px"><span class="d47-field__prefix">TO [D47]:</span><input placeholder="What can you do?"></div></div>
      <div class="state"><span class="cap">focus</span><div class="d47-field is-focus" style="width:520px"><span class="d47-field__prefix">TO [D47]:</span><input value="Where is my carrier"></div></div>
      <div class="state"><span class="cap">disabled</span><div class="d47-field disabled" style="width:520px"><input placeholder="Not available" disabled></div></div>
    </div>
```

## Validation

`error="…"` draws a red outline and a red one-line message under the field; `warning="…"` does the same in amber (`--d47-warn`).
Write the message as one plain sentence saying what to do: "That key was rejected. Paste it again."

```html
<div class="d47-field-wrap"><div class="d47-field invalid"><input value="sk-12"></div><span class="d47-field-message">That key was rejected. Paste it again.</span></div>
```

## Classes

| Name | What it does |
| --- | --- |
| `.d47-field, __prefix` | 44px. Holds an optional __prefix and a bare input. |
| `.invalid / .warning` | Red / amber outline, focused or not. |
| `.d47-field-wrap, .d47-field-message(.warning)` | Field plus a 13px message with a 3px bar. |

## Theme

Set `data-d47-theme` to `elite`, `dark`, `light` or `elite-palette` (My HUD colours) on an ancestor with
`d47-root`. Elite is the default. Glow and scanlines are off in Dark and Light. `is-hover`, `is-pressed`
and `is-focus` draw those states statically.
