@echo off
rem Shim. The rules live in promote.ps1 and only there - see get-ver.cmd for why.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0promote.ps1" %*
exit /b %ERRORLEVEL%
