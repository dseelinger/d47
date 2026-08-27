@echo off
rem Shim. The rules live in prerelease.ps1 and only there - see get-ver.cmd for why.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0prerelease.ps1" %*
exit /b %ERRORLEVEL%
