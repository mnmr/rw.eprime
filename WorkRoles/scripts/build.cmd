@echo off
setlocal
dotnet build -c Release "%~dp0..\WorkRoles.slnx"
exit /b %errorlevel%
