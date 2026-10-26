@echo off
setlocal
title Chained Beasts - Install Mods
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install.ps1" %*
set "INSTALL_RESULT=%ERRORLEVEL%"
echo.
if not "%INSTALL_RESULT%"=="0" echo Installation did not finish. Read the message above before trying again.
pause
exit /b %INSTALL_RESULT%
