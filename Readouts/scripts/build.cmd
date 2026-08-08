@echo off
setlocal
dotnet build -c Release "%~dp0..\src\EPrimeReadouts.sln"
exit /b %errorlevel%
