@echo off
setlocal
call "%~dp0build.cmd"
if errorlevel 1 exit /b %errorlevel%
dotnet test -c Release "%~dp0..\src\Implanner.slnx" --no-build
exit /b %errorlevel%
