@echo off
rem Closes the running test drive, rebuilds, mirrors, syncs data and relaunches it.
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\dougs\.claude\skills\test-drive\test-drive.ps1"
pause
