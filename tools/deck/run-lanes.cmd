@echo off
rem Stream Deck launcher. Starts every lane of the last /triage lanes overnight, each in its own
rem console. Arguments go to run-lanes.ps1: "run-lanes.cmd -DryRun" starts nothing.
cd /d C:\dev\d47
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-lanes.ps1" %*
pause
