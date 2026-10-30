@echo off
setlocal
rem Translate changed content with the signed-in Claude Code CLI (Sonnet), then validate it.
python "%~dp0..\Shared\tools\localization\update_translations.py" %*
exit /b %errorlevel%
