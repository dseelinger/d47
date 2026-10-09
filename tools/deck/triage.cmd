@echo off
rem Stream Deck launcher. Opens a session in the repo, then press "To desktop" to hand it over.
cd /d C:\dev\d47
rem An argument is passed to the skill: "triage.cmd lanes" runs "/triage lanes".
if "%~1"=="" (
    claude -n "Triage" --model opus --effort medium "/triage"
) else (
    claude -n "Triage" --model opus --effort medium "/triage %~1"
)
