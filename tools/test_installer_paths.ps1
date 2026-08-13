param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerZip
)

$ErrorActionPreference = "Stop"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("BatchPlotPlus-installer-paths-" + [Guid]::NewGuid().ToString("N"))
$chineseName = "installer " + [char]0x4E2D + [char]0x6587
$names = @("installer space", "installer (3)", "installer & copy", $chineseName, "installer ! bang")

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    foreach ($name in $names) {
        $folder = Join-Path $testRoot $name
        Expand-Archive -LiteralPath $InstallerZip -DestinationPath $folder -Force
        $bat = Join-Path $folder "InstallOrUpdate.bat"
        $start = New-Object System.Diagnostics.ProcessStartInfo
        $start.FileName = "cmd.exe"
        $start.Arguments = '/d /c ""' + $bat + '" -ValidateOnly"'
        $start.WorkingDirectory = $folder
        $start.UseShellExecute = $false
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.CreateNoWindow = $true
        $start.EnvironmentVariables["BATCHPLOTPLUS_NO_PAUSE"] = "1"
        $process = [Diagnostics.Process]::Start($start)
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "Installer path test failed for '$name' with exit $($process.ExitCode): $stdout $stderr"
        }
        Write-Output "Installer path passed: $name"
    }
    $failureFolder = Join-Path $testRoot "installer failure"
    Expand-Archive -LiteralPath $InstallerZip -DestinationPath $failureFolder -Force
    Remove-Item -LiteralPath (Join-Path $failureFolder "release\BatchPlotPlus.bundle\PackageContents.xml") -Force
    $failureBat = Join-Path $failureFolder "InstallOrUpdate.bat"
    $failureStart = New-Object System.Diagnostics.ProcessStartInfo
    $failureStart.FileName = "cmd.exe"
    $failureStart.Arguments = '/d /c ""' + $failureBat + '" -ValidateOnly"'
    $failureStart.WorkingDirectory = $failureFolder
    $failureStart.UseShellExecute = $false
    $failureStart.RedirectStandardOutput = $true
    $failureStart.RedirectStandardError = $true
    $failureStart.CreateNoWindow = $true
    $failureStart.EnvironmentVariables["BATCHPLOTPLUS_NO_PAUSE"] = "1"
    $failureProcess = [Diagnostics.Process]::Start($failureStart)
    $failureOutput = $failureProcess.StandardOutput.ReadToEnd() + $failureProcess.StandardError.ReadToEnd()
    $failureProcess.WaitForExit()
    if ($failureProcess.ExitCode -ne 40 -or $failureOutput -notmatch "Validation-only check failed") {
        throw "Installer failure-path test did not return the expected exit code and message. Exit=$($failureProcess.ExitCode)"
    }
    Write-Output "Installer failure path passed: missing manifest returned exit 40"
    Write-Output "Installer path tests passed: $($names.Count)"
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
