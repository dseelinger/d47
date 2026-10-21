#!/usr/bin/env python3
"""Regenerate d47's tables of the events, fields and values a story step may name.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/StepEvents.tsv` and
`StepFields.tsv` and shipped as embedded resources; the app never runs this. Re-run it when the
schemas change or when more journals are available:

    python tools/gen-step-events.py <journal folder> [<journal folder> ...]

Journal events, fields and descriptions come from jixxed/ed-journal-schemas at a pinned commit,
one JSON Schema per event. Values of a string field come from the schema's `examples`, a
committed Knowledge table column, and `Journal.*.log` under the folders given, and only for the
fields on `curated_step_events.WHITELIST`. d47 events come from `docs/capabilities/*.md`.

The script exits, naming the field, when a whitelisted field has no values from any source.
"""

from __future__ import annotations

import io
import json
import re
import sys
import urllib.request
import zipfile
from pathlib import Path

import curated_step_events as curated

REPOSITORY = "https://github.com/jixxed/ed-journal-schemas"
REVISION = "e4976b5f9b46f784029364453622c2668087cfd6"
SOURCE = f"{REPOSITORY}/archive/{REVISION}.zip"

ROOT = Path(__file__).resolve().parent.parent
KNOWLEDGE = ROOT / "src" / "D47.Core" / "Knowledge"
DOCS = ROOT / "docs" / "capabilities"

SCHEMA_PATH = re.compile(rf"^[^/]+/schemas/([A-Za-z0-9]+)/\1\.json$")
TOOL_HEADING = re.compile(r"^#### `([a-z0-9_]+)`\s*$")
HEADING = re.compile(r"^#{1,6} ")
FENCE = re.compile(r"^```(\w*)\s*$")


def clean(text: str) -> str:
    return " ".join(text.split())


def read_schemas() -> dict[str, dict]:
    request = urllib.request.Request(SOURCE, headers={"User-Agent": "d47-gen-step-events"})
    with urllib.request.urlopen(request, timeout=120) as response:
        archive = zipfile.ZipFile(io.BytesIO(response.read()))

    schemas: dict[str, dict] = {}
    for name in archive.namelist():
        match = SCHEMA_PATH.match(name)
        if match and match.group(1) != "_Event":
            schemas[match.group(1)] = json.loads(archive.read(name).decode("utf-8"))

    if len(schemas) != 275:
        raise SystemExit(f"{len(schemas)} schemas at {REVISION}; expected 275.")
    return schemas


def declared_fields(schema: dict) -> list[tuple[str, str, dict]]:
    fields = []
    for name, spec in schema.get("properties", {}).items():
        kind = spec.get("type")
        if kind not in ("string", "boolean"):
            continue
        if name in ("timestamp", "event") or name.endswith("_Localised"):
            continue
        fields.append((name, kind, spec))
    return sorted(fields)


def knowledge_values(file: str, column: str, lower: bool) -> set[str]:
    values: set[str] = set()
    header: list[str] | None = None
    for line in (KNOWLEDGE / file).read_text(encoding="utf-8").splitlines():
        if line.startswith("# ") and "\t" in line and header is None and column in line:
            header = line[2:].split("\t")
            continue
        if not line or line[0] == "#":
            continue
        if header is None:
            header = line.split("\t")
            continue
        cells = line.split("\t")
        if column in header and header.index(column) < len(cells) and cells[header.index(column)]:
            value = cells[header.index(column)]
            values.add(value.lower() if lower else value)
    return values


def journal_values(folders: list[Path], wanted: dict[str, set[str]]) -> dict[tuple[str, str], set[str]]:
    found: dict[tuple[str, str], set[str]] = {}
    markers = {event: f'"{event}"' for event in wanted}
    for folder in folders:
        for path in sorted(folder.glob("Journal.*.log")):
            with path.open(encoding="utf-8", errors="replace") as lines:
                for line in lines:
                    for event, marker in markers.items():
                        if marker not in line:
                            continue
                        try:
                            entry = json.loads(line)
                        except json.JSONDecodeError:
                            break
                        if entry.get("event") != event:
                            continue
                        for field in wanted[event]:
                            value = entry.get(field)
                            if isinstance(value, str) and value:
                                found.setdefault((event, field), set()).add(value)
                        break
    return found


def tools_and_groups(missing: list[str]) -> tuple[list[str], list[str]]:
    tools: set[str] = set()
    for page in sorted(DOCS.glob("*.md")):
        lines = page.read_text(encoding="utf-8").splitlines()
        for index, line in enumerate(lines):
            heading = TOOL_HEADING.match(line)
            if heading is None:
                continue
            fenced = False
            for after in lines[index + 1:]:
                if HEADING.match(after) and not fenced:
                    break
                fence = FENCE.match(after)
                if fence and not fenced:
                    fenced = True
                    if fence.group(1) == "json":
                        tools.add(heading.group(1))
                        break
                elif fence:
                    fenced = False
            else:
                pass
            if heading.group(1) not in tools:
                missing.append(f"{page.name}: {heading.group(1)}")

    groups = group_enum(DOCS / "checklists.md")
    return sorted(tools), groups


