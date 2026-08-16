@echo off
setlocal
dotnet build -c Release "%~dp0..\src\EPrimeReadouts.slnx"
exit /b %errorlevel%
