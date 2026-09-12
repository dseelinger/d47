@echo off
rem Stream Deck launcher. Asks which issue, takes the model and effort from the last triage, then
rem opens the session. Press "Desktop" to hand it over.
cd /d C:\dev\d47

set NUM=
set /p NUM=Issue number:
echo %NUM%| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo Not an issue number.
    pause
    exit /b 1
)

rem The script prints "<model> <effort>" and says on stderr where that came from.
set MODEL=sonnet
set EFFORT=medium
for /f "usebackq tokens=1,2" %%a in (`python tools\deck\issue_settings.py %NUM%`) do (
    set MODEL=%%a
    set EFFORT=%%b
)

claude -n "#%NUM%" --model %MODEL% --effort %EFFORT% "/issue-worker %NUM%"
