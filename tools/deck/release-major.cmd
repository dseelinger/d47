@echo off
cd /d C:\dev\d47
choice /C YN /N /M "Cut a major release from origin/main? [Y/N] "
if errorlevel 2 exit /b 0
where pwsh >nul 2>nul && (pwsh -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -Major) || (powershell -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -Major)
pause
