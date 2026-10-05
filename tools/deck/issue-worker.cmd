@echo off
rem Stream Deck launcher. Asks which issue, or which lane to take the next issue from, takes the
rem model and effort from the last triage, then opens the session. Press "Desktop" to hand it over.
cd /d C:\dev\d47

set NUM=
set /p NUM=Issue number or lane letter:
echo %NUM%| findstr /r "^[0-9][0-9]*$ ^[A-Za-z]$" >nul
if errorlevel 1 (
    echo Not an issue number or a lane letter.
    pause
    exit /b 1
)

rem The script prints "<model> <effort> <number>" and says on stderr where that came from. For a
rem lane with nothing left to start it prints nothing, and KEY stays empty.
set MODEL=sonnet
set EFFORT=medium
set KEY=
for /f "usebackq tokens=1,2,3" %%a in (`python tools\deck\issue_settings.py %NUM%`) do (
    set MODEL=%%a
    set EFFORT=%%b
    set KEY=%%c
)
if "%KEY%"=="" (
    pause
    exit /b 1
)

claude -n "#%KEY%" --model %MODEL% --effort %EFFORT% "/issue-worker %KEY%"
