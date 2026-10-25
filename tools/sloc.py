#!/usr/bin/env python3
"""Count code lines in the tracked C# and AXAML sources under `src/` and `tests/`.

Not part of the build and never run by CI. Standard library only.

    python tools/sloc.py                       # every project, then src, tests and total
    python tools/sloc.py src/D47.Core          # only files under the given directories
    python tools/sloc.py --by-dir              # also a row per directory under each project

A code line is a line that is neither blank nor comment-only. Comments are `//`, `///` and
`/* */` in `.cs` and `<!-- -->` in `.axaml`. A line with code and a trailing comment is code.
Comment markers inside C# string and character literals, including verbatim, interpolated and
raw strings, are not comments.

The file list comes from `git ls-files`. Files under `src/D47.Vr/vendor/` and files named
`*.g.cs` are excluded. A project is the first directory under `src/` or `tests/`; with
`--by-dir`, each directory directly under a project gets a row that includes its
subdirectories, and files directly in the project are counted under `.`.
"""

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TOP_LEVELS = ("src", "tests")
EXTENSIONS = (".cs", ".axaml")
EXCLUDED_DIRS = ("src/D47.Vr/vendor/",)
EXCLUDED_SUFFIXES = (".g.cs",)


class Counts:
    def __init__(self) -> None:
        self.files = 0
        self.blank = 0
        self.comment = 0
        self.code = 0

    def add(self, other: "Counts") -> None:
        self.files += other.files
        self.blank += other.blank
        self.comment += other.comment
        self.code += other.code


def classify(text: str, has_code: list[bool], has_comment: list[bool]) -> Counts:
    counts = Counts()
    counts.files = 1
    for line, code, comment in zip(text.split("\n"), has_code, has_comment):
        if code:
            counts.code += 1
        elif not line.strip():
            counts.blank += 1
        elif comment:
            counts.comment += 1
        else:
            raise AssertionError(f"unclassified line: {line!r}")
    return counts


def count_axaml(text: str) -> Counts:
    lines = text.split("\n")
    has_code = [False] * len(lines)
    has_comment = [False] * len(lines)
    in_comment = False
    for number, line in enumerate(lines):
        i = 0
        while i < len(line):
            if in_comment:
                end = line.find("-->", i)
                has_comment[number] = True
                if end < 0:
                    break
                in_comment = False
                i = end + 3
            elif line.startswith("<!--", i):
                in_comment = True
                has_comment[number] = True
                i += 4
            else:
                if not line[i].isspace():
                    has_code[number] = True
                i += 1
    return classify(text, has_code, has_comment)


# Lexer frames for C#. A code frame is the file body or an interpolation hole; a string frame is
# a string or character literal.
CODE, STRING = "code", "string"
REGULAR, VERBATIM, RAW, CHAR = "regular", "verbatim", "raw", "char"


def count_cs(text: str) -> Counts:
    lines = text.split("\n")
    line_of = []
    for number, line in enumerate(lines):
        line_of.extend([number] * (len(line) + 1))
    has_code = [False] * len(lines)
    has_comment = [False] * len(lines)

    # Code frame: [CODE, brace depth]. String frame: [STRING, kind, dollars, quotes].
    stack: list[list] = [[CODE, 0]]
    at_line_start = True
    n = len(text)
    i = 0

    def mark_code(start: int, end: int) -> None:
        for k in range(start, end):
            if not text[k].isspace():
                has_code[line_of[k]] = True

    def run_of(ch: str, start: int) -> int:
        end = start
        while end < n and text[end] == ch:
            end += 1
        return end - start

    while i < n:
        c = text[i]
        frame = stack[-1]

        if frame[0] == CODE:
            if c == "\n":
                at_line_start = True
                i += 1
                continue
            if c.isspace():
                i += 1
                continue
            if at_line_start and c == "#" and len(stack) == 1:
                end = text.find("\n", i)
                end = n if end < 0 else end
                mark_code(i, end)
                i = end
                continue
            at_line_start = False
            if text.startswith("//", i):
                end = text.find("\n", i)
                end = n if end < 0 else end
                has_comment[line_of[i]] = True
                i = end
                continue
            if text.startswith("/*", i):
                end = text.find("*/", i + 2)
                end = n if end < 0 else end + 2
                for k in range(i, end):
                    has_comment[line_of[k]] = True
                i = end
                continue
            if c == "{":
                frame[1] += 1
            elif c == "}" and len(stack) > 1 and frame[1] == 0:
                stack.pop()
                has_code[line_of[i]] = True
                i += 1
                continue
            elif c == "}":
                frame[1] -= 1
            elif c == "'":
                stack.append([STRING, CHAR, 0, 1])
            elif c in "$@" or c == '"':
                j = i
                while j < n and text[j] in "$@":
                    j += 1
                if j < n and text[j] == '"':
                    prefix = text[i:j]
                    dollars = prefix.count("$")
                    quotes = run_of('"', j)
                    mark_code(i, j + min(quotes, 3))
                    if "@" in prefix:
                        stack.append([STRING, VERBATIM, dollars, 1])
                        i = j + 1
                    elif quotes >= 3:
                        stack.append([STRING, RAW, dollars, quotes])
                        i = j + quotes
                    elif quotes == 2:
                        i = j + 2
                    else:
                        stack.append([STRING, REGULAR, dollars, 1])
                        i = j + 1
                    continue
                mark_code(i, j)
                i = j
                continue
            has_code[line_of[i]] = True
            i += 1
            continue

        _, kind, dollars, quotes = frame
        if c == "\n":
            i += 1
            continue
        if not c.isspace():
            has_code[line_of[i]] = True
        if kind == CHAR:
            if c == "\\":
                mark_code(i, i + 2)
                i += 2
            elif c == "'":
                stack.pop()
                i += 1
            else:
                i += 1
            continue
        if kind == REGULAR and c == "\\":
            mark_code(i, i + 2)
            i += 2
            continue
        if c == '"':
            if kind == REGULAR:
                stack.pop()
                i += 1
            elif kind == VERBATIM:
                if i + 1 < n and text[i + 1] == '"':
                    i += 2
                else:
                    stack.pop()
                    i += 1
            else:
                run = run_of('"', i)
                mark_code(i, i + run)
                if run >= quotes:
                    stack.pop()
                i += run
            continue
        if dollars and c == "{":
            run = run_of("{", i)
            mark_code(i, i + run)
            if kind == RAW:
                opens = run >= dollars
            else:
                opens = run % 2 == 1
            i += run
            if opens:
                stack.append([CODE, 0])
                at_line_start = False
            continue
        if dollars and c == "}" and kind != RAW:
            run = run_of("}", i)
            mark_code(i, i + run)
            i += run
            continue
        i += 1

    return classify(text, has_code, has_comment)


