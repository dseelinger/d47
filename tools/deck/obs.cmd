@echo off
rem OBS loads its plugins and locale relative to the working directory, so start it from its own folder.
cd /d "C:\Program Files\obs-studio\bin\64bit"
start "" obs64.exe
