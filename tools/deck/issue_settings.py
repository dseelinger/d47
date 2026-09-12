"""Resolves an issue number to the model and effort triage chose for it.

Prints "<model> <effort>" on stdout for the launcher to capture, and a line saying where that
came from on stderr, which the launcher shows but does not read. Anything unreadable, unknown or
outside the accepted tokens falls back to the defaults and says so — the values go on a command
line, so nothing else is allowed through.

    python tools/deck/issue_settings.py 105
"""

import datetime
import json
import os
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


def resolve(number):
    """(model, effort, note). The note is for the maintainer to read, not to parse."""
    if not os.path.isfile(STATE):
        return DEFAULT_MODEL, DEFAULT_EFFORT, 'No triage state. Run /triage to write one.'

    try:
        state = json.load(open(STATE, encoding='utf-8'))
    except ValueError:
        return DEFAULT_MODEL, DEFAULT_EFFORT, 'Triage state will not parse. Run /triage again.'

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
    if entry.get('review'):
        note += ' Flagged for {0}.'.format(entry['review'])
    return model, effort, note


def main():
    number = sys.argv[1] if len(sys.argv) > 1 else ''
    model, effort, note = resolve(number)
    print(model, effort)
    sys.stderr.write('{0} Starting on {1} / {2}.\n'.format(note, model, effort))


if __name__ == '__main__':
    main()
