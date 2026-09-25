# ARCHITECT-ELITE-03a — Transcript remediation

Follow-up to ARCHITECT-ELITE-03. **Only styling and layout changes on the Transcript screen.** Don't change behaviour and don't add features. The visual reference is `D47 Elite v4.dc.html` (Transcript tab). The colour rules are in `ELITE-COLOUR-NOTES.md`.

## Fixes

1. **SPEND placement.** Take SPEND off its own row. Put it at the far right of the PTT status row, as a 28px-high flat tile (`--tile` background, orange Saira 13px/600 text).
2. **Session cost.** In the PTT row, immediately left of SPEND, add `SESSION` in grey Saira 13px/500, followed by the session total in orange (for example `$0.1432`). Use the existing session cost value; don't add any new calculation.
3. **PTT status colour.** Make the `PTT READY` text cyan to match the dot, because it's a current-status indicator.
4. **Search box.** Set a fixed width of 340px. Give it a dim `--line` border at rest and an orange border only on focus. It must not visually outweigh the message input.
5. **Timestamp position.** In each message header row, show the name, then the intent/delivery tags, then the timestamp after a fixed 12px gap. Don't use a flex spacer or stretch, and don't pin the timestamp to the end of the text width. Apply this to both D47 and CMDR messages.
6. **Shared left gutter.** The input row and PTT row currently start at about 66px. Align them to the same left edge as the messages, the divider and the sub-tabs (about 44px).
7. **Per-message cost line.** Move the "Answered via…" line inside the D47 message it belongs to, below the body text. Style it as JetBrains Mono 12px, `--grey2`, uppercase, separated by `·`, for example `ANSWERED VIA <MODEL> · EFFORT MEDIUM · $0.0690`. Remove the free-standing row above the divider.
8. **Commander name.** Show `CMDR <name>` using the commander name from the journal (for example `CMDR JOHN DEPARAGON`), not just `CMDR`.
9. **Model name.** "Answered via Model" is showing the literal word "Model". Bind it to the real model/provider name that is already available. If it isn't available, report that back instead of inventing a value.

## Don't touch
The tabs, sub-tabs, COPY tile, SEND tile, title bar, SMS alignment (CMDR on the right with a cyan bar and tint, D47 on the left with an orange bar), and the colour roles.

## Verify
Screenshot the Transcript screen with at least one CMDR turn and two D47 turns, one of which has intent tags. Compare it against the mockup and list any remaining differences.
