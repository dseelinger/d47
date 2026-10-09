@echo off
rem Stream Deck launcher. Opens the Launcher session in the repo with Remote Control on, so the
rem Claude phone app can reach it from the Code tab. Leave the window open.
cd /d C:\dev\d47
claude -n "Launcher" --remote-control "Launcher" --model sonnet
