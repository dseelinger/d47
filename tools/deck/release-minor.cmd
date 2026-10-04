@echo off
cd /d C:\dev\d47
where pwsh >nul 2>nul && (pwsh -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -Minor) || (powershell -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -Minor)
pause
