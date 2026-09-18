$ErrorActionPreference = "Continue"
$report = New-Object System.Collections.Generic.List[string]
$errors = 0
$warnings = 0

function Add-Line([string]$text) {
    $script:report.Add($text)
    Write-Host $text
}

function Add-Pass([string]$text) { Add-Line "[PASS] $text" }
function Add-Warn([string]$text) { $script:warnings++; Add-Line "[WARN] $text" }
function Add-Fail([string]$text) { $script:errors++; Add-Line "[FAIL] $text" }

Add-Line "BatchPlotPlus installation diagnostic"
Add-Line ("Generated: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"))
Add-Line ("Windows: " + [Environment]::OSVersion.VersionString)
Add-Line ""

$trustedBundle = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"
$programFilesBundle = Join-Path $env:ProgramFiles "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle"
$programFilesX86Bundle = if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle" } else { "" }
$userBundle = if ($env:APPDATA) { Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle" } else { "" }
$allUsersBundle = if ($env:ProgramData) { Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\BatchPlotPlus.bundle" } else { "" }
$locations = @($trustedBundle, $programFilesBundle, $programFilesX86Bundle, $userBundle, $allUsersBundle) | Where-Object { $_ } | Select-Object -Unique
$found = @($locations | Where-Object { Test-Path -LiteralPath $_ })

if (Test-Path -LiteralPath $trustedBundle) {
    Add-Pass "Trusted ProgramData bundle exists."
} else {
    Add-Fail "Trusted ProgramData bundle is missing: $trustedBundle"
}
if ($found.Count -gt 1) {
    Add-Warn ("Duplicate bundle locations found: " + ($found -join " | "))
}

foreach ($bundle in $found) {
    Add-Line ""
    Add-Line "Bundle: $bundle"
    $manifestPath = Join-Path $bundle "PackageContents.xml"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Add-Fail "PackageContents.xml is missing."
        continue
    }
    try {
        [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
        $version = $manifest.ApplicationPackage.AppVersion
        Add-Line "Version: $version"
        if ($version -eq "1.5.1") { Add-Pass "Manifest version is 1.5.1." } else { Add-Warn "Manifest is not version 1.5.1." }
        $bundleFiles = @(Get-ChildItem -LiteralPath $bundle -Recurse -File -ErrorAction Stop)
        $blockedFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
        foreach ($bundleFile in $bundleFiles) {
            try {
                $streams = @(Get-Item -LiteralPath $bundleFile.FullName -Stream * -ErrorAction Stop)
                if ($streams.Stream -contains "Zone.Identifier") { $blockedFiles.Add($bundleFile) }
            } catch {
                Add-Fail "Could not inspect alternate data streams: $($bundleFile.FullName) -- $($_.Exception.Message)"
            }
        }
        if ($blockedFiles.Count -eq 0) {
            Add-Pass "All $($bundleFiles.Count) bundle files are free of Zone.Identifier."
        } else {
            foreach ($blockedFile in $blockedFiles) {
                Add-Fail "Blocked bundle file: $($blockedFile.FullName)"
            }
        }
        $entries = @($manifest.ApplicationPackage.Components.ComponentEntry)
        if ($entries.Count -ne 2) { Add-Fail "Expected two version-routed components; found $($entries.Count)." }
        foreach ($entry in $entries) {
            $module = [string]$entry.ModuleName
            $modulePath = Join-Path $bundle ($module.TrimStart(".").TrimStart("/").Replace("/", "\"))
            if (Test-Path -LiteralPath $modulePath) {
                $item = Get-Item -LiteralPath $modulePath
                Add-Pass "$($entry.AppName) DLL exists ($($item.Length) bytes)."
            } else {
                Add-Fail "$($entry.AppName) DLL is missing: $modulePath"
            }
            if ([string]$entry.LoadOnAutoCADStartup -ne "True") { Add-Fail "$($entry.AppName) does not explicitly enable LoadOnAutoCADStartup." }
            if ([string]$entry.LoadOnCommandInvocation -ne "True") { Add-Fail "$($entry.AppName) does not explicitly enable LoadOnCommandInvocation." }
            if ($null -ne $entry.LoadReasons) { Add-Fail "$($entry.AppName) still uses ambiguous LoadReasons." }
            $commands = @($entry.Commands.Command | ForEach-Object { [string]$_.Global })
            foreach ($required in @("BATCHPLOTPLUS", "BATCHPDF", "BATCHWB", "BATCHPLOTDIAG")) {
                if ($commands -notcontains $required) { Add-Fail "$($entry.AppName) does not declare $required." }
            }
        }
    } catch {
        Add-Fail ("Manifest cannot be parsed: " + $_.Exception.Message)
    }
}

Add-Line ""
$acadProcesses = @(Get-Process -Name acad -ErrorAction SilentlyContinue)
if ($acadProcesses.Count -gt 0) { Add-Warn "AutoCAD is currently running; restart is required after installation." }
else { Add-Pass "AutoCAD is closed." }

$installed = @()
foreach ($root in @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
)) {
    $installed += Get-ItemProperty -Path $root -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -match "^AutoCAD|Autodesk AutoCAD" } |
        ForEach-Object { ([string]$_.DisplayName).Split([char]0)[0] }
}
$installed = @($installed | Sort-Object -Unique)
if ($installed.Count -eq 0) { Add-Warn "No AutoCAD installation was found in standard uninstall registry keys." }
else { Add-Line ("Detected products: " + ($installed -join " | ")) }

$settings = @()
if (Test-Path "HKCU:\Software\Autodesk\AutoCAD") {
    $settings = Get-ChildItem "HKCU:\Software\Autodesk\AutoCAD" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $value = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
        if ($null -ne $value.APPAUTOLOAD -or $null -ne $value.SECURELOAD) {
            [pscustomobject]@{
                Path = $_.Name
                APPAUTOLOAD = $value.APPAUTOLOAD
                SECURELOAD = $value.SECURELOAD
            }
        }
    }
}
if ($settings.Count -eq 0) {
    Add-Warn "No saved APPAUTOLOAD/SECURELOAD profile values were found; verify them inside AutoCAD."
} else {
    foreach ($setting in $settings) {
        Add-Line "Profile: $($setting.Path); APPAUTOLOAD=$($setting.APPAUTOLOAD); SECURELOAD=$($setting.SECURELOAD)"
        if ($null -ne $setting.APPAUTOLOAD -and ([int]$setting.APPAUTOLOAD -band 2) -eq 0) {
            Add-Warn "This profile does not enable startup plug-in loading."
        }
    }
}

$loaderKeys = @()
if (Test-Path "HKCU:\Software\Autodesk\AutoCAD") {
    $loaderKeys = @(Get-ChildItem "HKCU:\Software\Autodesk\AutoCAD" -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.PSChildName -in @("BatchPlotPlus.R24", "BatchPlotPlus.R25")
    })
}
if ($loaderKeys.Count -eq 0) {
    Add-Warn "No BatchPlotPlus Loader registration exists yet. Restart AutoCAD once, then run this diagnostic again."
} else {
    foreach ($loaderKey in $loaderKeys) {
        $loaderProperties = Get-ItemProperty -LiteralPath $loaderKey.PSPath -ErrorAction SilentlyContinue
        $loader = [string]$loaderProperties.LOADER
        $loadCtrls = $loaderProperties.LOADCTRLS
        if ([string]::IsNullOrWhiteSpace($loader)) {
            Add-Fail "Loader registration has no LOADER value: $($loaderKey.Name)"
        } elseif ($loader.StartsWith($trustedBundle, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $loader)) {
            Add-Pass "Loader points to the trusted v1.5.1 bundle: $loader"
        } else {
            Add-Fail "Loader points to a missing or legacy DLL: $loader"
        }
        if ($null -eq $loadCtrls) {
            Add-Fail "Loader registration has no LOADCTRLS value: $($loaderKey.Name)"
        } elseif ((([int]$loadCtrls -band 2) -eq 0) -or (([int]$loadCtrls -band 4) -eq 0)) {
            Add-Fail "Loader LOADCTRLS does not enable startup and command demand-load: $($loaderKey.Name)=$loadCtrls"
        } else {
            Add-Pass "Loader LOADCTRLS enables startup and command demand-load: $($loaderKey.Name)=$loadCtrls"
        }
    }
}

Add-Line ""
Add-Line "Static result: $errors failure(s), $warnings warning(s)."
if ($errors -eq 0) {
    Add-Pass "Static installation checks passed. Start AutoCAD and run BATCHPLOTDIAG to prove runtime loading."
} else {
    Add-Fail "Installation is incomplete or inconsistent. Re-run InstallOrUpdate.bat as administrator."
}

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
$reportPath = Join-Path $desktop ("BatchPlotPlus-diagnostic-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".txt")
$report | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host ""
Write-Host "Report: $reportPath"
if ($errors -gt 0) { exit 1 }
exit 0
