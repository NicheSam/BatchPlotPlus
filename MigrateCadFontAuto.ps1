param([switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
# Run as the AutoCAD user, never as a substituted administrator account.
$legacy = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\CadFontAuto.bundle'
$cadRoot = 'HKCU:\Software\Autodesk\AutoCAD'
$keys = @()
if (Test-Path -LiteralPath $cadRoot) {
    $keys = @(Get-ChildItem -LiteralPath $cadRoot -Recurse | Where-Object {
        $_.PSChildName -eq 'CadFontAuto' -and $_.Name -match '\\Applications\\CadFontAuto$'
    })
}
foreach ($key in $keys) {
    $loader = [string](Get-ItemProperty -LiteralPath $key.PSPath).LOADER
    $expected = Join-Path $legacy 'Contents\Windows\CadFontAuto.dll'
    if (-not [string]::Equals([IO.Path]::GetFullPath($loader), [IO.Path]::GetFullPath($expected), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unrecognized CadFontAuto loader; migration stopped: $loader"
    }
}
if (Test-Path -LiteralPath $legacy) {
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $legacy 'PackageContents.xml') -Raw -Encoding UTF8
    if ($manifest.ApplicationPackage.ProductCode -ne '{8A9D1980-6826-430C-B837-63EB9468766D}') {
        throw 'Unrecognized CadFontAuto bundle; migration stopped.'
    }
}
if ($ValidateOnly) { Write-Output 'Legacy migration preflight passed.'; return }
if (Get-Process -Name acad,accoreconsole -ErrorAction SilentlyContinue) { throw 'Close AutoCAD before migrating CadFontAuto.' }
if ($keys.Count -eq 0 -and -not (Test-Path -LiteralPath $legacy)) { return }
$backup = Join-Path $env:LOCALAPPDATA ('BatchPlotPlus\MigrationBackups\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$exports = @()
$moved = $false
try {
    foreach ($key in $keys) {
        $export = Join-Path $backup ('loader-' + $exports.Count + '.reg')
        & reg.exe export $key.Name $export /y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Legacy loader backup failed.' }
        $exports += $export
    }
    if (Test-Path -LiteralPath $legacy) {
        # Exact inspected bundle path, moved outside ApplicationPlugins discovery.
        Move-Item -LiteralPath $legacy -Destination (Join-Path $backup 'CadFontAuto.bundle')
        $moved = $true
    }
    foreach ($key in $keys) { Remove-Item -LiteralPath $key.PSPath -Recurse -Force }
    Write-Output "Legacy module disabled. Rollback files: $backup"
    Write-Output 'Legacy font alias files and support paths were preserved; integrated Clear only removes its own new cache.'
} catch {
    $failure = $_
    foreach ($export in $exports) {
        & reg.exe import $export | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Registry rollback failed: $export" }
    }
    if ($moved) { Move-Item -LiteralPath (Join-Path $backup 'CadFontAuto.bundle') -Destination $legacy }
    throw $failure
}
