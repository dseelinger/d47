@echo off
rem Opens a deck launcher in a new console, without the calling Claude session's environment, so
rem the session it starts is a separate one. Used by the /deck skill.
rem     spawn.cmd <window title> <launcher in tools\deck> [argument]
for /f "delims==" %%v in ('set CLAUDE 2^>nul') do set "%%v="
for /f "delims==" %%v in ('set MCP_ 2^>nul') do set "%%v="
set "ANTHROPIC_BASE_URL="
start "%~1" "%~dp0%~2" %3
