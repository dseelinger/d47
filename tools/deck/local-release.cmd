@echo off
rem Builds Release, mirrors it to the pinned local-release folder, closes every other d47 and starts it.
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\dougs\.claude\skills\local-release\local-release.ps1" -Repo C:\dev\d47
pause
