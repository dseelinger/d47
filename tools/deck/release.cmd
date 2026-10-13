@echo off
rem Stream Deck launcher. Opens a session in the repo, then press "To desktop" to hand it over.
cd /d C:\dev\d47
rem Asks patch or minor, runs tools\release.ps1, fixes a failing build or test and runs it again.
claude -n "Release" --model opus --effort high "/release"
