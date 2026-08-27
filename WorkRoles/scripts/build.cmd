@echo off
setlocal
dotnet build -c Release "%~dp0..\src\WorkRoles.slnx"
exit /b %errorlevel%
