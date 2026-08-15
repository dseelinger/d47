#!/usr/bin/env python3
"""Step 0 of the Phase 14 `#102` plan: can d47 derive the material trader's *lines*?

Throwaway. Nothing in `src/` may reference this, and it is expected to be deleted once
the finding is written down. Run it:

    python spike/MaterialLineProbe/probe.py

The question
------------
A *line* is one column of the material trader's grid — one material per grade, e.g.
`Carbon -> Vanadium -> Niobium -> Yttrium`. Trading within a line is the cheap move;
crossing to another line costs a further 6x.

The journal cannot supply this. Its `Category` field names the Raw/Manufactured/Encoded
*type*, which no trade ever crosses. So Iron-for-Vanadium reads as "same category" and
prices as free when it is actually a 6x cross-line trade. Without the line, d47 is not
merely less capable, it is confidently wrong about most trades it is asked.

The source
----------
`EDDiscovery/EliteDangerousCore`, Apache-2.0, pinned at the commit below. The lines are
`MaterialGroupType` on each `Add(...)` call in `MCMRType.cs` — **C# source, not a data
file**, so this is a regex over source text and every assumption it makes is asserted
rather than trusted.

The data underneath is Frontier's, used under their media usage rules; EDDiscovery's own
Apache-2.0 grant covers their code, not the game facts. This probe derives and checks —
it writes no table.
"""

import json
import re
import sys
import urllib.request
from collections import defaultdict
from pathlib import Path

# Pinned rather than `master`: a re-run must be able to reproduce this finding, and the
# whole point of the exercise is that the shape of the source can move under us.
COMMIT = "459d01dc2bccf688019c2f8e45c3909277a4e316"
SOURCE = (
    f"https://raw.githubusercontent.com/EDDiscovery/EliteDangerousCore/{COMMIT}"
    "/EliteDangerous/FrontierData/Items/MCMRType.cs"
)

CACHE = Path(__file__).resolve().parent / "MCMRType.cs.cache"
GRADES_CS = (
    Path(__file__).resolve().parent.parent.parent
    / "src" / "D47.Core" / "Journal" / "MaterialGrades.g.cs"
)

# The five material commonalities, in the order the enum declares them. Asserted against
# the source below rather than taken on faith — if EDDiscovery reorders them, every grade
# in this probe shifts silently, which is the one failure mode that would look like success.
EXPECTED_ITEM_TYPES = ["VeryCommon", "Common", "Standard", "Rare", "VeryRare"]

MATERIAL_CATEGORIES = ("Raw", "Encoded", "Manufactured")

# Which lines the trader actually deals in. Not a guess and not folklore: EDDiscovery's
# own trader panel walks `MaterialGroupType` and admits exactly three contiguous ranges,
# in `EDDiscovery/UserControls/EngineeringSynthesis/UserControlMaterialTrader.cs`
# (the `sel == 0/1/2` tests around line 197). Everything the enum declares after
# `ManufacturedAlloys` is a grouping for display, not a column of the grid.
TRADER_RANGES = [
    ("RawCategory1", "RawCategory7"),
    ("EncodedEmissionData", "EncodedFirmware"),
    ("ManufacturedChemical", "ManufacturedAlloys"),
]

# `Add(CatType.X, ItemType.Y, MaterialGroupType.Z, MCMR.Id, [ "fdname", ] "English", "Sn")`
# Two overloads reach here: one takes an explicit FDName string, one derives it from the
# MCMR id. Both end in English name + short name, so count the trailing strings.
ADD_CALL = re.compile(
    r"""Add\(\s*CatType\.(?P<cat>\w+)\s*,
        \s*ItemType\.(?P<item>\w+)\s*,
        \s*MaterialGroupType\.(?P<group>\w+)\s*,
        \s*MCMR\.(?P<id>\w+)\s*,
        \s*(?P<strings>"(?:[^"\\]|\\.)*"(?:\s*,\s*"(?:[^"\\]|\\.)*")*)""",
    re.VERBOSE,
)

STRING_ARG = re.compile(r'"((?:[^"\\]|\\.)*)"')

# The same call *without* a MaterialGroupType. Those overloads default the group to NA,
# so a material added this way has no line and would price as a free same-line trade.
# EDDiscovery has at least one already, and a future one would arrive silently.
ADD_CALL_NO_GROUP = re.compile(
    r"""Add\(\s*CatType\.(?P<cat>\w+)\s*,
        \s*ItemType\.(?P<item>\w+)\s*,
        \s*MCMR\.(?P<id>\w+)\s*,
        \s*"(?P<english>(?:[^"\\]|\\.)*)\"""",
    re.VERBOSE,
)


