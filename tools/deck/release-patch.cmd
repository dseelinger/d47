@echo off
cd /d C:\dev\d47
where pwsh >nul 2>nul && (pwsh -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -Patch) || (powershell -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -Patch)
pause
