#!/usr/bin/env python3
"""Publish one approved stock story to the GitHub release `stories-1`.

Not part of the build. Writes no file in the checkout.

    python tools/publish-story.py <folder> [--replace] [--dry-run] [--work <dir outside the repository>]
                                  [--via-actions --branch <branch>]

<folder> holds `card.json`, `hidden.json` and the picked pictures: `<story-id>.<cast-id>.png`, or
`<story-id>.<cast-id>.for-man.png` and `.for-woman.png` for a cast member with `versions`.

The script downloads `index.json` from the release (an empty list if there is none), refuses an id already in
it unless --replace is given, checks the card and hidden entry with `faults` from seal-stories.py and requires a
picture for every primary cast member (any other member with no picture file is published without one), numbers the card,
lists the primary members' picture names in its `castPictures`, and builds in the work directory: `<id>.sealed`, a 1024 px
JPEG `<name>.jpg` and the kept `<name>.original.png` for each picture, and the new `index.json`. It then
uploads them through `gh api`, replacing any asset of the same name, `index.json` last. --dry-run stops before
the upload and lists each file with its size. The release is created with make_latest false when missing:
UpdateChecker reads the latest release to find updates.

Only REST endpoints are called, because cloud sessions refuse GitHub's GraphQL API, which `gh release` uses.

--via-actions is for sessions whose GitHub access accepts only JSON request bodies, which cannot upload release
assets. It commits the built files to `story-upload/<id>/` on <branch> without touching the checkout, pushes the
branch, dispatches .github/workflows/publish-story.yml to upload them from the runner, and waits for that run.
"""
import argparse
import base64
import importlib.util
import json
import mimetypes
import os
import pathlib
import shutil
import subprocess
import sys
import tempfile
import time
import zlib

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is needed to convert the pictures: python -m pip install pillow")

REPO = pathlib.Path(__file__).resolve().parent.parent
SLUG = "dseelinger/d47"
RELEASE = "stories-1"
INDEX = "index.json"
SIZE = 1024
QUALITY = 85
WORKFLOW = "publish-story.yml"
WORKFLOW_REF = "main"
STAGING = "story-upload"
WAIT = 15 * 60


