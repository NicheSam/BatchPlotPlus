@echo off
setlocal EnableExtensions DisableDelayedExpansion
title BatchPlotPlus 1.4.3 Installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0InstallOrUpdate.ps1" %*
set "INSTALL_EXIT=%ERRORLEVEL%"
echo.
echo Installation log: %TEMP%\BatchPlotPlus-install.log
if not defined BATCHPLOTPLUS_NO_PAUSE pause
exit /b %INSTALL_EXIT%
