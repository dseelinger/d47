"""Reports NPC given names that GivenNames does not yet recognise as a woman's.

Elite records no sex for anybody: 914 journals, 696,000 events, 221 event kinds, and not one
carries a gender field. What the journals do supply is the names, so this reads them, strips the
given name off the front of each, and lists what the shipped set does not match — most often
first, because that is the order worth working through.

It reads the C# set rather than a copy of it, so the two cannot drift apart. It writes nothing:
what to add is a judgement, and this only says where to look.

    python tools/scan-npc-names.py [--top 60]
"""

import argparse
import collections
import glob
import os
import re

NAMES = os.path.join(os.path.dirname(__file__), "..", "src", "D47.Core", "Audio", "GivenNames.cs")

JOURNALS = os.path.join(
    os.path.expanduser("~"), "Saved Games", "Frontier Developments", "Elite Dangerous")

SENDER = re.compile(r'"From":"\$npc_name_decorate:#name=([^;]*);"')


def shipped():
    """The set as the app has it, read out of the source rather than duplicated here."""
    with open(NAMES, encoding="utf-8") as handle:
        source = handle.read()

    start = source.index("Feminine = new(")
    end = source.index("};", start)

    return {name.lower() for name in re.findall(r'"([^"]+)"', source[start:end])}


def given(name):
    """The given name off the front, as GivenNames.ReadsFemale takes it."""
    first = name.split(" ")[0] if name else ""
    return first.strip("'\".,")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--top", type=int, default=60)
    parser.add_argument("--journals", default=JOURNALS)
    arguments = parser.parse_args()

    feminine = shipped()
    heard = collections.Counter()

    for path in glob.glob(os.path.join(arguments.journals, "Journal.*.log")):
        try:
            with open(path, encoding="utf-8", errors="ignore") as handle:
                for line in handle:
                    found = SENDER.search(line)

                    if found:
                        heard[given(found.group(1))] += 1
        except OSError:
            # A journal the game currently has open. There are 900 others.
            continue

    if not heard:
        print(f"No named NPC transmissions found under {arguments.journals}")
        return

    total = sum(heard.values())
    matched = sum(count for name, count in heard.items() if name.lower() in feminine)

    print(f"{len(feminine)} names shipped")
    print(f"{total} named-NPC transmissions, {len(heard)} distinct given names")
    print(f"{matched} transmissions matched ({100 * matched / total:.1f}%)")
    print()
    print("Commonest unmatched, most heard first. Women in this list are what to add:")

    unmatched = [(count, name) for name, count in heard.items() if name.lower() not in feminine]

    for count, name in sorted(unmatched, reverse=True)[: arguments.top]:
        print(f"  {count:5d}  {name}")


if __name__ == "__main__":
    main()
