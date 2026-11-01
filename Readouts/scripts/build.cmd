@echo off
call "%~dp0..\..\Scripts\build-deploy-mod.cmd" Readouts EPrimeReadouts.slnx
exit /b %errorlevel%
