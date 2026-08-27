@echo off
rem get-ver, from cmd and PowerShell. A shim and deliberately nothing more.
rem
rem The resolution rules - what "0.79" means, whether a named version includes pre-releases, which
rem asset is the installer - live in get-ver.ps1 and only there, for the reason the bash shim beside
rem this one gives: two implementations are two things that have to agree, and they will not.
rem
rem %~dp0 is this file's own directory with a trailing backslash, so the pair travels together and
rem neither has to know where the repository is.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-ver.ps1" %*
exit /b %ERRORLEVEL%
