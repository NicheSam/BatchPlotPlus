@echo off
setlocal EnableExtensions
title BatchPlotPlus 1.4.2 Installer

set "SOURCE=%~dp0release\BatchPlotPlus.bundle"
set "PLUGIN_ROOT=%ProgramFiles%\Autodesk\ApplicationPlugins"
set "TARGET=%PLUGIN_ROOT%\BatchPlotPlus.bundle"
set "STAGE=%PLUGIN_ROOT%\BatchPlotPlus.bundle.new"
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

powershell.exe -NoProfile -Command "Get-ChildItem -LiteralPath '%SOURCE%' -Recurse -File -ErrorAction Stop | Unblock-File" >NUL 2>NUL
if errorlevel 1 (
  echo WARNING: Download blocking could not be cleared from every source file.
)

if not exist "%PLUGIN_ROOT%" mkdir "%PLUGIN_ROOT%"
if errorlevel 1 goto :copy_failed
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
xcopy "%SOURCE%" "%STAGE%\" /E /I /Y /Q >NUL
if errorlevel 1 goto :copy_failed

fc /B "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" "%STAGE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :verify_failed
fc /B "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" "%STAGE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :verify_failed
findstr /C:"AppVersion=\"1.4.2\"" "%STAGE%\PackageContents.xml" >NUL
if errorlevel 1 goto :verify_failed
findstr /C:"LoadOnAutoCADStartup=\"True\"" "%STAGE%\PackageContents.xml" >NUL
if errorlevel 1 goto :verify_failed
findstr /C:"LoadOnCommandInvocation=\"True\"" "%STAGE%\PackageContents.xml" >NUL
if errorlevel 1 goto :verify_failed

if exist "%TARGET%" rmdir /S /Q "%TARGET%"
move "%STAGE%" "%TARGET%" >NUL
if errorlevel 1 goto :copy_failed

if defined APPDATA if exist "%LEGACY_USER%" rmdir /S /Q "%LEGACY_USER%"
if defined ProgramData if exist "%LEGACY_ALL%" rmdir /S /Q "%LEGACY_ALL%"

powershell.exe -NoProfile -Command "Get-ChildItem -LiteralPath '%TARGET%' -Recurse -File -ErrorAction Stop | Unblock-File" >NUL 2>NUL
fc /B "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R24\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :verify_failed
fc /B "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R25\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 goto :verify_failed

echo.
echo BatchPlotPlus 1.4.2 was deployed successfully.
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

:verify_failed
echo ERROR: The staged or deployed files failed verification.
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
pause
exit /b 5
