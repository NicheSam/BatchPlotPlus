@echo off
setlocal
title BatchPlotPlus Installation Diagnostic
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DiagnoseInstallation.ps1"
echo.
pause
