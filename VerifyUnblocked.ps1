param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [switch]$Repair
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    [Console]::Error.WriteLine("Bundle folder was not found: $Path")
    exit 20
}

$files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction Stop)
if ($files.Count -eq 0) {
    [Console]::Error.WriteLine("Bundle folder contains no files: $Path")
    exit 21
}

$failures = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
    if ($Repair) {
        try {
            Unblock-File -LiteralPath $file.FullName -ErrorAction Stop
        } catch {
            $failures.Add("Could not unblock: $($file.FullName) -- $($_.Exception.Message)")
            continue
        }
    }

    try {
        $streams = @(Get-Item -LiteralPath $file.FullName -Stream * -ErrorAction Stop)
        if ($streams.Stream -contains "Zone.Identifier") {
            $failures.Add("Zone.Identifier remains: $($file.FullName)")
        }
    } catch {
        $failures.Add("Could not inspect alternate data streams: $($file.FullName) -- $($_.Exception.Message)")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { [Console]::Error.WriteLine($_) }
    [Console]::Error.WriteLine("Blocked-file verification failed for $($failures.Count) of $($files.Count) file(s).")
    exit 22
}

Write-Output "Blocked-file verification passed for all $($files.Count) file(s): $Path"
exit 0
