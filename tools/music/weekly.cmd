@echo off
rem Run by the "d47 music weekly" scheduled task on Sunday at midnight.
rem Updates ACE-Step, writes the catalogue, generates the missing tracks until 05:00, publishes the folders that gained
rem tracks since the last publish, then empties that list.
if "%D47_MUSIC_SOURCE%"=="" (echo D47_MUSIC_SOURCE is not set. & exit /b 1)
if "%D47_ACE_STEP%"=="" (echo D47_ACE_STEP is not set. & exit /b 1)
set "GENERATED=%D47_MUSIC_SOURCE%\generated.txt"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-ace.ps1"
"%D47_ACE_STEP%\.venv\Scripts\python.exe" "%~dp0catalogue.py" || exit /b 1
"%D47_ACE_STEP%\.venv\Scripts\python.exe" "%~dp0generate.py" --until 05:00 || exit /b 1
if not exist "%GENERATED%" exit /b 0
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = @(Get-Content $env:GENERATED | Where-Object { $_ }); if ($f.Count -eq 0) { 'No tracks were added.'; exit 0 }; & '%~dp0..\publish-music.ps1' -Source (Join-Path $env:D47_MUSIC_SOURCE 'tracks') -Folders $f -Yes; exit $LASTEXITCODE" || exit /b 1
del "%GENERATED%"
