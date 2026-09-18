param(
    [switch]$Elevated,
    [switch]$ValidateOnly,
    [string]$OriginalAppData,
    [string]$LogPath
)

$ErrorActionPreference = "Stop"
$version = "1.5.3"
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $env:TEMP "BatchPlotPlus-install.log"
}
if ([string]::IsNullOrWhiteSpace($OriginalAppData)) {
    $OriginalAppData = $env:APPDATA
}

function Write-InstallLog([string]$message) {
    $line = (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff") + " " + $message
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $message
}

function Test-IsAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Quote-ProcessArgument([string]$value) {
    return '"' + $value.Replace('"', '\"') + '"'
}

function Verify-BundleUnblocked([string]$path, [switch]$Repair) {
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "VerifyUnblocked.ps1"), "-Path", $path)
    if ($Repair) { $arguments += "-Repair" }
    & powershell.exe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Blocked-file verification failed with exit code $LASTEXITCODE for: $path"
    }
}

function Assert-BundleMatch([string]$source, [string]$destination) {
    $sourceFiles = @(Get-ChildItem -LiteralPath $source -Recurse -File | Sort-Object { $_.FullName.Substring($source.Length) })
    $destinationFiles = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Sort-Object { $_.FullName.Substring($destination.Length) })
    if ($sourceFiles.Count -ne $destinationFiles.Count) {
        throw "Bundle file count mismatch: source=$($sourceFiles.Count), destination=$($destinationFiles.Count)."
    }
    for ($index = 0; $index -lt $sourceFiles.Count; $index++) {
        $sourceRelative = $sourceFiles[$index].FullName.Substring($source.Length)
        $destinationRelative = $destinationFiles[$index].FullName.Substring($destination.Length)
        if ($sourceRelative -ne $destinationRelative) {
            throw "Bundle file list mismatch: $sourceRelative versus $destinationRelative."
        }
        $sourceHash = (Get-FileHash -LiteralPath $sourceFiles[$index].FullName -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $destinationFiles[$index].FullName -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "Bundle hash mismatch: $sourceRelative."
        }
    }
}

function Assert-Manifest([string]$bundle) {
    $manifestPath = Join-Path $bundle "PackageContents.xml"
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
    if ([string]$manifest.ApplicationPackage.AppVersion -ne $version) {
        throw "Manifest version is not $version."
    }
    $entries = @($manifest.ApplicationPackage.Components.ComponentEntry)
    if ($entries.Count -ne 2) { throw "Manifest must contain two version-routed components." }
    foreach ($entry in $entries) {
        if ([string]$entry.LoadOnAutoCADStartup -ne "True") {
            throw "$($entry.AppName) does not explicitly enable LoadOnAutoCADStartup."
        }
        if ([string]$entry.LoadOnCommandInvocation -ne "True") {
            throw "$($entry.AppName) does not explicitly enable LoadOnCommandInvocation."
        }
        if ($null -ne $entry.LoadReasons) {
            throw "$($entry.AppName) still uses ambiguous LoadReasons."
        }
        $commands = @($entry.Commands.Command | ForEach-Object { [string]$_.Global })
        foreach ($command in @("BATCHPLOTPLUS", "BATCHPDF", "BATCHWB", "BATCHPLOTDIAG", "BATCHFONTS")) {
            if ($commands -notcontains $command) { throw "$($entry.AppName) does not declare $command." }
        }
    }
}

function Restore-PreviousBundle([string]$target, [string]$backup) {
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    if (Test-Path -LiteralPath $backup) {
        Move-Item -LiteralPath $backup -Destination $target
        Write-InstallLog "Previous bundle restored."
    }
}

