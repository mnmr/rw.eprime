@echo off
setlocal
rem Build (Release) and deploy every mod present. Skips missing folders, stops on the first failure.
rem Lives in scripts\; ROOT resolves to the workspace root one level up.
for %%i in ("%~dp0..") do set ROOT=%%~fi\

call :one Implanner Implanner.slnx || exit /b 1
call :one QualityJobs QualityJobs.slnx || exit /b 1
call :one Readouts EPrimeReadouts.slnx || exit /b 1
call :one WorkRoles WorkRoles.slnx || exit /b 1
echo All mods built and deployed.
exit /b 0

:one
if not exist "%ROOT%%1\" (
    echo === %1: not present, skipping ===
    exit /b 0
)
echo === %1: build ===
dotnet build -c Release "%ROOT%%1\src\%2" || (echo %1 build FAILED & exit /b 1)
echo === %1: deploy ===
pwsh -NoProfile -File "%ROOT%%1\scripts\deploy.ps1" || (echo %1 deploy FAILED & exit /b 1)
exit /b 0
