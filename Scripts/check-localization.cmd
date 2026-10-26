@echo off
setlocal
rem Offline check; nonzero exit means localization is not ready to publish.
python "%~dp0..\Shared\tools\localization\translations.py" check %*
exit /b %errorlevel%
