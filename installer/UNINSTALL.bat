@echo off
setlocal
title Chained Beasts - Uninstall Mods
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Uninstall.ps1" %*
set "UNINSTALL_RESULT=%ERRORLEVEL%"
echo.
if not "%UNINSTALL_RESULT%"=="0" echo Uninstall did not finish. Read the message above before trying again.
pause
exit /b %UNINSTALL_RESULT%
