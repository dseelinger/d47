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

rem The script prints "<model> <effort> <number> <directory>" and says on stderr where that came
rem from. The directory is the issue's lane worktree, which the script creates, or the main
rem checkout. For a lane with nothing left to start, or a worktree it could not create, it prints
rem nothing, and KEY stays empty.
set MODEL=sonnet
set EFFORT=medium
set KEY=
set DIR=
for /f "usebackq tokens=1,2,3,4" %%a in (`python tools\deck\issue_settings.py %NUM%`) do (
    set MODEL=%%a
    set EFFORT=%%b
    set KEY=%%c
    set DIR=%%d
)
if "%KEY%"=="" (
    pause
    exit /b 1
)
if "%DIR%"=="" (
    pause
    exit /b 1
)

cd /d "%DIR%"
claude -n "#%KEY%" --model %MODEL% --effort %EFFORT% "/issue-worker %KEY%"
