@echo off
cd /d C:\dev\d47
dotnet test tests\D47.Core.Tests --filter FullyQualifiedName~Ticking
pause
