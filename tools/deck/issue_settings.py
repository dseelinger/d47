"""Resolves an issue number, or a lane letter, to the issue to start and the model and effort
triage chose for it.

A lane letter resolves to the first issue in that lane with no "Fixes #N" commit on main.

Prints "<model> <effort> <number>" on stdout for the launcher to capture, and a line saying where
that came from on stderr, which the launcher shows but does not read. Anything unreadable, unknown
or outside the accepted tokens falls back to the defaults and says so — the values go on a command
line, so nothing else is allowed through. A lane with no issue left to start prints nothing on
stdout and exits 1.

    python tools/deck/issue_settings.py 105
    python tools/deck/issue_settings.py b
"""

import datetime
import json
import os
import re
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


def next_in_lane(state, letter):
    """(number, None), or (None, a note saying why there is none)."""
    when = age(state.get('generated')) or 'age unknown'
    lane = (state.get('lanes') or {}).get(letter)
    if not isinstance(lane, list):
        return None, 'Triage ({0}) has no lane {1}. Run /triage lanes.'.format(when, letter)
    done = landed()
    for number in lane:
        if str(number) not in done:
            return str(number), None
    return None, 'Lane {0} is finished: every issue in it has merged.'.format(letter)


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
    print(model, effort, number)
    sys.stderr.write('{0} Starting #{1} on {2} / {3}.\n'.format(note, number, model, effort))


if __name__ == '__main__':
    main()
