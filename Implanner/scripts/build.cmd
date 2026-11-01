@echo off
call "%~dp0..\..\Scripts\build-deploy-mod.cmd" Implanner Implanner.slnx
exit /b %errorlevel%
