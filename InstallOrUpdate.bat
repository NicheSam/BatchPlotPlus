@echo off
setlocal EnableExtensions
title BatchPlotPlus 1.4.0 Installer

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

if not exist "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" (
  echo ERROR: AutoCAD 2021-2024 release DLL was not found:
  echo %SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll
  pause
  exit /b 2
)

if not exist "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" (
  echo ERROR: AutoCAD 2025 release DLL was not found:
  echo %SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll
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

if not exist "%TARGET%\Contents\R24" (
  mkdir "%TARGET%\Contents\R24"
  if errorlevel 1 (
    echo ERROR: Could not create the target folder:
    echo %TARGET%\Contents\R24
    goto :copy_failed
  )
)
if not exist "%TARGET%\Contents\R25" (
  mkdir "%TARGET%\Contents\R25"
  if errorlevel 1 (
    echo ERROR: Could not create the target folder:
    echo %TARGET%\Contents\R25
    goto :copy_failed
  )
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
copy /Y "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R24\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 (
  echo ERROR: Could not copy the AutoCAD 2021-2024 DLL.
  goto :copy_failed
)
copy /Y "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R25\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 (
  echo ERROR: Could not copy the AutoCAD 2025 DLL.
  goto :copy_failed
)
copy /Y "%SOURCE%\PackageContents.xml" "%TARGET%\PackageContents.xml" >NUL
if errorlevel 1 (
  echo ERROR: Could not activate the new PackageContents.xml.
  goto :copy_failed
)

del /Q "%TARGET%\Contents\BatchPlotPlus.lsp" 2>NUL
del /Q "%TARGET%\Contents\BatchPlotPlus.dcl" 2>NUL
del /Q "%TARGET%\Contents\PdfMerge.exe" 2>NUL
del /Q "%TARGET%\Contents\BatchPlotPlus.AutoCAD.dll" 2>NUL

fc /B "%SOURCE%\Contents\R24\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R24\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 (
  echo ERROR: Deployed AutoCAD 2021-2024 DLL does not match the release DLL.
  pause
  exit /b 5
)
fc /B "%SOURCE%\Contents\R25\BatchPlotPlus.AutoCAD.dll" "%TARGET%\Contents\R25\BatchPlotPlus.AutoCAD.dll" >NUL
if errorlevel 1 (
  echo ERROR: Deployed AutoCAD 2025 DLL does not match the release DLL.
  pause
  exit /b 5
)

findstr /C:"AppVersion=\"1.4.0\"" "%TARGET%\PackageContents.xml" >NUL
if errorlevel 1 (
  echo ERROR: The deployed manifest is not version 1.4.0.
  pause
  exit /b 6
)
findstr /C:"ModuleName=\"./Contents/R24/BatchPlotPlus.AutoCAD.dll\"" "%TARGET%\PackageContents.xml" >NUL
if errorlevel 1 (
  echo ERROR: The deployed manifest does not route AutoCAD 2021-2024 correctly.
  pause
  exit /b 6
)
findstr /C:"ModuleName=\"./Contents/R25/BatchPlotPlus.AutoCAD.dll\"" "%TARGET%\PackageContents.xml" >NUL
if errorlevel 1 (
  echo ERROR: The deployed manifest does not route AutoCAD 2025 correctly.
  pause
  exit /b 6
)

echo.
echo BatchPlotPlus 1.4.0 was deployed successfully.
echo Supported hosts: AutoCAD 2021 through AutoCAD 2025, Windows 64-bit.
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
