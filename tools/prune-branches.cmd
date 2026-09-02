@echo off
rem Shim. The rules live in prune-branches.ps1 and only there - see get-ver.cmd for why.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0prune-branches.ps1" %*
exit /b %ERRORLEVEL%
