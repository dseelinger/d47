@echo off
cd /d C:\dev\d47
where pwsh >nul 2>nul && (pwsh -NoProfile -ExecutionPolicy Bypass -File tools\watch-release.ps1) || (powershell -NoProfile -ExecutionPolicy Bypass -File tools\watch-release.ps1)
pause
