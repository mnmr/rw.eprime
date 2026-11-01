@echo off
setlocal EnableDelayedExpansion
rem Publish every mod present to the Steam Workshop. Each mod shows its plan
rem (version, change note from CHANGELOG.md, tag/description/preview changes)
rem and asks for y/yes. A version already in the mod's RELEASES.md, or already
rem the latest Workshop update, is skipped. A declined or failed publish moves
rem on to the next mod; the summary lists each outcome. Build and deploy first.
for %%i in ("%~dp0..") do set ROOT=%%~fi\
set SUMMARY=
set FAILED=0

for %%m in (Implanner QualityJobs Readouts WorkRoles) do call :one %%m

echo.
echo === Summary ===
for %%s in (!SUMMARY!) do echo   %%~s
exit /b %FAILED%

:one
if not exist "%ROOT%%1\" (
    set SUMMARY=!SUMMARY! "%1: not present"
    exit /b 0
)
echo.
echo === %1: publish ===
pwsh -NoProfile -File "%ROOT%%1\scripts\publish.ps1"
set CODE=%ERRORLEVEL%
if %CODE%==0 (set SUMMARY=!SUMMARY! "%1: published or already up to date") else if %CODE%==2 (set SUMMARY=!SUMMARY! "%1: declined") else (set SUMMARY=!SUMMARY! "%1: FAILED (exit %CODE%)" & set FAILED=1)
exit /b 0
