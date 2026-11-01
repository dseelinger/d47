@echo off
rem Run by the "d47 music nightly" scheduled task at midnight.
start "d47 music generator" /min cmd /c ""%~dp0run.cmd" --until 05:00"
