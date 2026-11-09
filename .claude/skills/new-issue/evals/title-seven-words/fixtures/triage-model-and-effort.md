## Model and effort

An issue's own size wins within the rules below (see **The size each issue names**); this table
sizes the issues that state none. Emit the exact tokens, so the line can be pasted: `opus` / `sonnet` / `haiku`,
`low` / `medium` / `high` / `xhigh` / `max`, and `opus` or nothing for the advisor.

| Model | Effort | Advisor | When |
| --- | --- | --- | --- |
| `haiku` | `medium` | | A generated table or a string. Almost never — the generators are the edit point, not the table. |
| `sonnet` | `medium` | | The default. The issue names the cause and the fix follows from it. |
| `sonnet` | `medium` | `opus` | The cause is named but the fix is a judgement: placement, layout, which of several sites changes. |
| `opus` | `high` | | The fix crosses a project boundary, moves a Core seam, touches `TickLoop` or its subscribers, or the issue names a symptom without a cause. |

The advisor is a stronger model the session consults at decision points — before choosing the fix
and before committing — while the main model does the edits, builds and tests. It does not raise
the main model's effort, and it only helps a session that knows when to ask, so it goes on the one
row where a single judgement sits among routine work. Do not give `haiku` an advisor, and do not
pair `sonnet` with `high`: work that needs more than `sonnet medium` with an `opus` advisor goes to
`opus high`.

`low` only for a change whose diff you could write from the title. `xhigh` or `max` where the issue
is an architecture question filed as a bug — flag those as candidates for the `needs-architect` label
