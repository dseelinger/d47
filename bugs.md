# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## 1. Three log-level rows control nothing

**Seen:** not reported by a Commander — found while building the Technical page bridge
(change request 6), which needed to know which namespaces the speech loop logs under.

**Verified in the code.** `Subsystems.SourcePrefixes` maps each subsystem to the namespace its
loggers live under, and `LoggingSetup.Create` turns each pair into a Serilog
`MinimumLevel.Override`. Three of the eight prefixes name namespaces that do not exist anywhere
in the repository:

| Subsystem | Mapped to | Namespaces that actually exist |
|---|---|---|
| `Voice` | `D47.Voice` | `D47.App.Voice`, and the loop also logs from `D47.Core.Audio` and `D47.Core.Listening` |
| `Input` | `D47.Input` | `D47.App.Input`, `D47.Core.Input` |
| `Llm` | `D47.Core.Llm` | `D47.Llm` |

An override is matched against the start of `SourceContext`, so a prefix matching no logger is
never applied. The Voice, Input and LLM level rows therefore do nothing at all: those subsystems
log at whatever the default switch says, and changing their row is silent in both directions.
The other five are correct and do work.

**Not a hypothesis.** The absence of the namespaces is checkable without running anything, and
the matching rule is Serilog's documented behaviour. What has *not* been reproduced is the
Commander-visible symptom — turning Voice down to Warning and observing Information lines still
being written.

**It is not just three strings.** Two of the three subsystems span more than one namespace, so
the repair is either a prefix *list* per subsystem or an accepted partial match. That is a design
question about a shared vocabulary — `Subsystems` is deliberately "a closed set, so the verbosity
capability, the generated settings rows and the model-free keyword router all draw on the same
vocabulary" — and it wants deciding rather than patching.

**Knock-on already in the tree.** `TechnicalLogBridge.SpeechSources` lists the speech-loop
namespaces itself rather than reading them from `Subsystems`, precisely because the entry it
would have read is wrong. When this is fixed, that list should become a lookup instead of a
second copy; the comment there says so.

---

## Closed

The four that were here shipped in 0.16.2 and their record is that section of the changelog.

One of them is fixed but not confirmed: the VR panel could not be picked up because the two
flags that make an overlay interactive were called by nothing at all, and no test on this side
of the headset can say whether the grab now works. A test asserts the call exists, the log
says whether a press ever arrives, and the rest is a Commander in a headset.
