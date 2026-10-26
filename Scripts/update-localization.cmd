@echo off
setlocal
rem Translate changed content with the signed-in Codex CLI, then validate it.
python "%~dp0..\Shared\tools\localization\update_translations.py" %*
exit /b %errorlevel%
