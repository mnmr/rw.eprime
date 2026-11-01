@echo off
call "%~dp0..\..\Scripts\build-deploy-mod.cmd" QualityJobs QualityJobs.slnx
exit /b %errorlevel%