def group_enum(page: Path) -> list[str]:
    lines = page.read_text(encoding="utf-8").splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("#### `add_to_checklist`"))
    block = []
    inside = False
    for line in lines[start + 1:]:
        if FENCE.match(line):
            if inside:
                break
            inside = True
            continue
        if inside:
            block.append(line)
    schema = json.loads("\n".join(block))
    return list(schema["properties"]["group"]["enum"])


def main() -> None:
    folders = [Path(argument) for argument in sys.argv[1:]]
    if not folders or not all(folder.is_dir() for folder in folders):
        raise SystemExit("Usage: gen-step-events.py <journal folder> [<journal folder> ...]")

    schemas = read_schemas()

    def drawable_event(event: str) -> bool:
        return event not in curated.SNAPSHOT_EVENTS and not curated.NOT_DRAWABLE.search(event)

    wanted: dict[str, set[str]] = {}
    for entry in curated.WHITELIST:
        event, field = entry.split(".")
        if event not in schemas:
            raise SystemExit(f"{entry}: no schema for {event}")
        wanted.setdefault(event, set()).add(field)

    fromjournals = journal_values(folders, wanted)

    event_rows: list[list[str]] = []
    field_rows: list[list[str]] = []
    problems: list[str] = []

    for event in sorted(schemas):
        schema = schemas[event]
        drawable = "yes" if drawable_event(event) else "no"
        event_rows.append([
            "journal", event, drawable, curated.NEEDS.get(event, ""), curated.CAREERS.get(event, ""),
            clean(schema.get("description", "")),
        ])

        for field, kind, spec in declared_fields(schema):
            key = f"{event}.{field}"
            if kind == "boolean":
                field_rows.append([event, field, kind, "", drawable])
                field_rows.append([event, field, kind, "false", drawable])
                field_rows.append([event, field, kind, "true", drawable])
                continue

            field_rows.append([event, field, kind, "", drawable])
            if key not in curated.WHITELIST:
                continue

            values = {v for v in spec.get("examples", []) if isinstance(v, str) and v}
            if key in curated.KNOWLEDGE_COLUMNS:
                values |= knowledge_values(*curated.KNOWLEDGE_COLUMNS[key])
            values |= fromjournals.get((event, field), set())

            if not values:
                problems.append(f"{key}: whitelisted, but no values from any source")
            for value in sorted(values):
                field_rows.append([event, field, kind, value, drawable])

    for entry in curated.WHITELIST:
        event, field = entry.split(".")
        if not any(r[0] == event and r[1] == field for r in field_rows):
            problems.append(f"{entry}: whitelisted, but not a top-level string field")

    missing: list[str] = []
    tools, groups = tools_and_groups(missing)
    for line in missing:
        print(f"No `#### name` heading with a json block, left out: {line}")

    event_rows.append(["d47", "ToolRun", "yes", "", "", curated.TOOL_RUN_DESCRIPTION])
    field_rows.append(["ToolRun", "Tool", "string", "", "yes"])
    for tool in tools:
        field_rows.append(["ToolRun", "Tool", "string", tool, "yes" if tool in curated.DRAWABLE_TOOLS else "no"])

    event_rows.append(["d47", "ChecklistLineAdded", "no", "", "", curated.CHECKLIST_DESCRIPTION])
    field_rows.append(["ChecklistLineAdded", "Group", "string", "", "no"])
    for group in sorted(groups):
        field_rows.append(["ChecklistLineAdded", "Group", "string", group,
                           "yes" if group in curated.DRAWABLE_GROUPS else "no"])

    if problems:
        raise SystemExit("\n".join(problems))

    notice = [
        "# Do not edit by hand — rerun the tool.",
        f"# Sources: {REPOSITORY} at {REVISION}, schemas/<Event>/<Event>.json (Apache-2.0);",
        "# Journal.*.log in the folders given on the command line, whitelisted fields only;",
        "# docs/capabilities/*.md for the d47 events. Event data is Frontier game data; see NOTICE.",
    ]
    write("StepEvents.tsv", "gen-step-events.py", notice,
          "source\tevent\tdrawable\tneeds\tcareer\tdescription", event_rows)
    write("StepFields.tsv", "gen-step-events.py", notice,
          "event\tfield\ttype\tvalue\tdrawable", field_rows)


def write(name: str, tool: str, notice: list[str], columns: str, rows: list[list[str]]) -> None:
    header = [f"# Generated by tools/{tool}. {notice[0][2:]}"] + notice[1:] + [f"# {columns}"]
    text = "\n".join(header + ["\t".join(row) for row in rows]) + "\n"
    (KNOWLEDGE / name).write_text(text, encoding="utf-8", newline="\n")
    print(f"Wrote {len(rows)} rows to {KNOWLEDGE / name}")


if __name__ == "__main__":
    main()
