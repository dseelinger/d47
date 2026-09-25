# ARCHITECT-SETTINGS-01 — Settings layout and controls

Visual reference: `D47 Settings.dc.html` (use the sidebar to switch between Voice input, Its voice, and Sounds and levels). Colour rules: `ELITE-COLOUR-NOTES.md`.
**This brief changes layout and controls only.** Every setting keeps its existing behaviour. Don't add any settings and don't remove any.

## Structure
1. **One sub-section per page.** Clicking a sidebar item replaces the content with that sub-section alone. Remove the accordions, EXPAND ALL and COLLAPSE ALL. The sidebar is now the only navigation.
2. **Remove the per-section HELP tiles.** Each group heading gets a one-line grey description next to it. The HELP tile in the top bar stays.
3. **Page head:** a small orange breadcrumb (`VOICE AND HEARING ›`), then a 28px white title, then the legend line for protected rows.
4. **Group head:** the name in Saira 15/600 white, then the grey description, then a group reset glyph in a 44px slot on the right. All of this sits on a 1px orange rule.
5. **SHOW EVERY SETTING** becomes a checkbox tile in the sub-tab row, next to a 340px search field with a dim border.

## The row grid (the alignment fix)
- Every row uses `grid-template-columns: 240px minmax(0,1fr) 44px`, gap 16, a minimum height of 56, and a 1px `--line2` separator.
- **Every row has a 3px left border.** Protected rows make it orange; all other rows make it transparent. That keeps every label on the same x position.
- **The 44px reset column is always reserved**, even when it's empty. The ↺ glyph shows only when the value differs from its default.
- Content is capped at 900px wide, so the reset column stays close to the control it resets.

## Which control to use
| Setting type | Control |
|---|---|
| Up to 4 options | Segmented tiles, all equal width (2×2 for long labels). The selected tile is orange-filled. Status goes on a second 11px line (e.g. `PAID · KEY STORED` in yellow, `NEEDS KEY` in grey) instead of in brackets inside the label |
| 5–7 short options | Elite ◄ value ► stepper, max 420px wide. The `n / N` count sits **inside** the value box on the right, not underneath it |
| Long lists (devices, voices) | Dropdown tile with ▼ that opens a list below it |
| Number with a unit | Compact stepper, 220px, showing the value and unit (`500 MS`, `$0.05`) |
| Boolean | Checkbox **tile** that toggles when you click anywhere on it. Put related booleans in a grid (2 across, or 4 across for Guardian voice) instead of one per row |
| 0–1 level | A 20-segment bar you click to set, with the value in mono beside it |
| Key binding | Grey mono chip, then BIND, then CLEAR |
| API key | Grey block showing the masked key and yellow `KEY STORED`, then REPLACE (and VERIFY where it exists). The input field only appears after you press REPLACE |
| Read-only value or status | Grey data block (`--slab`), like the Fleet screen |
| Destructive action | Red tile (FORGET THEM ALL, FORGET AND PAIR AGAIN) |

## Specific screens
- **API key row** appears only under the selected provider it belongs to.
- **Levels** become one mixer table with the columns CHANNEL | LEVEL | MUTE | DUCK | reset, one row per channel. Right now the build repeats Level/Mute/Duck with **no channel names**, so nobody can tell which is which. Use the real channel names from the C#; the mockup's names are placeholders. If a channel can't duck, show `—` in that column.
- **Thinking bed sound** appears only when Thinking bed is ticked.
- **Wake word** had a heading and no rows in the build. If it has settings, give them the same row grid. If it doesn't, hide the group.
- **What it costs:** show the spend figures as data blocks, and show "What the voice provider receives" as a SHOW tile.

## Verify
For each of the three sub-pages, take a screenshot at full width and at 924px. Check that labels, controls and reset glyphs all line up in straight columns, and that nothing wraps into another element or gets cut off.