def fetch() -> str:
    """The source text, cached beside the probe so re-runs do not hammer GitHub."""
    if CACHE.exists():
        return CACHE.read_text(encoding="utf-8-sig")
    with urllib.request.urlopen(SOURCE) as response:
        text = response.read().decode("utf-8-sig")
    CACHE.write_text(text, encoding="utf-8", newline="\n")
    return text


def enum_members(text: str, name: str) -> list[str]:
    """Members of an enum declaration, in declaration order, comments stripped."""
    match = re.search(
        rf"enum\s+{name}\b[^{{]*\{{(?P<body>.*?)\}}\s*;", text, re.DOTALL
    )
    if not match:
        raise SystemExit(f"could not find `enum {name}` — the source has changed shape")
    body = re.sub(r"//[^\n]*", "", match.group("body"))
    return [m.strip() for m in body.split(",") if m.strip()]


def parse_grades_cs() -> dict[str, int]:
    """`MaterialGrades.g.cs` as journal name -> grade. This is the table under test."""
    text = GRADES_CS.read_text(encoding="utf-8")
    pairs = re.findall(r'\["([^"]+)"\]\s*=\s*(\d+)', text)
    if not pairs:
        raise SystemExit(f"no entries parsed out of {GRADES_CS}")
    return {name: int(grade) for name, grade in pairs}


def check_against_trades(
    extract: Path,
    lines_of: dict[str, set[str]],
    grades_cs: dict[str, int],
    untradeable: set[str],
    failures: list[str],
) -> None:
    """Price every real trade in an extract using the derived line, and compare.

    This is the only check here that tests the *line* rather than the parse. A line
    table can be internally tidy and still be the wrong grouping; a table that predicts
    what a trader actually charged, trade after trade, is the right one.

    Predicted received-per-paid, from the published rule:

        down a grade   3x per grade      up a grade   1/6 per grade
        different line a further 1/6, whether or not it changes anything else

    The extract is one `MaterialTrade` JSON object per line. It is a Commander's own
    play history, so it stays out of the repository — keep it in the scratchpad and
    pass its path.
    """
    from fractions import Fraction

    if not extract.exists():
        raise SystemExit(f"no trade extract at {extract}")

    def line_of(name: str) -> str | None:
        found = lines_of.get(name.lower())
        return sorted(found)[0] if found else None

    total = agreed = 0
    unresolved: list[str] = []
    mismatches: list[str] = []
    cross_type = 0
    same_line_count = 0
    touched_untradeable: list[str] = []

    for raw in extract.read_text(encoding="utf-8", errors="replace").splitlines():
        raw = raw.strip()
        if not raw:
            continue
        event = json.loads(raw)
        paid, received = event.get("Paid", {}), event.get("Received", {})
        pn, rn = paid.get("Material", "").lower(), received.get("Material", "").lower()
        pq, rq = paid.get("Quantity"), received.get("Quantity")
        total += 1

        pl, rl = line_of(pn), line_of(rn)
        if pl is None or rl is None or pn not in grades_cs or rn not in grades_cs:
            unresolved.append(f"{pn} -> {rn}")
            continue
        if pl in untradeable or rl in untradeable:
            touched_untradeable.append(f"{pn} ({pl}) -> {rn} ({rl})")
        if paid.get("Category") != received.get("Category"):
            cross_type += 1

        same_line = pl == rl
        same_line_count += same_line
        delta = grades_cs[rn] - grades_cs[pn]
        rate = Fraction(3) ** -delta if delta < 0 else Fraction(6) ** -delta
        if not same_line:
            rate /= 6

        if Fraction(rq, pq) == rate:
            agreed += 1
        else:
            mismatches.append(
                f"{pn} G{grades_cs[pn]} ({pl}) x{pq} -> {rn} G{grades_cs[rn]} ({rl}) x{rq}: "
                f"predicted {rate}, observed {Fraction(rq, pq)}"
            )

    print(f"\n[6] Priced {total} real trades from {extract.name} using the derived line")
    print(f"      predicted rate correct: {agreed}/{total - len(unresolved)}")
    print(f"      same line: {same_line_count}, different line: {total - len(unresolved) - same_line_count}")
    print(f"      trades crossing Raw/Encoded/Manufactured: {cross_type}")
    print(f"      trades touching a Guardian/Thargoid group: {len(touched_untradeable)}")
    for item in touched_untradeable[:10]:
        print(f"        {item}")
    if unresolved:
        print(f"      unresolved material names: {len(unresolved)}")
        for item in unresolved[:10]:
            print(f"        {item}")
        failures.append("unresolved trade materials")
    if mismatches:
        print(f"      MISPRICED: {len(mismatches)}")
        for item in mismatches[:20]:
            print(f"        {item}")
        failures.append("mispriced trades")


