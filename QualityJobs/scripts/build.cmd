@echo off
setlocal
dotnet build -c Release "%~dp0..\QualityJobs.slnx"
exit /b %errorlevel%
