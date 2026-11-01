@echo off
setlocal
rem Build (Release) and deploy every mod present. Skips missing folders, stops on the first failure.
call "%~dp0build-deploy-mod.cmd" Implanner Implanner.slnx || exit /b 1
call "%~dp0build-deploy-mod.cmd" QualityJobs QualityJobs.slnx || exit /b 1
call "%~dp0build-deploy-mod.cmd" Readouts EPrimeReadouts.slnx || exit /b 1
call "%~dp0build-deploy-mod.cmd" WorkRoles WorkRoles.slnx || exit /b 1
echo All mods built and deployed.
exit /b 0
