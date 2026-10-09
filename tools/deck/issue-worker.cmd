@echo off
rem Stream Deck launcher. Takes the issue number or lane letter as an argument, or asks for it,
rem takes the model, effort and advisor from the last triage, then opens the session. Press
rem "Desktop" to hand it over.
cd /d C:\dev\d47

set NUM=%~1
if "%NUM%"=="" set /p NUM=Issue number or lane letter:
echo %NUM%| findstr /r "^[0-9][0-9]*$ ^[A-Za-z]$" >nul
if errorlevel 1 (
    echo Not an issue number or a lane letter.
    pause
    exit /b 1
)

rem The script prints "<model> <effort> <advisor> <number> <directory>" and says on stderr where
rem that came from. The advisor is "opus" or "none". The directory is the issue's lane worktree,
rem which the script creates, or the main checkout. For a lane with nothing left to start, or a
rem worktree it could not create, it prints nothing, and KEY stays empty.
set MODEL=sonnet
set EFFORT=medium
set ADVISOR=none
set KEY=
set DIR=
for /f "usebackq tokens=1,2,3,4,5" %%a in (`python tools\deck\issue_settings.py %NUM%`) do (
    set MODEL=%%a
    set EFFORT=%%b
    set ADVISOR=%%c
    set KEY=%%d
    set DIR=%%e
)
if "%KEY%"=="" (
    pause
    exit /b 1
)
if "%DIR%"=="" (
    pause
    exit /b 1
)

set ADVISOR_ARG=
if "%ADVISOR%"=="opus" set ADVISOR_ARG=--advisor opus

cd /d "%DIR%"
claude -n "#%KEY%" --model %MODEL% --effort %EFFORT% %ADVISOR_ARG% "/issue-worker %KEY%"