function Backup-AndRemoveLegacyLoaders([string]$appData, [string]$target) {
    $root = "HKCU:\Software\Autodesk\AutoCAD"
    if (-not (Test-Path $root)) { return }
    $legacyRoots = @(
        (Join-Path $appData "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"),
        (Join-Path $env:ProgramFiles "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle")
    )
    if (${env:ProgramFiles(x86)}) {
        $legacyRoots += Join-Path ${env:ProgramFiles(x86)} "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"
    }
    $legacyRoots = @($legacyRoots | Where-Object { $_ -and $_ -ne $target })
    $keys = @(Get-ChildItem $root -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.PSChildName -in @("BatchPlotPlus.R24", "BatchPlotPlus.R25")
    })
    foreach ($key in $keys) {
        $loader = [string](Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction Stop).LOADER
        $isLegacy = $false
        foreach ($legacyRoot in $legacyRoots) {
            if ($loader.StartsWith($legacyRoot, [StringComparison]::OrdinalIgnoreCase)) { $isLegacy = $true; break }
        }
        if (-not $isLegacy) { continue }
        $safeName = ($key.Name -replace '[^A-Za-z0-9._-]', '_')
        $backupPath = Join-Path $env:TEMP ("BatchPlotPlus-loader-backup-" + $safeName + ".reg")
        & reg.exe export $key.Name $backupPath /y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not back up legacy Loader key: $($key.Name)" }
        Remove-Item -LiteralPath $key.PSPath -Recurse -Force
        Write-InstallLog "Removed stale Loader after backup: $($key.Name)"
    }
}


function Ensure-LoaderRegistrations([string]$target) {
    $root = "HKCU:\Software\Autodesk\AutoCAD"
    if (-not (Test-Path $root)) { return }
    $productRoots = @(Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
        $seriesKey = $_
        Get-ChildItem $seriesKey.PSPath -ErrorAction SilentlyContinue | Where-Object {
            $seriesKey.PSChildName -in @("R24.0", "R24.1", "R24.2", "R24.3", "R25.0")
        } | ForEach-Object {
            [pscustomobject]@{ Series = $seriesKey.PSChildName; Path = $_.PSPath }
        }
    })
    foreach ($product in $productRoots) {
        if ($product.Series -like "R24.*") {
            $appName = "BatchPlotPlus.R24"
            $loader = Join-Path $target "Contents\R24\BatchPlotPlus.AutoCAD.dll"
        } elseif ($product.Series -eq "R25.0") {
            $appName = "BatchPlotPlus.R25"
            $loader = Join-Path $target "Contents\R25\BatchPlotPlus.AutoCAD.dll"
        } else {
            continue
        }
        if (-not (Test-Path -LiteralPath $loader -PathType Leaf)) { continue }
        $appsPath = Join-Path $product.Path "Applications"
        $appPath = Join-Path $appsPath $appName
        New-Item -Path $appPath -Force | Out-Null
        New-ItemProperty -Path $appPath -Name "LOADER" -PropertyType String -Value $loader -Force | Out-Null
        New-ItemProperty -Path $appPath -Name "LOADCTRLS" -PropertyType DWord -Value 14 -Force | Out-Null
        New-ItemProperty -Path $appPath -Name "DESCRIPTION" -PropertyType String -Value $appName -Force | Out-Null
        New-ItemProperty -Path $appPath -Name "MANAGED" -PropertyType DWord -Value 1 -Force | Out-Null
        $commandsPath = Join-Path $appPath "Commands"
        New-Item -Path $commandsPath -Force | Out-Null
        foreach ($command in @("BATCHPLOTPLUS", "BATCHPDF", "BATCHWB", "BATCHPLOTDIAG", "BATCHFONTS")) {
            New-ItemProperty -Path $commandsPath -Name $command -PropertyType String -Value $command -Force | Out-Null
        }
        $groupsPath = Join-Path $appPath "Groups"
        New-Item -Path $groupsPath -Force | Out-Null
        New-ItemProperty -Path $groupsPath -Name "BatchPlotPlus.Commands" -PropertyType String -Value "BatchPlotPlus.Commands" -Force | Out-Null
        Write-InstallLog "Ensured startup Loader registration: $appPath"
    }
}
function Assert-SourceBundle([string]$source) {
    foreach ($required in @(
        (Join-Path $source "PackageContents.xml"),
        (Join-Path $source "Contents\R24\BatchPlotPlus.AutoCAD.dll"),
        (Join-Path $source "Contents\R25\BatchPlotPlus.AutoCAD.dll")
    )) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Required release file is missing: $required"
        }
    }
    Assert-Manifest $source
    Verify-BundleUnblocked $source -Repair
}

if (-not $Elevated) {
    if (Test-Path -LiteralPath $LogPath) { Remove-Item -LiteralPath $LogPath -Force }
    Write-InstallLog "BatchPlotPlus $version installation started."
}

