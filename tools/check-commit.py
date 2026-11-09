"""Runs the Debug build and both Category=Gate test filters, then stamps the staged tree.

The pre-commit hook (.githooks/pre-commit) refuses a commit unless the stamp matches `git write-tree`.
Stage the change first, then run this; staging anything afterwards invalidates the stamp.
"""

import subprocess
import sys

STAMP_NAME = "d47-checked-tree"

STEPS = [
    ["dotnet", "build", "d47.slnx", "-c", "Debug", "--nologo"],
    ["dotnet", "test", "tests/D47.Core.Tests", "--filter", "Category=Gate", "--nologo"],
    ["dotnet", "test", "tests/D47.App.Tests", "--filter", "Category=Gate", "--nologo"],
]


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True, check=True).stdout.strip()


def main():
    tree = git("write-tree")
    for step in STEPS:
        print("check-commit:", " ".join(step), flush=True)
        if subprocess.run(step).returncode != 0:
            print("check-commit: failed, no stamp written", file=sys.stderr)
            return 1
    if git("write-tree") != tree:
        print("check-commit: the index changed during the run, no stamp written", file=sys.stderr)
        return 1
    with open(git("rev-parse", "--git-path", STAMP_NAME), "w", encoding="ascii") as stamp:
        stamp.write(tree + "\n")
    print("check-commit: stamped", tree)
    return 0


if __name__ == "__main__":
    sys.exit(main())
