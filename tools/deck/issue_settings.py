"""Resolves an issue number, or a lane letter, to the issue to start and the model and effort
triage chose for it.

A lane letter resolves to the first issue in that lane that is neither closed on GitHub nor named
by a "Fixes #N" commit on main.

Prints "<model> <effort> <number> <directory>" on stdout for the launcher to capture, and a line
saying where that came from on stderr, which the launcher shows but does not read. Anything
unreadable, unknown or outside the accepted tokens falls back to the defaults and says so — the
values go on a command line, so nothing else is allowed through. A lane with no issue left to start
prints nothing on stdout and exits 1.

The directory is the main checkout, or the issue's worktree when the issue has a lane or a lane
worktree already exists. The worktree is created here, from local main on branch issue/<N>, so a
lane session starts inside it and never in the main checkout. When it cannot be created, nothing
is printed on stdout and the exit code is 1.

    python tools/deck/issue_settings.py 105
    python tools/deck/issue_settings.py b
"""

import datetime
import json
import os
import re
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STATE = os.path.join(REPO, '.claude', 'triage-state.json')

DEFAULT_MODEL = 'sonnet'
DEFAULT_EFFORT = 'medium'
MODELS = ('opus', 'sonnet', 'haiku')
EFFORTS = ('low', 'medium', 'high', 'xhigh', 'max')


def age(stamp):
    """How old the report is, in words, or None when the stamp will not parse."""
    try:
        written = datetime.datetime.fromisoformat(str(stamp).replace('Z', '+00:00'))
    except ValueError:
        return None
    minutes = (datetime.datetime.now(datetime.timezone.utc) - written).total_seconds() / 60
    if minutes < 90:
        return '{0:.0f} min old'.format(minutes)
    if minutes < 48 * 60:
        return '{0:.0f} hours old'.format(minutes / 60)
    return '{0:.0f} days old'.format(minutes / 1440)


def is_lane(key):
    return len(key) == 1 and key.isalpha()


def load():
    """(state, None), or (None, a note saying why there is none)."""
    if not os.path.isfile(STATE):
        return None, 'No triage state. Run /triage to write one.'
    try:
        return json.load(open(STATE, encoding='utf-8')), None
    except ValueError:
        return None, 'Triage state will not parse. Run /triage again.'


def landed():
    """Issue numbers named by a "Fixes #N" line on local main."""
    log = subprocess.run(['git', '-C', REPO, 'log', 'main', '--format=%B'],
                         capture_output=True, text=True, encoding='utf-8', check=False).stdout
    return set(re.findall(r'^Fixes #(\d+)\s*$', log, re.MULTILINE))


def closed(number):
    """True when GitHub reports the issue closed. False when gh fails, so the issue is offered."""
    result = subprocess.run(['gh', 'issue', 'view', str(number), '--json', 'state', '-q', '.state'],
                            capture_output=True, text=True, encoding='utf-8', check=False, cwd=REPO)
    return result.returncode == 0 and result.stdout.strip() == 'CLOSED'


def next_in_lane(state, letter):
    """(number, None), or (None, a note saying why there is none)."""
    when = age(state.get('generated')) or 'age unknown'
    lane = (state.get('lanes') or {}).get(letter)
    if not isinstance(lane, list):
        return None, 'Triage ({0}) has no lane {1}. Run /triage lanes.'.format(when, letter)
    done = landed()
    for number in lane:
        if str(number) not in done and not closed(number):
            return str(number), None
    return None, 'Lane {0} is finished: every issue in it has merged or closed.'.format(letter)


def git(*args):
    return subprocess.run(['git', '-C', REPO] + list(args),
                          capture_output=True, text=True, encoding='utf-8', check=False)


def lanes_running():
    """True when git lists a worktree under .claude/worktrees."""
    listed = git('worktree', 'list', '--porcelain').stdout
    marker = os.path.normcase(os.path.join(REPO, '.claude', 'worktrees'))
    return any(os.path.normcase(os.path.normpath(line[len('worktree '):])).startswith(marker)
               for line in listed.splitlines() if line.startswith('worktree '))


def worktree(number):
    """(path, None) for the issue's worktree, created if missing, or (None, why it could not be)."""
    path = os.path.join(REPO, '.claude', 'worktrees', str(number))
    branch = 'issue/{0}'.format(number)
    if os.path.isdir(path):
        return path, None
    if git('rev-parse', '--verify', '--quiet', 'refs/heads/' + branch).returncode == 0:
        made = git('worktree', 'add', path, branch)
    else:
        made = git('worktree', 'add', path, '-b', branch, 'main')
    if made.returncode != 0:
        return None, 'Could not create the worktree for #{0}: {1}'.format(
            number, (made.stderr or made.stdout).strip())
    local = os.path.join(REPO, '.claude', 'settings.local.json')
    if os.path.isfile(local):
        shutil.copyfile(local, os.path.join(path, '.claude', 'settings.local.json'))
    return path, None


def resolve(state, number):
    """(model, effort, note). The note is for the maintainer to read, not to parse."""
    when = age(state.get('generated')) or 'age unknown'
    entry = (state.get('issues') or {}).get(str(number))
    if not isinstance(entry, dict):
        return DEFAULT_MODEL, DEFAULT_EFFORT, 'Triage ({0}) does not name #{1}.'.format(
            when, number)

    model = entry.get('model')
    effort = entry.get('effort')
    if model not in MODELS or effort not in EFFORTS:
        return DEFAULT_MODEL, DEFAULT_EFFORT, 'Triage ({0}) named no usable model for #{1}.'.format(
            when, number)

    note = 'Triage ({0}).'.format(when)
    if entry.get('lane'):
        note += ' Lane {0}.'.format(entry['lane'])
    if entry.get('review'):
        note += ' Flagged for {0}.'.format(entry['review'])
    return model, effort, note


def main():
    key = sys.argv[1] if len(sys.argv) > 1 else ''
    state, missing = load()

    if is_lane(key):
        if state is None:
            sys.stderr.write(missing + '\n')
            sys.exit(1)
        number, why = next_in_lane(state, key.upper())
        if number is None:
            sys.stderr.write(why + '\n')
            sys.exit(1)
    else:
        number = key

    if state is None:
        model, effort, note = DEFAULT_MODEL, DEFAULT_EFFORT, missing
    else:
        model, effort, note = resolve(state, number)

    entry = ((state or {}).get('issues') or {}).get(str(number))
    laned = isinstance(entry, dict) and bool(entry.get('lane'))
    directory = REPO
    if laned or lanes_running():
        directory, why = worktree(number)
        if directory is None:
            sys.stderr.write(why + '\n')
            sys.exit(1)
        note += ' In worktree {0}.'.format(directory)

    print(model, effort, number, directory)
    sys.stderr.write('{0} Starting #{1} on {2} / {3}.\n'.format(note, number, model, effort))


if __name__ == '__main__':
    main()