def load_sealer():
    spec = importlib.util.spec_from_file_location("seal_stories", REPO / "tools" / "seal-stories.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def outside_repository(path: pathlib.Path, what: str) -> pathlib.Path:
    path = path.resolve()
    if path == REPO or REPO in path.parents:
        sys.exit(f"Refusing a {what} inside the repository: {path}")
    return path


def gh_api(*args: str, body: str | None = None) -> subprocess.CompletedProcess:
    return subprocess.run(["gh", "api", *args], input=body, capture_output=True, text=True, encoding="utf-8")


def git(*args: str, env: dict | None = None) -> str:
    done = subprocess.run(["git", *args], cwd=REPO, env=env, capture_output=True, text=True, encoding="utf-8")
    if done.returncode != 0:
        sys.exit(f"git {args[0]} failed: {done.stderr.strip()}")
    return done.stdout.strip()


def stage(files: list, story: str, branch: str, work: pathlib.Path) -> None:
    """Commit the files to story-upload/<story>/ on the branch through a separate index, then push the branch."""
    env = {**os.environ, "GIT_INDEX_FILE": str(work / "staging.index")}
    parent = None
    if git("ls-remote", "--heads", "origin", branch):
        git("fetch", "--quiet", "origin", branch)
        parent = git("rev-parse", "FETCH_HEAD")
        git("read-tree", parent, env=env)
    else:
        git("read-tree", "--empty", env=env)
    git("rm", "-r", "--cached", "--quiet", "--ignore-unmatch", f"{STAGING}/{story}", env=env)
    for file in files:
        blob = git("hash-object", "-w", str(file))
        git("update-index", "--add", "--cacheinfo", f"100644,{blob},{STAGING}/{story}/{file.name}", env=env)
    tree = git("write-tree", env=env)
    commit = git("commit-tree", tree, *(["-p", parent] if parent else []), "-m", f"Stage {story} for the {RELEASE} release")
    git("push", "--quiet", "origin", f"{commit}:refs/heads/{branch}")
    (work / "staging.index").unlink(missing_ok=True)


def run_workflow(story: str, branch: str) -> None:
    """Dispatch the upload workflow and wait for its run to finish."""
    runs = f"repos/{SLUG}/actions/workflows/{WORKFLOW}/runs?event=workflow_dispatch&per_page=10"
    listed = gh_api(runs)
    if listed.returncode != 0:
        sys.exit(f"Could not list the runs of {WORKFLOW}: {listed.stderr.strip()}")
    seen = {run["id"] for run in json.loads(listed.stdout)["workflow_runs"]}
    body = json.dumps({"ref": WORKFLOW_REF, "inputs": {"branch": branch, "story": story}})
    started = gh_api("--method", "POST", f"repos/{SLUG}/actions/workflows/{WORKFLOW}/dispatches", "--input", "-", body=body)
    if started.returncode != 0:
        sys.exit(f"Could not dispatch {WORKFLOW}: {started.stderr.strip()}")

    deadline = time.monotonic() + WAIT
    run = None
    while time.monotonic() < deadline:
        time.sleep(10)
        listed = gh_api(runs)
        if listed.returncode != 0:
            continue
        fresh = [r for r in json.loads(listed.stdout)["workflow_runs"] if r["id"] not in seen]
        run = fresh[0] if fresh else None
        if run is not None and run["status"] == "completed":
            break
    if run is None:
        sys.exit(f"{WORKFLOW} was dispatched but no run appeared within {WAIT // 60} minutes.")
    if run["status"] != "completed":
        sys.exit(f"{WORKFLOW} is still running after {WAIT // 60} minutes: {run['html_url']}")
    if run["conclusion"] != "success":
        sys.exit(f"{WORKFLOW} ended {run['conclusion']}: {run['html_url']}")


def find_release() -> dict | None:
    """The release with its assets; None when it does not exist."""
    view = gh_api(f"repos/{SLUG}/releases/tags/{RELEASE}")
    if view.returncode != 0:
        if "HTTP 404" in view.stderr:
            return None
        sys.exit(f"Could not read the release {RELEASE}: {view.stderr.strip()}")
    return json.loads(view.stdout)


def download_index(release: dict | None) -> list:
    """The release's story list; empty when the release or its index.json is missing."""
    asset = next((a for a in (release or {}).get("assets", []) if a["name"] == INDEX), None)
    if asset is None:
        return []
    fetched = gh_api("-H", "Accept: application/octet-stream", f"repos/{SLUG}/releases/assets/{asset['id']}")
    if fetched.returncode != 0:
        sys.exit(f"Could not download {INDEX}: {fetched.stderr.strip()}")
    return json.loads(fetched.stdout)


def create_release() -> dict:
    created = gh_api("--method", "POST", f"repos/{SLUG}/releases", "-f", f"tag_name={RELEASE}",
                     "-f", "name=Stock stories", "-f", "body=Stock story files downloaded by d47. Not an app version.",
                     "-f", "make_latest=false")
    if created.returncode != 0:
        sys.exit(f"Could not create the release {RELEASE}: {created.stderr.strip()}")
    return json.loads(created.stdout)


def upload(release: dict, file: pathlib.Path) -> None:
    """Upload one asset, deleting any asset of the same name first."""
    for asset in release.get("assets", []):
        if asset["name"] == file.name:
            deleted = gh_api("--method", "DELETE", f"repos/{SLUG}/releases/assets/{asset['id']}")
            if deleted.returncode != 0:
                sys.exit(f"Could not replace {file.name}: {deleted.stderr.strip()}")
    content_type = mimetypes.guess_type(file.name)[0] or "application/octet-stream"
    uploaded = gh_api("--method", "POST", "-H", f"Content-Type: {content_type}", "--input", str(file),
                      f"https://uploads.github.com/repos/{SLUG}/releases/{release['id']}/assets?name={file.name}")
    if uploaded.returncode != 0:
        sys.exit(f"Upload of {file.name} failed: {uploaded.stderr.strip()}")


def picture_names(entry: dict) -> list:
    """(name, primary) for every picture the cast can have."""
    names = []
    for speaker in entry.get("cast") or []:
        base = f"{entry['id']}.{speaker['id']}"
        primary = speaker.get("primary") is True
        bases = [f"{base}.for-man", f"{base}.for-woman"] if speaker.get("versions") is not None else [base]
        names += [(name, primary) for name in bases]
    return names


def seal(entry: dict) -> str:
    packer = zlib.compressobj(9, zlib.DEFLATED, -15)
    packed = packer.compress(json.dumps([entry], ensure_ascii=False).encode("utf-8")) + packer.flush()
    text = base64.b64encode(packed).decode("ascii")
    return "\n".join(text[at:at + 100] for at in range(0, len(text), 100)) + "\n"


def convert(source: pathlib.Path, work: pathlib.Path, name: str) -> list:
    shutil.copyfile(source, work / f"{name}.original.png")
    with Image.open(source) as image:
        image.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS).save(work / f"{name}.jpg", "JPEG", quality=QUALITY)
    return [work / f"{name}.jpg", work / f"{name}.original.png"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folder", type=pathlib.Path)
    parser.add_argument("--replace", action="store_true", help="replace a story already in the release")
    parser.add_argument("--dry-run", action="store_true", help="check and build, list the files, upload nothing")
    parser.add_argument("--work", type=pathlib.Path, help="working directory outside the repository")
    parser.add_argument("--via-actions", action="store_true", help="upload through the publish-story workflow")
    parser.add_argument("--branch", help="with --via-actions, the branch the built files are pushed to")
    args = parser.parse_args()
    if args.via_actions and not args.branch:
        parser.error("--via-actions needs --branch")

    folder = outside_repository(args.folder, "story folder")
    work = outside_repository(args.work, "working directory") if args.work else None
    if work is None:
        work = pathlib.Path(tempfile.mkdtemp(prefix="d47-publish-story-"))
    work.mkdir(parents=True, exist_ok=True)
    for stale in work.iterdir():
        if stale.is_file():
            stale.unlink()

    card = json.loads((folder / "card.json").read_text(encoding="utf-8"))
    entry = json.loads((folder / "hidden.json").read_text(encoding="utf-8"))
    story = card.get("id")
    if not story or entry.get("id") != story:
        sys.exit(f"card.json has the id {story} and hidden.json has {entry.get('id')}; they must match")

    release = find_release()
    index = download_index(release)
    known = next((at for at, existing in enumerate(index) if existing.get("id") == story), None)
    if known is not None and not args.replace:
        sys.exit(f"{story} is already in the release; pass --replace to publish it again")

    sealer = load_sealer()
    problems = [f"{story}: {fault}" for fault in sealer.faults(entry, sealer.persona_ids(), card)]
    sources = {}
    for name, primary in picture_names(entry):
        picture = folder / f"{name}.png"
        if picture.is_file():
            sources[name] = picture
        elif primary:
            problems.append(f"{story}: no picture {picture.name}; a primary cast member needs one")
        else:
            print(f"{story}: no picture {picture.name}; published without it")
    if problems:
        print("\n".join(problems), file=sys.stderr)
        sys.exit(1)

    card["castPictures"] = [name for name, primary in picture_names(entry) if primary]
    card["number"] = index[known]["number"] if known is not None else max((c.get("number", 0) for c in index), default=0) + 1
    if known is not None:
        index[known] = card
    else:
        index.append(card)

    (work / f"{story}.sealed").write_text(seal(entry), encoding="ascii", newline="\n")
    files = [work / f"{story}.sealed"]
    for name, source in sources.items():
        files += convert(source, work, name)
    (work / INDEX).write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    files.append(work / INDEX)

    for file in files:
        print(f"{file.name}  {file.stat().st_size:,} bytes")
    if args.dry_run:
        print(f"Dry run: nothing uploaded. Files are in {work}")
        return

    if release is None:
        release = create_release()
    if args.via_actions:
        stage(files, story, args.branch, work)
        run_workflow(story, args.branch)
    else:
        for file in files:
            upload(release, file)
    print(f"Published {story} as number {card['number']} to {RELEASE}.")


if __name__ == "__main__":
    main()
