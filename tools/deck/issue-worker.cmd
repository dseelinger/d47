@echo off
rem Stream Deck launcher. Asks which issue or lane, takes the model and effort from the last triage,
rem then opens the session. Press "Desktop" to hand it over.
cd /d C:\dev\d47

set NUM=
set /p NUM=Issue number or lane letter:
echo %NUM%| findstr /r "^[0-9][0-9]*$ ^[A-Za-z]$" >nul
if errorlevel 1 (
    echo Not an issue number or a lane letter.
    pause
    exit /b 1
)

rem The script prints "<model> <effort> <key>" and says on stderr where that came from.
set MODEL=sonnet
set EFFORT=medium
set KEY=%NUM%
for /f "usebackq tokens=1,2,3" %%a in (`python tools\deck\issue_settings.py %NUM%`) do (
    set MODEL=%%a
    set EFFORT=%%b
    set KEY=%%c
)

echo %KEY%| findstr /r "^[0-9]" >nul
if errorlevel 1 (
    claude -n "Lane %KEY%" --model %MODEL% --effort %EFFORT% "/issue-worker lane %KEY%"
) else (
    claude -n "#%KEY%" --model %MODEL% --effort %EFFORT% "/issue-worker %KEY%"
)
