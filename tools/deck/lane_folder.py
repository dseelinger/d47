"""Keeps one worktree per lane, .claude/worktrees/lane-<X>, and switches it to each issue's branch.

A lane's folder is created once and reused for every issue in that lane, so the desktop app asks
to trust it once rather than once per issue. Each issue still has its own branch, issue/<N>.

    python tools/deck/lane_folder.py prepare A 2296   # prints the folder, on branch issue/2296
    python tools/deck/lane_folder.py done A           # detaches it at main once the branch merged
    python tools/deck/lane_folder.py park A 2296      # stashes unfinished edits, detaches at main
    python tools/deck/lane_folder.py tidy             # done for every lane folder

prepare refuses when the folder holds edits or a rebase for another issue, and prints why on
stderr with exit code 1. An issue/<M> branch that has merged into main counts as finished: prepare
switches away from it and deletes it.
"""

import os
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORKTREES = os.path.join(REPO, '.claude', 'worktrees')


def git(*args, cwd=REPO):
    return subprocess.run(['git', '-C', cwd] + list(args),
                          capture_output=True, text=True, encoding='utf-8', check=False)


def path_for(letter):
    return os.path.join(WORKTREES, 'lane-' + letter.upper())


def stash_name(letter, number):
    return 'lane {0} #{1} unfinished'.format(letter.upper(), number)


def registered(path):
    """True when git lists the folder as a worktree."""
    listed = git('worktree', 'list', '--porcelain').stdout
    want = os.path.normcase(os.path.normpath(path))
    return any(os.path.normcase(os.path.normpath(line[len('worktree '):])) == want
               for line in listed.splitlines() if line.startswith('worktree '))


def branch_exists(branch):
    return git('rev-parse', '--verify', '--quiet', 'refs/heads/' + branch).returncode == 0


def merged(branch):
    """True when the branch's tip is on main."""
    return git('merge-base', '--is-ancestor', branch, 'main').returncode == 0


def current(path):
    """The folder's branch, or '' when it is detached."""
    return git('branch', '--show-current', cwd=path).stdout.strip()


def unfinished(path):
    """Why the folder cannot change branch, or None."""
    gitdir = git('rev-parse', '--git-dir', cwd=path).stdout.strip()
    gitdir = gitdir if os.path.isabs(gitdir) else os.path.join(path, gitdir)
    for marker, what in (('rebase-merge', 'a rebase'), ('rebase-apply', 'a rebase'),
                         ('MERGE_HEAD', 'a merge'), ('CHERRY_PICK_HEAD', 'a cherry-pick')):
        if os.path.exists(os.path.join(gitdir, marker)):
            return 'it is in the middle of {0}'.format(what)
    changed = [line for line in git('status', '--porcelain', cwd=path).stdout.splitlines() if line]
    if changed:
        return 'it has {0} changed or new file{1}'.format(len(changed), '' if len(changed) == 1 else 's')
    return None


def copy_settings(path):
    local = os.path.join(REPO, '.claude', 'settings.local.json')
    if os.path.isfile(local):
        os.makedirs(os.path.join(path, '.claude'), exist_ok=True)
        shutil.copyfile(local, os.path.join(path, '.claude', 'settings.local.json'))


def detach(path):
    """Detaches the folder at main and deletes the merged issue branch it was on. (ok, why)."""
    branch = current(path)
    if branch and not (branch.startswith('issue/') and merged(branch)):
        return False, 'lane folder {0} is on {1}, which has not merged into main'.format(path, branch)
    why = unfinished(path)
    if why:
        return False, 'lane folder {0} cannot be detached: {1}'.format(path, why)
    switched = git('switch', '--detach', 'main', cwd=path)
    if switched.returncode != 0:
        return False, (switched.stderr or switched.stdout).strip()
    if branch:
        git('branch', '-d', branch)
    return True, None


def prepare(letter, number):
    """(path, None) with the lane's folder on issue/<number>, or (None, why it is not)."""
    path = path_for(letter)
    branch = 'issue/{0}'.format(number)

    if not registered(path):
        if os.path.exists(path):
            return None, '{0} exists but is not a git worktree; /pre-release clears leftovers'.format(path)
        if branch_exists(branch):
            made = git('worktree', 'add', path, branch)
        else:
            made = git('worktree', 'add', path, '-b', branch, 'main')
        if made.returncode != 0:
            return None, 'Could not create {0} for #{1}: {2}'.format(
                path, number, (made.stderr or made.stdout).strip())
        copy_settings(path)
        return path, None

    on = current(path)
    if on == branch:
        copy_settings(path)
        return path, None

    if on and not (on.startswith('issue/') and merged(on)):
        return None, 'Lane {0} is on {1}, which has not merged into main. Finish it, or run "python tools/deck/lane_folder.py park {0} {2}".'.format(
            letter.upper(), on, on[len('issue/'):] if on.startswith('issue/') else '<N>')
    why = unfinished(path)
    if why:
        return None, 'Lane {0} cannot switch to #{1}: {2}.'.format(letter.upper(), number, why)

    if branch_exists(branch):
        switched = git('switch', branch, cwd=path)
    else:
        switched = git('switch', '-c', branch, 'main', cwd=path)
    if switched.returncode != 0:
        return None, 'Could not switch {0} to {1}: {2}'.format(
            path, branch, (switched.stderr or switched.stdout).strip())
    if on:
        git('branch', '-d', on)
    copy_settings(path)
    return path, None


def park(letter, number):
    """Stashes the folder's unfinished edits for #number and detaches it at main. (ok, why)."""
    path = path_for(letter)
    if not registered(path):
        return True, None
    why = unfinished(path)
    if why and why.startswith('it is in the middle'):
        return False, 'lane folder {0} cannot be parked: {1}'.format(path, why)
    if why:
        stashed = git('stash', 'push', '--include-untracked', '--message', stash_name(letter, number), cwd=path)
        if stashed.returncode != 0:
            return False, (stashed.stderr or stashed.stdout).strip()
    switched = git('switch', '--detach', 'main', cwd=path)
    if switched.returncode != 0:
        return False, (switched.stderr or switched.stdout).strip()
    return True, None


def tidy():
    """Detaches every lane folder whose issue branch has merged. Returns the notes to print."""
    notes = []
    if not os.path.isdir(WORKTREES):
        return notes
    for name in sorted(os.listdir(WORKTREES)):
        path = os.path.join(WORKTREES, name)
        if not name.startswith('lane-') or not registered(path):
            continue
        on = current(path)
        if not on:
            continue
        ok, why = detach(path)
        notes.append('{0}: {1}'.format(name, 'detached at main, deleted {0}'.format(on) if ok else why))
    return notes


def main():
    args = sys.argv[1:]
    command = args[0] if args else ''
    if command == 'prepare' and len(args) == 3:
        path, why = prepare(args[1], args[2])
        if path is None:
            sys.stderr.write(why + '\n')
            sys.exit(1)
        print(path)
    elif command == 'done' and len(args) == 2:
        path = path_for(args[1])
        if registered(path) and current(path):
            ok, why = detach(path)
            if not ok:
                sys.stderr.write(why + '\n')
                sys.exit(1)
    elif command == 'park' and len(args) == 3:
        ok, why = park(args[1], args[2])
        if not ok:
            sys.stderr.write(why + '\n')
            sys.exit(1)
    elif command == 'tidy' and len(args) == 1:
        for note in tidy():
            print(note)
    else:
        sys.stderr.write(__doc__)
        sys.exit(2)


if __name__ == '__main__':
    main()
