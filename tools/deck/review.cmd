@echo off
rem Stream Deck launcher. Takes an issue number as an argument, or asks for it, then opens a review
rem of that issue's commits and everything committed after them. With no number it reviews the last
rem commit. The issue worker pushes when it lands, so origin/main holds the work and cannot be the
rem base. Press "Desktop" to hand it over.
cd /d C:\dev\d47

set NUM=%~1
if "%NUM%"=="" set /p NUM=Issue number, or Enter for the last commit:
if "%NUM%"=="" (
    claude -n "Review" "/code-review high HEAD~1"
    exit /b
)
echo %NUM%| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo Not an issue number.
    pause
    exit /b 1
)

rem The oldest of the last 300 commits whose subject has "#N" followed by ")" or ",": the issue's
rem first commit. A combined commit names "(#N, #M)". Bodies are not searched, since prose there
rem names other issues.
set FIRST=
for /f "usebackq tokens=1" %%h in (`git log -n 300 --reverse --format^="%%h %%s" ^| findstr /r "#%NUM%[),]"`) do (
    if not defined FIRST set FIRST=%%h
)
if "%FIRST%"=="" (
    echo No recent commit names #%NUM%.
    pause
    exit /b 1
)

claude -n "Review #%NUM%" "/code-review high %FIRST%~1"