def main() -> int:
    text = fetch()

    # --- assumption checks, before anything is derived from them -------------------
    item_types = enum_members(text, "ItemType")
    if item_types[:5] != EXPECTED_ITEM_TYPES:
        raise SystemExit(
            f"ItemType no longer starts {EXPECTED_ITEM_TYPES} but {item_types[:5]} — "
            "every grade this probe assigns would be wrong"
        )
    grade_of = {name: i + 1 for i, name in enumerate(EXPECTED_ITEM_TYPES)}

    declared_lines = enum_members(text, "MaterialGroupType")
    if declared_lines[0] != "NA":
        raise SystemExit(f"MaterialGroupType[0] is {declared_lines[0]!r}, expected 'NA'")
    declared_lines = declared_lines[1:]

    # --- parse the rows ------------------------------------------------------------
    rows = []           # (line, grade, fdname, english, category, item_type)
    skipped_na = 0
    non_material = []
    for match in ADD_CALL.finditer(text):
        cat, item, group, mcmr_id = (
            match.group("cat"), match.group("item"),
            match.group("group"), match.group("id"),
        )
        if group == "NA":
            skipped_na += 1
            continue
        if cat not in MATERIAL_CATEGORIES:
            non_material.append((cat, group, mcmr_id))
            continue
        if item not in grade_of:
            raise SystemExit(
                f"{mcmr_id} is a material on line {group} but its ItemType is {item}, "
                "which is not one of the five commonalities"
            )

        strings = STRING_ARG.findall(match.group("strings"))
        # Three strings means the overload carrying an explicit FDName; two means the
        # FDName is the MCMR id. The journal writes the FDName, lower-cased.
        fdname = strings[0] if len(strings) >= 3 else mcmr_id
        english = strings[-2] if len(strings) >= 2 else strings[-1]
        rows.append((group, grade_of[item], fdname.lower(), english, cat, item))

    # Materials filed without a group at all — they default to NA and so have no line.
    groupless = [
        (m.group("cat"), m.group("id"), m.group("english"))
        for m in ADD_CALL_NO_GROUP.finditer(text)
        if m.group("cat") in MATERIAL_CATEGORIES
    ]

    print(f"Source   {SOURCE}")
    print(f"Parsed   {len(rows)} material rows, skipped {skipped_na} rows on MaterialGroupType.NA")
    if non_material:
        print(f"         {len(non_material)} non-material rows carried a line and were skipped:")
        for cat, group, mcmr_id in non_material:
            print(f"           {cat}.{mcmr_id} on {group}")
    print(f"         {len(groupless)} material rows were filed with no MaterialGroupType at all:")
    for cat, mcmr_id, english in groupless:
        print(f"           {cat}.{mcmr_id} ({english})")

    # --- the count, verified rather than asserted ----------------------------------
    used_lines = sorted({r[0] for r in rows})
    print(f"Lines    {len(declared_lines)} declared in the enum, {len(used_lines)} carry materials")
    # Split the declared lines into the trader's grid columns and the rest, using the
    # contiguous ranges EDDiscovery's own trader panel admits.
    tradeable: list[str] = []
    for first, last in TRADER_RANGES:
        if first not in declared_lines or last not in declared_lines:
            raise SystemExit(
                f"trader range {first}..{last} is no longer in MaterialGroupType — "
                "re-read UserControlMaterialTrader.cs before trusting this split"
            )
        tradeable += declared_lines[declared_lines.index(first): declared_lines.index(last) + 1]
    untradeable = set(declared_lines) - set(tradeable)
    print(
        f"         {len(tradeable)} are trader grid columns "
        f"({'; '.join(f'{a}..{b}' for a, b in TRADER_RANGES)})"
    )
    print(f"         {len(untradeable)} are display groupings the trader does not deal in")

    empty = [line for line in declared_lines if line not in used_lines]
    if empty:
        print(f"         declared but empty: {', '.join(empty)}")
    undeclared = [line for line in used_lines if line not in declared_lines]
    if undeclared:
        print(f"         used but not declared: {', '.join(undeclared)}")

    # --- build the ladders ----------------------------------------------------------
    ladders: dict[str, dict[int, list[tuple[str, str]]]] = defaultdict(lambda: defaultdict(list))
    for line, grade, fdname, english, _cat, _item in rows:
        ladders[line][grade].append((fdname, english))

    category_of_line = {}
    for line, _grade, _fd, _en, cat, _item in rows:
        category_of_line.setdefault(line, set()).add(cat)

    print()
    print("=" * 78)
    print("THE LADDERS - eyeball against the in-game trader grid")
    print("=" * 78)
    for line in declared_lines:
        if line not in ladders:
            continue
        cats = "/".join(sorted(category_of_line[line]))
        depth = len(ladders[line])
        grid = "trader column" if line in tradeable else "display grouping, not traded"
        print(f"\n{line}  [{cats}, {depth} grade{'s' if depth != 1 else ''}, {grid}]")
        for grade in range(1, 6):
            entries = ladders[line].get(grade, [])
            if not entries:
                print(f"  G{grade}  -")
                continue
            for n, (fdname, english) in enumerate(entries):
                marker = "  " if n == 0 else " *"  # * marks a collision at this grade
                print(f"  G{grade}{marker}{english}  ({fdname})")

    # --- the checks -----------------------------------------------------------------
    print()
    print("=" * 78)
    print("CHECKS")
    print("=" * 78)
    failures = []

    # 1. no line holds two materials at the same grade
    collisions = [
        (line, grade, [e for _fd, e in entries])
        for line, by_grade in ladders.items()
        for grade, entries in sorted(by_grade.items())
        if len(entries) > 1
    ]
    on_grid = [c for c in collisions if c[0] in tradeable]
    off_grid = [c for c in collisions if c[0] not in tradeable]
    print(f"\n[1] Two materials at the same grade in one line: {len(collisions)} occurrence(s)")
    print(f"      in a trader grid column - a real failure: {len(on_grid)}")
    for line, grade, names in sorted(on_grid):
        print(f"        {line} G{grade}: {', '.join(names)}")
    print(f"      in a display grouping the trader does not deal in: {len(off_grid)}")
    for line, grade, names in sorted(off_grid):
        print(f"        {line} G{grade}: {', '.join(names)}")
    if on_grid:
        failures.append("grade collisions inside the trader grid")

    # 2. every material lands in exactly one line
    lines_of: dict[str, set[str]] = defaultdict(set)
    for line, _grade, fdname, _en, _cat, _item in rows:
        lines_of[fdname].add(line)
    multi = {fd: ls for fd, ls in lines_of.items() if len(ls) > 1}
    print(f"\n[2] Materials appearing in more than one line: {len(multi)}")
    for fdname, ls in sorted(multi.items()):
        print(f"      {fdname}: {', '.join(sorted(ls))}")
    if multi:
        failures.append("materials in multiple lines")

    # 3. every material in MaterialGrades.g.cs has a line
    grades_cs = parse_grades_cs()
    print(f"\n[3] MaterialGrades.g.cs holds {len(grades_cs)} materials")
    unlined = sorted(name for name in grades_cs if name not in lines_of)
    print(f"      without a line: {len(unlined)}")
    for name in unlined:
        print(f"        {name}  (grade {grades_cs[name]})")
    if unlined:
        failures.append("materials with no line")

    # 4. the reverse — EDDiscovery knows a material d47's table does not
    unknown = sorted(fd for fd in lines_of if fd not in grades_cs)
    print(f"\n[4] Lined materials absent from MaterialGrades.g.cs: {len(unknown)}")
    for fdname in unknown:
        line = sorted(lines_of[fdname])[0]
        grade = next(g for g, es in ladders[line].items() if any(f == fdname for f, _ in es))
        print(f"        {fdname}  ({line} G{grade})")

    # 5. the real check — do the grades agree?
    disagreements = []
    for line, grade, fdname, english, _cat, _item in rows:
        if fdname in grades_cs and grades_cs[fdname] != grade:
            disagreements.append((fdname, english, grades_cs[fdname], grade, line))
    checked = sum(1 for r in rows if r[2] in grades_cs)
    print(f"\n[5] Grade agreement over {checked} shared materials: {len(disagreements)} disagreement(s)")
    for fdname, english, ours, theirs, line in sorted(disagreements):
        print(f"      {fdname} ({english}): MaterialGrades.g.cs says {ours}, {line} says {theirs}")
    if disagreements:
        failures.append("grade disagreements")

    # 6. the strongest check available — does the line predict what traders charged?
    if len(sys.argv) > 1:
        check_against_trades(Path(sys.argv[1]), lines_of, grades_cs, untradeable, failures)
    else:
        print("\n[6] No trade extract given, so the rate check was skipped. Pass the path to a")
        print("      MaterialTrade extract to run it — see the module docstring.")

    print()
    print("=" * 78)
    if failures:
        print("RESULT: not clean - " + "; ".join(failures))
        print("Read the ladders above before concluding; a collision in a group the trader")
        print("does not trade is a different fact from one in a grid column.")
    else:
        print("RESULT: clean")
    print("=" * 78)
    return 0


if __name__ == "__main__":
    sys.exit(main())
