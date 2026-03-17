[CmdletBinding()]
param(
    [string]$InstallDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

$startMenuPrograms = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$desktop = [Environment]::GetFolderPath("Desktop")

foreach ($shortcut in @(
    (Join-Path $startMenuPrograms "Color Grader.lnk"),
    (Join-Path $startMenuPrograms "Uninstall Color Grader.lnk"),
    (Join-Path $desktop "Color Grader.lnk")
)) {
    if (Test-Path $shortcut) {
        Remove-Item -Path $shortcut -Force
    }
}

Write-Host "Scheduling removal of $InstallDir"
Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c ping 127.0.0.1 -n 2 > nul && rmdir /s /q `"$InstallDir`"" `
    -WindowStyle Hidden

Write-Host "ColorGrader uninstall started."
