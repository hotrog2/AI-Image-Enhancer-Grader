[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\ColorGrader"),
    [switch]$CreateDesktopShortcut
)

$ErrorActionPreference = "Stop"

$packageAppRoot = Join-Path $PSScriptRoot "app"
$packageExe = Join-Path $packageAppRoot "ColorGrader.App.exe"

if (-not (Test-Path $packageExe)) {
    throw "The packaged app folder was not found. Run this script from the extracted installer package."
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $packageAppRoot "*") -Destination $InstallDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot "Uninstall-ColorGrader.ps1") -Destination (Join-Path $InstallDir "Uninstall-ColorGrader.ps1") -Force

$appExe = Join-Path $InstallDir "ColorGrader.App.exe"
$startMenuPrograms = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutName = "Color Grader.lnk"
$uninstallShortcutName = "Uninstall Color Grader.lnk"

$shell = New-Object -ComObject WScript.Shell

$appShortcut = $shell.CreateShortcut((Join-Path $startMenuPrograms $shortcutName))
$appShortcut.TargetPath = $appExe
$appShortcut.WorkingDirectory = $InstallDir
$appShortcut.Save()

$uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuPrograms $uninstallShortcutName))
$uninstallShortcut.TargetPath = "powershell.exe"
$uninstallShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$InstallDir\Uninstall-ColorGrader.ps1`""
$uninstallShortcut.WorkingDirectory = $InstallDir
$uninstallShortcut.Save()

if ($CreateDesktopShortcut) {
    $desktopShortcut = $shell.CreateShortcut((Join-Path $desktop $shortcutName))
    $desktopShortcut.TargetPath = $appExe
    $desktopShortcut.WorkingDirectory = $InstallDir
    $desktopShortcut.Save()
}

Write-Host "ColorGrader installed to $InstallDir"
Write-Host "Start Menu shortcut created."
if ($CreateDesktopShortcut) {
    Write-Host "Desktop shortcut created."
}
