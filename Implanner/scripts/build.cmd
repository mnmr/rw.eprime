@echo off
setlocal
dotnet build -c Release "%~dp0..\src\Implanner.slnx"
exit /b %errorlevel%
