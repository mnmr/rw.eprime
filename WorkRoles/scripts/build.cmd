@echo off
call "%~dp0..\..\Scripts\build-deploy-mod.cmd" WorkRoles WorkRoles.slnx
exit /b %errorlevel%
