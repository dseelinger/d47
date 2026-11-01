@echo off
rem Runs the generator in this window. Arguments pass through, e.g. run.cmd --until 05:00
rem Needs D47_MUSIC_SOURCE (data folder) and D47_ACE_STEP (ACE-Step checkout).
if "%D47_MUSIC_SOURCE%"=="" (echo D47_MUSIC_SOURCE is not set. & exit /b 1)
if "%D47_ACE_STEP%"=="" (echo D47_ACE_STEP is not set. & exit /b 1)
title d47 music generator
"%D47_ACE_STEP%\.venv\Scripts\python.exe" "%~dp0generate.py" %*
