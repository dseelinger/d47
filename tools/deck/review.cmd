@echo off
rem Stream Deck launcher. Opens a session in the repo, then press "To desktop" to hand it over.
cd /d C:\dev\d47
rem The issue worker commits and does not push, so origin/main is the base to review against.
claude -n "Review" "/code-review high origin/main"
