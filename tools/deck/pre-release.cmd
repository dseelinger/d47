@echo off
rem Stream Deck launcher. Opens a session in the repo, then press "To desktop" to hand it over.
cd /d C:\dev\d47
rem Runs the whole suite in Release, fixes what fails and pushes main. Then press Patch or Minor.
claude -n "Pre-release" --model opus --effort high "/pre-release"
