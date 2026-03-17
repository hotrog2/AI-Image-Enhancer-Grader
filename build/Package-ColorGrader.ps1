[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$appProject = Join-Path $repoRoot "src\ColorGrader.App\ColorGrader.App.csproj"
$publishProfile = "Release-win-x64"

$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish\$RuntimeIdentifier"
$stagingDir = Join-Path $artifactsRoot "staging\$RuntimeIdentifier"
$packageDir = Join-Path $artifactsRoot "packages"
$portableZip = Join-Path $packageDir "ColorGrader-$RuntimeIdentifier-portable.zip"
$installerZip = Join-Path $packageDir "ColorGrader-$RuntimeIdentifier-installer.zip"
$installerRoot = Join-Path $stagingDir "ColorGrader-$RuntimeIdentifier-installer"
$installerAppRoot = Join-Path $installerRoot "app"

foreach ($path in @($publishDir, $stagingDir, $packageDir)) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

Write-Host "Publishing ColorGrader for $RuntimeIdentifier..."
dotnet publish $appProject `
    -c $Configuration `
    -p:PublishProfile=$publishProfile `
    -p:RuntimeIdentifier=$RuntimeIdentifier `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$appExe = Join-Path $publishDir "ColorGrader.App.exe"
if (-not (Test-Path $appExe)) {
    throw "Expected published executable was not found at $appExe"
}

Write-Host "Building package staging layout..."
New-Item -ItemType Directory -Path $installerAppRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $installerAppRoot -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot "build\Install-ColorGrader.ps1") -Destination (Join-Path $installerRoot "Install-ColorGrader.ps1")
Copy-Item -Path (Join-Path $repoRoot "build\Uninstall-ColorGrader.ps1") -Destination (Join-Path $installerRoot "Uninstall-ColorGrader.ps1")

@"
ColorGrader package

Portable:
  Run app\ColorGrader.App.exe directly.

Installed:
  1. Right-click Install-ColorGrader.ps1 and run with PowerShell.
  2. The script copies the app into `%LocalAppData%\Programs\ColorGrader`.
  3. Start Menu and optional Desktop shortcuts are created.
"@ | Set-Content -Path (Join-Path $installerRoot "README.txt")

Write-Host "Creating zip artifacts..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $installerRoot "*") -DestinationPath $installerZip -CompressionLevel Optimal

Write-Host ""
Write-Host "Package flow complete."
Write-Host "Publish output:  $publishDir"
Write-Host "Portable zip:   $portableZip"
Write-Host "Installer zip:  $installerZip"