def tracked_files(paths: list[str]) -> list[str]:
    specs = []
    for path in paths or list(TOP_LEVELS):
        full = Path(path).resolve()
        try:
            specs.append(full.relative_to(ROOT).as_posix())
        except ValueError:
            sys.exit(f"{path} is outside the repository")
    result = subprocess.run(
        ["git", "-C", str(ROOT), "ls-files", "-z", "--", *specs],
        check=True,
        capture_output=True,
    )
    files = []
    for name in result.stdout.decode("utf-8").split("\0"):
        if not name or not name.endswith(EXTENSIONS):
            continue
        if name.split("/", 1)[0] not in TOP_LEVELS:
            continue
        if name.startswith(EXCLUDED_DIRS) or name.endswith(EXCLUDED_SUFFIXES):
            continue
        if not (ROOT / name).is_file():
            continue
        files.append(name)
    for path, spec in zip(paths, specs):
        prefix = "" if spec == "." else spec.rstrip("/") + "/"
        if not any(name.startswith(prefix) or name == spec for name in files):
            sys.exit(f"{path} holds no counted files")
    return sorted(files)


def count_file(name: str) -> Counts:
    text = (ROOT / name).read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
    if text.endswith("\n"):
        text = text[:-1]
    if not text:
        counts = Counts()
        counts.files = 1
        return counts
    return count_cs(text) if name.endswith(".cs") else count_axaml(text)


def main() -> None:
    parser = argparse.ArgumentParser(description="Count code lines in src/ and tests/.")
    parser.add_argument("paths", nargs="*", help="directories to restrict the count to")
    parser.add_argument("--by-dir", action="store_true", help="add a row per directory under each project")
    args = parser.parse_args()

    projects: dict[str, Counts] = {}
    directories: dict[str, dict[str, Counts]] = {}
    for name in tracked_files(args.paths):
        parts = name.split("/")
        project = "/".join(parts[:2]) if len(parts) > 2 else parts[0]
        directory = parts[2] if len(parts) > 3 else "."
        counts = count_file(name)
        projects.setdefault(project, Counts()).add(counts)
        directories.setdefault(project, {}).setdefault(directory, Counts()).add(counts)

    totals = {top: Counts() for top in TOP_LEVELS}
    for project, counts in projects.items():
        totals[project.split("/", 1)[0]].add(counts)
    total = Counts()
    for counts in totals.values():
        total.add(counts)

    rows: list[tuple[str, Counts | None]] = []
    for project in sorted(projects):
        rows.append((project, projects[project]))
        if args.by_dir:
            for directory in sorted(directories[project]):
                rows.append(("  " + directory, directories[project][directory]))
    rows.append(("", None))
    for top in TOP_LEVELS:
        rows.append((top, totals[top]))
    rows.append(("total", total))

    width = max(len("project"), *(len(label) for label, _ in rows))
    header = f"{'project':<{width}}  {'files':>7}  {'blank':>9}  {'comment':>9}  {'code':>9}"
    print(header)
    print("-" * len(header))
    for label, counts in rows:
        if counts is None:
            print("-" * len(header))
            continue
        print(
            f"{label:<{width}}  {counts.files:>7,}  {counts.blank:>9,}  "
            f"{counts.comment:>9,}  {counts.code:>9,}"
        )


if __name__ == "__main__":
    main()
