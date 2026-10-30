"""PreToolUse hook: refuses Bash commands that fail on this machine, with what to do instead.

Reads the hook's JSON from stdin. Prints a deny decision to refuse; prints nothing to allow.
Any input it cannot read is allowed. `--check` runs the built-in cases and exits 1 on a mismatch.
"""

import json
import re
import sys

MOST_BYTES = 6000

PYTHON3 = re.compile(r"(?:^|[;&|(`\n]|\$\()\s*python3(?:\.\d+)?(?![\w.-])")
DRIVE_PATH = re.compile(r"(?<!\w)[A-Za-z]:\\")
TREE_PATH = re.compile(r"\b(?:src|tests|tools|docs|design|installer)\\\w")
TMP_PATH = re.compile(r"(?<![\w.~-])/tmp(?:/|(?![\w.-]))")


def refusal(command):
    """The reason to refuse `command`, or None to allow it."""
    size = len(command.encode("utf-8"))
    if size >= MOST_BYTES:
        return (f"This command is {size:,} bytes; Bash commands of about 7 KB or more arrive cut off "
                f"and fail with 'unexpected EOF'. The limit here is {MOST_BYTES:,}. Write file contents "
                "with the Write tool. For a long script, write it to a file with Write and run that file.")
    if PYTHON3.search(command):
        return ("'python3' is the Microsoft Store alias on this machine and fails with "
                "'Python was not found'. Use 'python'.")
    if DRIVE_PATH.search(command) or TREE_PATH.search(command):
        return ("This command has a path with backslashes, which Bash removes or collapses "
                "(src\\D47.Core arrives as srcD47.Core). Use forward slashes: src/D47.Core, C:/dev/d47.")
    if TMP_PATH.search(command):
        return ("Windows programs, Python among them, read /tmp/ as C:\\tmp, not the Git Bash /tmp. "
                "Use the session scratchpad directory instead.")
    return None


def respond(raw):
    """The hook's stdout for the stdin text `raw`: a deny decision, or empty to allow."""
    try:
        event = json.loads(raw)
        if event.get("tool_name") != "Bash":
            return ""
        command = event["tool_input"]["command"]
    except (ValueError, KeyError, TypeError, AttributeError):
        return ""
    if not isinstance(command, str):
        return ""
    reason = refusal(command)
    if reason is None:
        return ""
    return json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": "deny",
        "permissionDecisionReason": reason,
    }})


HEREDOC = "cat > notes.txt <<'EOF'\nline one\nline two\nEOF"

CASES = [
    ("refuse", "echo " + "a" * (MOST_BYTES - 5)),
    ("refuse", "python3 x.py"),
    ("refuse", "cd tools && python3 -c 'print(1)'"),
    ("refuse", "ls $(python3 -c 'print(1)')"),
    ("refuse", "ls src\\D47.Core"),
    ("refuse", "cat C:\\dev\\d47\\README.md"),
    ("refuse", "python -c \"open('C:\\\\dev\\\\x')\""),
    ("refuse", "ls /tmp/x"),
    ("refuse", "cd /tmp"),
    ("allow", "echo " + "a" * (MOST_BYTES - 6)),
    ("allow", "python -I x.py"),
    ("allow", "echo python3"),
    ("allow", "grep \"a\\|b\" src"),
    ("allow", "echo \"Status:\\\\n\""),
    ("allow", "ls src/D47.Core"),
    ("allow", "ls C:/dev/d47/src"),
    ("allow", "ls /c/tmp/x"),
    ("allow", "cmd.exe //c \"tools\\\\deck\\\\spawn.cmd\" Claude claude.cmd"),
    ("allow", HEREDOC),
]


def check():
    failures = 0
    for expected, command in CASES:
        out = respond(json.dumps({"tool_name": "Bash", "tool_input": {"command": command}}))
        got = "refuse" if out else "allow"
        if got != expected:
            failures += 1
            print(f"FAIL: expected {expected}, got {got}: {command[:80]!r}")
    for raw, label in [("not json", "unreadable input"),
                       (json.dumps({"tool_name": "Read", "tool_input": {"file_path": "/tmp/x"}}), "another tool")]:
        if respond(raw):
            failures += 1
            print(f"FAIL: expected allow for {label}")
    total = len(CASES) + 2
    print(f"{total - failures} of {total} cases passed")
    return 1 if failures else 0


def main():
    if sys.argv[1:] == ["--check"]:
        return check()
    try:
        raw = sys.stdin.buffer.read().decode("utf-8", errors="replace")
    except OSError:
        return 0
    out = respond(raw)
    if out:
        sys.stdout.write(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
