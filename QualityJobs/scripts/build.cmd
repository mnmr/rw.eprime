@echo off
setlocal
dotnet build -c Release "%~dp0..\src\QualityJobs.slnx"
exit /b %errorlevel%
