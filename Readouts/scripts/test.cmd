@echo off
setlocal
call "%~dp0build.cmd"
if errorlevel 1 exit /b %errorlevel%
dotnet test -c Release "%~dp0..\src\EPrimeReadouts.slnx" --no-build
exit /b %errorlevel%
