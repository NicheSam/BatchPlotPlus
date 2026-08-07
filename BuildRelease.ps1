$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginProject = Join-Path $projectRoot "BatchPlotPlus.AutoCAD\BatchPlotPlus.AutoCAD.csproj"
$logicProject = Join-Path $projectRoot "LogicTests\BatchPlotPlus.LogicTests.csproj"
$bundle = Join-Path $projectRoot "Bundle\BatchPlotPlus.bundle"
$releaseRoot = Join-Path $projectRoot "release"
$releaseBundle = Join-Path $releaseRoot "BatchPlotPlus.bundle"
$releaseZip = Join-Path $releaseRoot "BatchPlotPlus-1.4.1.zip"
$installerZip = Join-Path $releaseRoot "BatchPlotPlus-1.4.1-installer.zip"
$installerStage = Join-Path $releaseRoot "_installer"
$r24Output = Join-Path $projectRoot "BatchPlotPlus.AutoCAD\bin\Release\net48\BatchPlotPlus.AutoCAD.dll"
$r25Output = Join-Path $projectRoot "BatchPlotPlus.AutoCAD\bin\Release\net8.0-windows\BatchPlotPlus.AutoCAD.dll"
$r24Bundle = Join-Path $bundle "Contents\R24\BatchPlotPlus.AutoCAD.dll"
$r25Bundle = Join-Path $bundle "Contents\R25\BatchPlotPlus.AutoCAD.dll"
$legacyBundleDll = Join-Path $bundle "Contents\BatchPlotPlus.AutoCAD.dll"

dotnet restore $pluginProject
if ($LASTEXITCODE -ne 0) { throw "Plugin restore failed." }
dotnet build $pluginProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed." }
dotnet build $logicProject -c Release
if ($LASTEXITCODE -ne 0) { throw "Logic test build failed." }
& (Join-Path $projectRoot "LogicTests\bin\Release\net48\BatchPlotPlus.LogicTests.exe")
if ($LASTEXITCODE -ne 0) { throw "Logic tests failed." }

New-Item -ItemType Directory -Path (Split-Path -Parent $r24Bundle) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $r25Bundle) -Force | Out-Null
if (Test-Path -LiteralPath $legacyBundleDll) { Remove-Item -LiteralPath $legacyBundleDll -Force }
Copy-Item -LiteralPath $r24Output -Destination $r24Bundle -Force
Copy-Item -LiteralPath $r25Output -Destination $r25Bundle -Force

python (Join-Path $projectRoot "tools\validate_bundle.py")
if ($LASTEXITCODE -ne 0) { throw "Bundle validation failed." }

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
if (Test-Path -LiteralPath $releaseBundle) { Remove-Item -LiteralPath $releaseBundle -Recurse -Force }
Copy-Item -LiteralPath $bundle -Destination $releaseBundle -Recurse
if (Test-Path -LiteralPath $releaseZip) { Remove-Item -LiteralPath $releaseZip -Force }
Compress-Archive -LiteralPath $releaseBundle -DestinationPath $releaseZip -CompressionLevel Optimal
if (Test-Path -LiteralPath $installerStage) { Remove-Item -LiteralPath $installerStage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $installerStage "release") -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot "InstallOrUpdate.bat") -Destination $installerStage -Force
Copy-Item -LiteralPath $releaseBundle -Destination (Join-Path $installerStage "release") -Recurse
if (Test-Path -LiteralPath $installerZip) { Remove-Item -LiteralPath $installerZip -Force }
Compress-Archive -Path (Join-Path $installerStage "*") -DestinationPath $installerZip -CompressionLevel Optimal
Remove-Item -LiteralPath $installerStage -Recurse -Force

Write-Output "BatchPlotPlus 1.4.1 release created."
Write-Output $releaseZip
Write-Output $installerZip
