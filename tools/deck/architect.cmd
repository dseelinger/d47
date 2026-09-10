@echo off
rem Stream Deck launcher. Opens a session in the repo, then press "To desktop" to hand it over.
cd /d C:\dev\d47
claude -n "Architect" --model opus --effort high "/architect"
