@echo off
setlocal
rem Build (Release, 1.6 and 1.7) and deploy one mod: build-deploy-mod.cmd <ModFolder> <Solution>
rem Lives in scripts\; ROOT resolves to the workspace root one level up.
for %%i in ("%~dp0..") do set ROOT=%%~fi\

if not exist "%ROOT%%1\" (
    echo === %1: not present, skipping ===
    exit /b 0
)
for %%v in (1.6 1.7) do (
    echo === %1: build %%v ===
    dotnet build -c Release -p:RimWorldVersion=%%v "%ROOT%%1\src\%2" || (echo %1 %%v build FAILED & exit /b 1)
)
echo === %1: deploy ===
pwsh -NoProfile -File "%ROOT%%1\scripts\deploy.ps1" || (echo %1 deploy FAILED & exit /b 1)
exit /b 0
