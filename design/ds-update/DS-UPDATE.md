# D47 Design System — update pack

Source project: "Design feedback and fixes". Apply this to the existing **D47** design system. **Where it conflicts with the original D47 spec (`uploads/D47-SPEC.md`), this pack wins.**

Read these files from the source project:
- `ELITE-COLOUR-NOTES.md`: what the colours mean, as observed in Elite Dangerous.
- `D47-VS-ELITE.md`: where d47 departed from Elite.
- `D47 Elite v4.dc.html`: the Transcript, Fleet and Engineers screens, plus the SPEND popup.
- `D47 Settings.dc.html`: the Settings pages and the control vocabulary.
- `ARCHITECT-ELITE-03.md`, `ARCHITECT-ELITE-03a.md`, `ARCHITECT-SETTINGS-01.md`: build rules.
- `uploads/*.png`: the Elite reference screenshots.

## 1. Principle
d47 lives *inside* Elite. It borrows Elite's visual language and does not invent its own. Colour communicates **relationship and state**, not decoration.

## 2. Tokens (Elite default theme)
```
--bg #070606        --bar #0F0D0C       --slab #232120 (read-only / disabled ground)
--a  #FF7A1A (accent)  --knock #140800 (text on solid accent)  --brown #6B2F00 (labels inside selected)
--white #EDE9E3     --grey #A09B94      --grey2 #6E6A65
--cyan #33D6E8      --yellow #F5D426    --red #F0343F
--tile  = mix(a 20%, bg)   --tile2 = mix(a 30%, bg)   (hover)
--line  = mix(a 55%, bg)   --line2 = mix(a 28%, bg)
```
**Colour roles:**
- **Orange**: can act, and values.
- **Grey**: unavailable, read-only, or label.
- **White**: names, titles and speech.
- **Cyan**: yours, current, or ready (including the current system and the PTT status).
- **Yellow**: stored or equipped (for example KEY STORED).
- **Red**: destructive, hostile or locked.
- Every other system name is orange.

**Themes** all transform one base palette:
- **Elite default.**
- **Dark:** VS Code–blue base.
- **Light:** no scanlines, no glow.
- **Match my Elite colours:** the player's GraphicsConfiguration matrix applied to every hue.

## 3. Type
- **Saira 500/600** for chrome, headings and tiles. Chrome is uppercase with +0.04–0.12em tracking.
- **Sintony** for body text and speech, in sentence case.
- **JetBrains Mono** for numbers, keys, timestamps and costs.

**Scale:**
- Page title: 28.
- Tab: 15.
- Group head: 15/600.
- Row label: 15.
- Control text: 13–14.
- Meta text: 11–12.

## 4. Surfaces and motifs
- **Controls are flat filled tiles**, not outlines. At rest they use `--tile` with orange text; hover uses `--tile2`. The selected or pressed state is a solid `--a` fill with `--knock` text. Tiles are separated by 2px gaps.
- **No button weight.** SEND is a standard tile. Destructive buttons are red tiles: a red 22% tint, with a solid red fill on hover.
- **Read-only data** uses grey `--slab` blocks with a grey label and a white or orange value.
- **Section heads** sit on a 1px full-width orange rule.
- **Window:** a 1px `--line` frame so the window reads on black, a 44px title bar, and a subtle scanline overlay (Elite theme only).
- **Corners:** no rounded corners anywhere.
- **Glow:** soft, and only on the brand diamond and the active tab.

## 5. Layout rules
- **Glyphs** are drawn at about 32px but have a **44×44 hit area** for VR. All interactive targets are at least 44px.
- **Settings rows** use `grid 240px | minmax(0,1fr) | 44px`, gap 16, minimum height 56, and a 1px `--line2` separator. **Every row has a 3px left border**: orange if the setting is protected, transparent otherwise. The reset column is always reserved, and ↺ appears only when the value is dirty. Content is capped at 900px.
- **One shared left gutter** for tabs, content, input and status rows.
- **Navigation:** a sidebar shows one sub-section per page. There are no accordions, and no expand-all or collapse-all.
- **Group head:** the white name, then a one-line grey description, then a group reset. Don't add per-section HELP tiles.

## 6. Component inventory

| Component | Rule |
|---|---|
| Tab / SubTab | Tab: a tile, with the active tab solid orange. SubTab: text, with the active one in white and a 2px orange underline |
| Tile button | Default and destructive variants only |
| Glyph button | 32px visual, 44px hit area, with a hover tooltip |
| Checkbox tile | An orange square outline with an 8px fill when checked. The whole tile is clickable. Group related checkboxes in a 2- or 4-column grid |
| Segmented | Up to 4 equal-width options (2×2 when labels are long). Status goes on an 11px second line, with stored status in yellow |
| Stepper | ◄ value ►, max width 420, with the `n / N` count inside the value box. For 5–7 options |
| Number stepper | 220px wide, showing the value and its unit |
| Dropdown | A tile with ▼ that opens a framed list. For long lists |
| Level bar | 20 clickable segments, with the value in mono |
| Mixer table | CHANNEL \| LEVEL \| MUTE \| DUCK \| reset. Show `—` when a channel can't duck |
| Key binding | Mono chip, then BIND, then CLEAR |
| API key | Masked grey block with yellow KEY STORED, then REPLACE and VERIFY. The input field appears only after REPLACE |
| Data block | Grey ground, grey label, white or orange value |
| Search field | 340px, with a `--line2` border that turns orange on focus |
| Message (SMS) | CMDR messages align right with a cyan right bar and a tint. D47 messages align left with an orange bar. The header shows the name, then the intent tags, then the time after a fixed 12px gap. The cost line is mono 12px grey2 uppercase |
| Status row | PTT dot and text in cyan. The SESSION cost has a grey label and an orange value. SPEND is a 28px tile |
| Modal (SPEND) | A 1px orange frame, a white title, an orange dek, and breakdowns as data blocks |

## 7. UI kit screens
Transcript, Fleet, Engineers, the SPEND popup, and Settings (Voice input, Its voice, Sounds and levels).

## 8. Content voice
- Plain, second person, calm ("Changes apply as you make them.").
- Chrome is uppercase and prose is sentence case.
- No emoji.
- Commander names are shown in full, as `CMDR JOHN DEPARAGON`.