if ($ValidateOnly) {
    try {
        Assert-SourceBundle (Join-Path $PSScriptRoot "release\BatchPlotPlus.bundle")
        Write-InstallLog "Validation-only check passed. No system files or registry entries were changed."
        exit 0
    } catch {
        Write-InstallLog ("Validation-only check failed: " + $_.Exception.Message)
        exit 40
    }
}

if (-not $Elevated) {
    if (-not [string]::Equals($OriginalAppData, $env:APPDATA, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Run the installer as the AutoCAD user; do not substitute another account."
    }
    & (Join-Path $PSScriptRoot "MigrateCadFontAuto.ps1") -ValidateOnly
}

if (-not (Test-IsAdministrator)) {
    Write-InstallLog "Requesting administrator permission for ProgramData deployment."
    $argumentLine = @(
        "-NoProfile",
        "-ExecutionPolicy Bypass",
        "-File " + (Quote-ProcessArgument $PSCommandPath),
        "-Elevated",
        "-OriginalAppData " + (Quote-ProcessArgument $OriginalAppData),
        "-LogPath " + (Quote-ProcessArgument $LogPath)
    ) -join " "
    try {
        $elevatedProcess = Start-Process -FilePath "powershell.exe" -ArgumentList $argumentLine -Verb RunAs -WindowStyle Hidden -Wait -PassThru
    } catch {
        Write-InstallLog ("Elevation was cancelled or failed: " + $_.Exception.Message)
        exit 1
    }
    if ($elevatedProcess.ExitCode -ne 0) {
        Write-InstallLog "Elevated installer failed with exit code $($elevatedProcess.ExitCode)."
        exit $elevatedProcess.ExitCode
    }
    try {
        $target = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"
        Backup-AndRemoveLegacyLoaders $OriginalAppData $target
        Ensure-LoaderRegistrations $target
        & (Join-Path $PSScriptRoot "MigrateCadFontAuto.ps1")
        Write-InstallLog "Deployment verified; AutoCAD runtime verification is pending."
        exit 0
    } catch {
        Write-InstallLog ("Post-install cleanup failed: " + $_.Exception.Message)
        exit 30
    }
}

$pluginRoot = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins"
$source = Join-Path $PSScriptRoot "release\BatchPlotPlus.bundle"
$target = Join-Path $pluginRoot "BatchPlotPlus.bundle"
$stage = Join-Path $pluginRoot "BatchPlotPlus.bundle.new"
$backup = Join-Path $pluginRoot "BatchPlotPlus.bundle.previous"
$activated = $false

try {
    if (Get-Process -Name acad -ErrorAction SilentlyContinue) {
        throw "AutoCAD is running. Close every AutoCAD window and run the installer again."
    }
    Assert-SourceBundle $source
    New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    Copy-Item -LiteralPath $source -Destination $stage -Recurse
    Verify-BundleUnblocked $stage -Repair
    Assert-BundleMatch $source $stage
    Assert-Manifest $stage

    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
    if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $backup }
    Move-Item -LiteralPath $stage -Destination $target
    $activated = $true
    Verify-BundleUnblocked $target -Repair
    Assert-BundleMatch $source $target
    Assert-Manifest $target

    $legacyBundles = @(
        (Join-Path $OriginalAppData "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"),
        (Join-Path $env:ProgramFiles "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle")
    )
    if (${env:ProgramFiles(x86)}) {
        $legacyBundles += Join-Path ${env:ProgramFiles(x86)} "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"
    }
    foreach ($legacy in $legacyBundles | Select-Object -Unique) {
        if ($legacy -and $legacy -ne $target -and (Test-Path -LiteralPath $legacy)) {
            Remove-Item -LiteralPath $legacy -Recurse -Force
            Write-InstallLog "Removed legacy bundle: $legacy"
        }
    }
    Write-InstallLog "Bundle deployed and statically verified: $target"
    if ($Elevated) { exit 0 }
    Backup-AndRemoveLegacyLoaders $OriginalAppData $target
    Ensure-LoaderRegistrations $target
    if (-not $Elevated) { & (Join-Path $PSScriptRoot "MigrateCadFontAuto.ps1") }
    Write-InstallLog "Deployment verified; AutoCAD runtime verification is pending."
    exit 0
} catch {
    Write-InstallLog ("Installation failed: " + $_.Exception.Message)
    if ($activated) {
        try { Restore-PreviousBundle $target $backup } catch { Write-InstallLog ("Rollback failed: " + $_.Exception.Message) }
    }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    exit 20
}
