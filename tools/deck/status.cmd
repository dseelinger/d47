@echo off
cd /d C:\dev\d47\repo
git status --short --branch
echo.
git log --oneline -12
echo.
pause
