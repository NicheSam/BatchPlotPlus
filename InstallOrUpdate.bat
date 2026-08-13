@echo off
setlocal EnableExtensions
title BatchPlotPlus 1.4.21 Installer

set "SOURCE=%~dp0release\BatchPlotPlus.bundle"
set "PLUGIN_ROOT=%ProgramFiles%\Autodesk\ApplicationPlugins"
set "TARGET=%PLUGIN_ROOT%\BatchPlotPlus.bundle"
set "STAGE=%PLUGIN_ROOT%\BatchPlotPlus.bundle.new"
set "BACKUP=%PLUGIN_ROOT%\BatchPlotPlus.bundle.previous"
set "LEGACY_USER=%APPDATA%\Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"
set "LEGACY_ALL=%ProgramData%\Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"

powershell.exe -NoProfile -Command "$p=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent(); if($p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){exit 0}else{exit 1}" >NUL 2>NUL
if errorlevel 1 (
  echo Administrator permission is required to install to AutoCAD's trusted plug-in folder.
  powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  if errorlevel 1 (
    echo ERROR: Elevation was cancelled or failed.
    pause
    exit /b 1
  )
  exit /b 0
)

if not exist "%SOURCE%\PackageContents.xml" (
  echo ERROR: Release manifest was not found. Extract the entire installer ZIP first.
  echo %SOURCE%\PackageContents.xml
  pause
  exit /b 2
)
if not exist "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" (
  echo ERROR: AutoCAD 2021-2024 DLL was not found. Extract the entire installer ZIP first.
  pause
  exit /b 2
)
if not exist "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" (
  echo ERROR: AutoCAD 2025 DLL was not found. Extract the entire installer ZIP first.
  pause
  exit /b 2
)

powershell.exe -NoProfile -Command "if (Get-Process -Name acad -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }" >NUL 2>NUL
if not errorlevel 1 (
  echo ERROR: AutoCAD is running.
  echo Close every AutoCAD window, then run this installer again.
  pause
  exit /b 3
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VerifyUnblocked.ps1" -Path "%SOURCE%" -Repair
if errorlevel 1 goto :source_blocked

if not exist "%PLUGIN_ROOT%" mkdir "%PLUGIN_ROOT%"
if errorlevel 1 goto :copy_failed
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
xcopy "%SOURCE%" "%STAGE%\" /E /I /Y /Q >NUL
if errorlevel 1 goto :copy_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VerifyUnblocked.ps1" -Path "%STAGE%" -Repair
if errorlevel 1 goto :stage_blocked

fc /B "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" "%STAGE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :verify_failed
fc /B "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" "%STAGE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :verify_failed
findstr /C:"AppVersion=\"1.4.21\"" "%STAGE%\PackageContents.xml" >NUL
if errorlevel 1 goto :verify_failed
findstr /C:"LoadOnAutoCADStartup=\"True\"" "%STAGE%\PackageContents.xml" >NUL
if errorlevel 1 goto :verify_failed
findstr /C:"LoadOnCommandInvocation=\"True\"" "%STAGE%\PackageContents.xml" >NUL
if errorlevel 1 goto :verify_failed

if exist "%BACKUP%" rmdir /S /Q "%BACKUP%"
if exist "%TARGET%" (
  move "%TARGET%" "%BACKUP%" >NUL
  if errorlevel 1 goto :copy_failed
)
move "%STAGE%" "%TARGET%" >NUL
if errorlevel 1 goto :activate_failed

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VerifyUnblocked.ps1" -Path "%TARGET%" -Repair
if errorlevel 1 goto :target_blocked

if defined APPDATA if exist "%LEGACY_USER%" rmdir /S /Q "%LEGACY_USER%"
if defined ProgramData if exist "%LEGACY_ALL%" rmdir /S /Q "%LEGACY_ALL%"

fc /B "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R24\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :target_verify_failed
fc /B "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R25\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :target_verify_failed

echo.
if exist "%BACKUP%" rmdir /S /Q "%BACKUP%"

echo BatchPlotPlus 1.4.21 was deployed successfully.
echo Trusted target: %TARGET%
echo Restart AutoCAD. The Batch Plot Tools ribbon tab should appear.
echo Fallback commands: BATCHPLOTPLUS, BATCHPDF, BATCHWB, BATCHPLOTDIAG.
echo If loading still fails, run DiagnoseInstallation.bat and send the report.
echo.
pause
exit /b 0

:copy_failed
echo ERROR: Deployment failed while copying files.
echo Source: %SOURCE%
echo Target: %TARGET%
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
pause
exit /b 4

:source_blocked
echo ERROR: One or more release files could not be unblocked.
echo Installation stopped before any deployed files were changed.
pause
exit /b 6

:stage_blocked
echo ERROR: One or more staged files still contain Zone.Identifier.
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
pause
exit /b 7

:activate_failed
echo ERROR: The verified staged bundle could not be activated.
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
if exist "%BACKUP%" move "%BACKUP%" "%TARGET%" >NUL
pause
exit /b 8

:target_blocked
echo ERROR: One or more deployed files still contain Zone.Identifier.
echo The unsafe deployment will be removed and the previous version restored when available.
if exist "%TARGET%" rmdir /S /Q "%TARGET%"
if exist "%BACKUP%" move "%BACKUP%" "%TARGET%" >NUL
pause
exit /b 9

:target_verify_failed
echo ERROR: The deployed bundle failed its final integrity check.
echo The invalid deployment will be removed and the previous version restored when available.
if exist "%TARGET%" rmdir /S /Q "%TARGET%"
if exist "%BACKUP%" move "%BACKUP%" "%TARGET%" >NUL
pause
exit /b 10

:verify_failed
echo ERROR: The staged or deployed files failed verification.
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
pause
exit /b 5
