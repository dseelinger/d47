@echo off
rem Asks the generator to stop after the track it is on.
if "%D47_MUSIC_SOURCE%"=="" (echo D47_MUSIC_SOURCE is not set. & exit /b 1)
type nul > "%D47_MUSIC_SOURCE%\STOP"
echo The music generator will stop after the current track.
timeout /t 3 >nul
