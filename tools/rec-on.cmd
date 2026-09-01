@echo off
rem rec-on, from cmd and PowerShell. A shim and deliberately nothing more.
rem
rem Where d47 is installed, what the switch is called and what happens when one is already running
rem live in rec-on.ps1 and only there, for the reason the bash shim beside this one gives: two
rem implementations are two things that have to agree, and they will not.
rem
rem %~dp0 is this file's own directory with a trailing backslash, so the pair travels together and
rem neither has to know where the repository is.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0rec-on.ps1" %*
exit /b %ERRORLEVEL%
