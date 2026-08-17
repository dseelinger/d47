# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## None open.

The four that were here shipped in 0.16.2 and their record is that section of the changelog.

One of them is fixed but not confirmed: the VR panel could not be picked up because the two
flags that make an overlay interactive were called by nothing at all, and no test on this side
of the headset can say whether the grab now works. A test asserts the call exists, the log
says whether a press ever arrives, and the rest is a Commander in a headset.
