"""Gathers outside text for /triage-improver into a run folder without printing any of it."""
import argparse
import datetime
import hashlib
import json
import os
import subprocess
import sys
import zipfile

OWNER = "dseelinger"


def login(obj):
    return ((obj or {}).get("author") or {}).get("login", "")


def gh_issues():
    out = subprocess.run(
        ["gh", "issue", "list", "--state", "open", "--limit", "1000",
         "--json", "number,author,title,body,comments"],
        capture_output=True, text=True, encoding="utf-8", check=True).stdout
    return json.loads(out)


def excerpt_zips(folder):
    if not os.path.isdir(folder):
        return []
    return sorted(n for n in os.listdir(folder) if "-excerpt-" in n and n.endswith(".zip"))


def zip_markdown(path):
    with zipfile.ZipFile(path) as z:
        return "\n\n".join(z.read(n).decode("utf-8", "replace")
                           for n in sorted(z.namelist()) if n.endswith(".md"))


def last_sources(runs):
    if not os.path.isdir(runs):
        return None
    for name in sorted(os.listdir(runs), reverse=True):
        p = os.path.join(runs, name, "sources.txt")
        if os.path.isfile(p):
            return set(open(p, encoding="utf-8").read().split("\n")) - {""}
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fixture", help="folder with issues.json and excerpts/*.zip")
    ap.add_argument("--runs", help="runs folder")
    a = ap.parse_args()

    home = os.path.expanduser("~")
    runs = a.runs or os.path.join(home, ".claude", "triage-improver",
                                  "runs-fixture" if a.fixture else "runs")
    if a.fixture:
        issues = json.load(open(os.path.join(a.fixture, "issues.json"), encoding="utf-8"))
        zips_dir = os.path.join(a.fixture, "excerpts")
    else:
        issues = gh_issues()
        zips_dir = os.path.join(os.environ["LOCALAPPDATA"], "d47-donations", "downloads")

    parts, sources = [], []
    for i in sorted(issues, key=lambda x: x["number"]):
        own = login(i) == OWNER
        comments = [c for c in i.get("comments", []) if login(c) != OWNER]
        if own and not comments:
            continue
        if not own:
            sources.append(f"issue {i['number']}")
            parts.append(f"## Issue #{i['number']} by {login(i)}: {i['title']}\n\n{i['body']}\n")
        else:
            parts.append(f"## Comments on issue #{i['number']}: {i['title']}\n")
        for c in comments:
            sources.append(f"comment {c['id']}")
            parts.append(f"### Comment on #{i['number']} by {login(c)}\n\n{c['body']}\n")
    zips = excerpt_zips(zips_dir)
    for n in zips:
        sources.append(f"zip {n}")
        parts.append(f"## Excerpt {n}\n\n{zip_markdown(os.path.join(zips_dir, n))}\n")

    if set(sources) == last_sources(runs):
        print(f"nothing new ({len(sources)} sources, same as the last run)")
        return 0

    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run = os.path.join(runs, stamp)
    os.makedirs(run)
    data = ("# Outside text\n\n" + "\n".join(parts)).encode("utf-8")
    with open(os.path.join(run, "input.md"), "wb") as f:
        f.write(data)
    with open(os.path.join(run, "input.sha256"), "w") as f:
        f.write(hashlib.sha256(data).hexdigest() + "\n")
    with open(os.path.join(run, "sources.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(sorted(sources)) + "\n")
    print(f"run {run}")
    print(f"sources {len(sources)} ({len(zips)} excerpts)")
    return 0


sys.exit(main())
