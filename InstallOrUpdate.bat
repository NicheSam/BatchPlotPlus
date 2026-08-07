@echo off
setlocal EnableExtensions
title BatchPlotPlus 1.3.6 Installer

set "SOURCE=%~dp0release\BatchPlotPlus.bundle"
set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"

if not defined APPDATA (
  echo ERROR: APPDATA is not available.
  pause
  exit /b 1
)

if not exist "%SOURCE%\PackageContents.xml" (
  echo ERROR: Release manifest was not found:
  echo %SOURCE%\PackageContents.xml
  pause
  exit /b 2
)

if not exist "%SOURCE%\Contents\BatchPlotPlus.AutoCAD.dll" (
  echo ERROR: Release DLL was not found:
  echo %SOURCE%\Contents\BatchPlotPlus.AutoCAD.dll
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

if not exist "%TARGET%\Contents" (
  mkdir "%TARGET%\Contents"
  if errorlevel 1 (
    echo ERROR: Could not create the target folder:
    echo %TARGET%\Contents
    goto :copy_failed
  )
)

copy /Y "%SOURCE%\PackageContents.xml" "%TARGET%\PackageContents.xml" >NUL
if errorlevel 1 (
  echo ERROR: Could not copy PackageContents.xml.
  goto :copy_failed
)
copy /Y "%SOURCE%\README.txt" "%TARGET%\README.txt" >NUL
if errorlevel 1 (
  echo ERROR: Could not copy README.txt.
  goto :copy_failed
)
copy /Y "%SOURCE%\THIRD_PARTY_NOTICES.txt" "%TARGET%\THIRD_PARTY_NOTICES.txt" >NUL
if errorlevel 1 (
  echo ERROR: Could not copy THIRD_PARTY_NOTICES.txt.
  goto :copy_failed
)
copy /Y "%SOURCE%\Contents\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 (
  echo ERROR: Could not copy BatchPlotPlus.AutoCAD.dll.
  goto :copy_failed
)

del /Q "%TARGET%\Contents\BatchPlotPlus.lsp" 2>NUL
del /Q "%TARGET%\Contents\BatchPlotPlus.dcl" 2>NUL
del /Q "%TARGET%\Contents\PdfMerge.exe" 2>NUL

fc /B "%SOURCE%\Contents\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 (
  echo ERROR: Deployed DLL does not match the release DLL.
  pause
  exit /b 5
)

findstr /C:"AppVersion=\"1.3.6\"" "%TARGET%\PackageContents.xml" >NUL
if errorlevel 1 (
  echo ERROR: The deployed manifest is not version 1.3.6.
  pause
  exit /b 6
)

echo.
echo BatchPlotPlus 1.3.6 was deployed successfully.
echo Target: %TARGET%
echo Restart AutoCAD, then use the Batch Plot Tools ribbon tab.
echo Fallback commands: BATCHPLOTPLUS, BATCHPDF, or BATCHWB.
echo.
pause
exit /b 0

:copy_failed
echo ERROR: Deployment failed while copying files.
echo Source: %SOURCE%
echo Target: %TARGET%
pause
exit /b 4
